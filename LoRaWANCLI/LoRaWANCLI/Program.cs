using System;
using System.IO;
using LoRaWANDevice;

namespace LoRaWANCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("LoRaWAN device provisioning for Microsoft Azure IoT Hub");
            Console.WriteLine("10th October 2018 Version 0.1");
            Console.WriteLine("");
            Console.WriteLine("");

            if (args.Length==0)
            {
                // No args fail and print help message
                HelpMessage();
            }

            // Validate args are present for appropriate use cases
            // Generate config file
            // Validate provided config file

            Generator creator = new Generator();

            for(int i=0; i<args.Length;i++)
            {

                switch (args[i].ToLower())
                {
                    case "/backup":
                    case "/b":
                        {
                            creator.Configuration.Backup = true;
                            break;
                        }
                    case "/configfile":
                    case "/c":
                        {
                            creator.Configuration.ConfigFile = true;
                            // Obtain next parameter
                            i++;
                            // Should be a file path string
                            if (File.Exists(args[i]))
                            {
                                creator.Configuration.ConfigFilePath = args[i];
                                if (creator.LoadValidateConfigFile(args[i]) == 0)
                                {
                                    creator.Provision().Wait();
                                }
                                else
                                {
                                    Console.WriteLine("Invalid configuration file format/data %s", args[i]);
                                    Console.WriteLine("");
                                    HelpMessage();
                                }

                            }
                            else
                            {
                                Console.WriteLine("Invalid configuration path specified %s", args[i]);
                                Console.WriteLine("");
                                HelpMessage();
                            }
                            break;
                        }
                    case "/abp":
                        {
                            creator.Configuration.ActivationMethod = NodeActivationMethod.ABP;
                            break;
                        }
                    case "/otaa":
                        {
                            creator.Configuration.ActivationMethod = NodeActivationMethod.OTAA;
                            break;
                        }
                    case "/createconfigfile":
                    case "/f":
                        {
                            i++;
                            // Serialise config file class
                            // using next parameter as file name
                            creator.MakeConfigFile(args[i]);
                            return;
                        }
                    default:
                        {
                            break;
                        }
                }


            }
            //creator set IoT Hub Connection string
            //creator set Blob storage connection string (optional based on backup or bulk)
            //
        }

        static void HelpMessage()
        {
            Console.WriteLine("Usage instructions");
        }
    }
}
