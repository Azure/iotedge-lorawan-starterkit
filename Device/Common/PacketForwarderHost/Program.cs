namespace PacketForwarderHost
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    using System.Collections.Generic;     
    using Microsoft.Azure.Devices.Shared; // for TwinCollection
    using Newtonsoft.Json;                // for JsonConvert

    using PacketForwarder_JSON;

    class Program
    {
        static int counter;
        static string packetforwardercli = "./single_chan_pkt_fwd_eth0";

        static System.Diagnostics.Process packetForwarderProcess;

        static void Main(string[] args)
        {

            Console.WriteLine("Up and running");

            // The Edge runtime gives us the connection string we need -- it is injected as an environment variable
            string connectionString = Environment.GetEnvironmentVariable("EdgeHubConnectionString");

            // Cert verification is not yet fully functional when using Windows OS for the container
            bool bypassCertVerification = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (!bypassCertVerification) InstallCert();
            Init(connectionString, bypassCertVerification).Wait();

            StartPacketForwarder(packetforwardercli);

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Start the Packet Forwarder Process
        /// </summary>
        public static void StartPacketForwarder(string cmdline)
        {
            try
            {
                Console.WriteLine("Starting Packet Forwarder");
                // start packet forwarder
                packetForwarderProcess = System.Diagnostics.Process.Start(cmdline);
            }
            catch (Exception e)
            {
                Console.WriteLine("Start packet forwarder failed with: ");
                Console.WriteLine(e.Message);
            }
            Console.WriteLine("Started Packet Forwarder");
        }

        public static void StopPacketForwarder()
        {
            try
            {
                packetForwarderProcess.Kill();
                while(!packetForwarderProcess.HasExited)
                {
                    Console.WriteLine("Stopping Packet Forwarder");
                    Thread.Sleep(250);
                }
                Console.WriteLine("Packet Forwarder Stopped");
                packetForwarderProcess.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Attemting to stop the packet forwarder failed with: ");
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Add certificate in local cert store for use by client for secure connection to IoT Edge runtime
        /// </summary>
        static void InstallCert()
        {
            string certPath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
            if (string.IsNullOrWhiteSpace(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing path to certificate file.");
            }
            else if (!File.Exists(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing certificate file.");
            }
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate2.CreateFromCertFile(certPath)));
            Console.WriteLine("Added Cert: " + certPath);
            store.Close();
        }


        /// <summary>
        /// Initializes the DeviceClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init(string connectionString, bool bypassCertVerification = false)
        {
            Console.WriteLine("Connection String {0}", connectionString);

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            // During dev you might want to bypass the cert verification. It is highly recommended to verify certs systematically in production
            if (bypassCertVerification)
            {
                mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            DeviceClient ioTHubModuleClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Register callback to be called when a message is received by the module
            //await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);
            var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
            var moduleTwinCollection = moduleTwin.Properties.Desired;
            // Attach callback for Twin desired properties updates
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, null);

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PacketForwarderHostMessages, ioTHubModuleClient);
        }

        /// <summary>
        /// Receive changes from the module twin
        /// Write them to the global_conf.json for the packet forwarder binary to consume
        /// Force restart of the binary packet forwarder
        /// </summary>
        static Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                RootObject currentConfig = null;
                TwinCollection packetForwarderConfig = new TwinCollection();

                //LoadJson: get the current global_conf.json
                if(File.Exists("./global_conf.json"))
                {
                    Console.WriteLine("Reading existing global_conf.json");
                    using (StreamReader r = new StreamReader("./global_conf.json"))
                    {
                        string currentConfigJson = r.ReadToEnd();
                        currentConfig = JsonConvert.DeserializeObject<RootObject>(currentConfigJson);

                        packetForwarderConfig["configId"] = "0";
                        packetForwarderConfig["global_config"] = currentConfigJson;
                    }
                }
                else
                {
                    Console.WriteLine("Creating new global_conf.json");
                    packetForwarderConfig["configId"] = "0";
                    packetForwarderConfig["global_config"] = "";
                }

                var desiredTelemetryConfig = desiredProperties["global_config"];

                if ((desiredTelemetryConfig != null) && (desiredTelemetryConfig["configId"] != packetForwarderConfig["configId"]))
                {
                    // archive the current global_conf.json

                    // write out the new global_conf.json
                    JsonSerializer serializer = new JsonSerializer();
                    //  serializer.Converters.Add(new JavaScriptDateTimeConverter());
                    serializer.NullValueHandling = NullValueHandling.Ignore;

                    using (StreamWriter sw = new StreamWriter("./global_conf.json"))
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(writer, desiredTelemetryConfig);
                    }
                    // stop and restart the binary packet forwarder process
                    StopPacketForwarder();
                    StartPacketForwarder(packetforwardercli);

                    // Update the packetForwarderConfig configId
                    Console.WriteLine("Packet Forwarder Updated");

                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error in sample: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
            }
            return Task.CompletedTask;
        }


        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PacketForwarderHostMessages(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var deviceClient = userContext as DeviceClient;
            if (deviceClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                var pipeMessage = new Message(messageBytes);
                foreach (var prop in message.Properties)
                {
                    pipeMessage.Properties.Add(prop.Key, prop.Value);
                }
                await deviceClient.SendEventAsync("output1", pipeMessage);
                Console.WriteLine("Received message sent");
            }
            return MessageResponse.Completed;
        }
    }
}
