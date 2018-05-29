namespace PacketForwarderHost
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;     
    using Microsoft.Azure.Devices.Shared; // for TwinCollection
    using Newtonsoft.Json;                // for JsonConvert
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
        ///     Targets only gateway_conf element of global_conf.json. Needs extending to process any section of global_conf.json
        static Task processDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
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
                        // Requires refactoring. Going to string to access sections of json is naff
                        TwinCollection global_Config = new TwinCollection(packetForwarderConfig["global_conf"].ToString());
                        TwinCollection newGatewayConfig = new TwinCollection(global_Config["gateway_conf"].ToString());
                        TwinCollection desiredGatewayConfig = new TwinCollection(desiredProperties["global_conf"].ToString());
                        foreach (Newtonsoft.Json.Linq.JProperty prop in desiredGatewayConfig["gateway_conf"])
                        {
                            // Updates or Inserts properties
                            newGatewayConfig[prop.Name] = prop.Value;
                        }

                        // write out the new global_conf.json
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.NullValueHandling = NullValueHandling.Ignore;
                        global_Config["gateway_conf"] = newGatewayConfig;
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
            return Task.CompletedTask;
        } 

    }
}