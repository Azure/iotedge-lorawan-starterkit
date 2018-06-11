namespace PacketForwarderHost
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;     
    using Microsoft.Azure.Devices.Shared; // for TwinCollection
    using Newtonsoft.Json;                // for JsonConvert
    using Microsoft.Azure.Devices.Client;
    partial class Program
    {
        
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
                if(packetForwarderProcess is null) return; // First time execution, packet process not started
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

        /// Write them to the global_conf.json for the packet forwarder binary to consume
        /// Force restart of the binary packet forwarder
        /// Issues:
        ///     doesn't retain last configId value processed
        static void processDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            
            Console.WriteLine("Processing property updates");
            TwinCollection packetForwarderConfig = new TwinCollection();
            try {
   
                    //LoadJson: get the current global_conf.json
                    if (File.Exists("./global_conf.json"))
                    {
                        Console.WriteLine("Reading existing global_conf.json");
                        using (StreamReader r = new StreamReader("./global_conf.json"))
                        {
                            string currentConfigJson = r.ReadToEnd();
                            packetForwarderConfig["configId"] = "0";
                            packetForwarderConfig["global_conf"] = currentConfigJson;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Creating new global_conf.json");
                        packetForwarderConfig["configId"] = "0";
                        packetForwarderConfig["global_conf"] = "{\"gateway_conf\": {}}";
                    }

                    if ((desiredProperties != null) && (desiredProperties["configId"] != packetForwarderConfig["configId"]))
                    {
                        Console.WriteLine("Applying changes to global_conf.json");
                        TwinCollection global_Config = new TwinCollection(packetForwarderConfig["global_conf"].ToString());
                        TwinCollection desiredConfig = new TwinCollection(desiredProperties["global_conf"].ToString());
                        Console.WriteLine(desiredProperties["global_conf"].ToString());

                        // Iterate over top elements in global_conf, then properties of those elements
                        foreach (System.Collections.Generic.KeyValuePair<string,object> prop in desiredConfig)
                        {

                            Console.WriteLine(prop.Key.ToString());
                            // This test required. IoT Hub sends historic properties previously set, with null values
                            if(prop.Value.ToString() != "")
                            {
                                Console.WriteLine(prop.Value.ToString());
                                // set to current config
                                TwinCollection newConfig = new TwinCollection(global_Config[prop.Key].ToString()); 
                                // set to desired config top element's properties
                                TwinCollection targetConfig = new TwinCollection(prop.Value.ToString());
                                // Update or add each desired property value to current config
                                foreach (System.Collections.Generic.KeyValuePair<string, object> newprop in targetConfig) 
                                {
                                    Console.WriteLine(newprop.Key.ToString());
                                    Console.WriteLine(newprop.Value.ToString());
                                    // Updates or Inserts properties to existing config
                                    newConfig[newprop.Key.ToString()] = newprop.Value;
                                }
                                // replaces existing config with updated config
                                global_Config[prop.Key] = newConfig;
                            }
                        }

                        Console.WriteLine("Writing new global_conf.json");
                        // write out the new global_conf.json
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Formatting = Formatting.Indented;
                        serializer.NullValueHandling = NullValueHandling.Ignore;
                        packetForwarderConfig["global_conf"] = global_Config;

                        using (StreamWriter sw = new StreamWriter("./global_conf.json"))
                        using (JsonWriter writer = new JsonTextWriter(sw))
                        {
                            // writes only the contents of global_conf, thereby not capturing configId value, 
                            // and not corrupting global_conf.json format
                            serializer.Serialize(writer, packetForwarderConfig["global_conf"]);
                        }
                        // Update the packetForwarderConfig configId
                        Console.WriteLine("Packet Forwarder Updated");

                        // stop and restart the binary packet forwarder process
                        StopPacketForwarder();
                        StartPacketForwarder(packetforwardercli);

                        DeviceClient ioTHubModuleClient = (DeviceClient) userContext;
                        TwinCollection reportedProps = new TwinCollection();
                        reportedProps["reported"] = packetForwarderConfig["global_conf"];
                        ioTHubModuleClient.UpdateReportedPropertiesAsync(reportedProps);

                    }
                }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error in packetforwarder module: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in packetforwarder module: {0}", ex.Message);
            }
        } 

    }
}