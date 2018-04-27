using System;
using System.Collections.Generic;

namespace TestAppForEverything
{
    class Program
    {
        static void Main(string[] args)
        {
            constructConnectionStringMask();
            Console.ReadLine();
        }

        static string hubConnParams = "HostName=testloriotv3hub.azure-devices.net;SharedAccessKeyName=device;SharedAccessKey=Wt1vLxOMHtCVTILfxzFBAyE3wWguBQOM8NM14dz8YVw=";
        static string originalConnectionString = "HostName=testloriotv3hub.azure-devices.net;GatewayHostName=cehackpi1;DeviceId=LoraEdgeGatewayPiRonnie;ModuleId=loraudpfiltermodule;SharedAccessKey=6YuLw1kWK0VKXgESQc6+T3Wc/WOOXSxB3km0WIATq5Q=";
        static string connectionStringMask;
        static void constructConnectionStringMask()
        {
            //Constructing new Connection String
            connectionStringMask += $"HostName={getVal("HostName", originalConnectionString)};";
            connectionStringMask += $"GatewayHostName={getVal("GatewayHostName", originalConnectionString)};";
            connectionStringMask += $"DeviceId=<deviceId>;";
            connectionStringMask += $"SharedAccessKeyName={getVal("SharedAccessKeyName", hubConnParams)};";
            connectionStringMask += $"SharedAccessKey={getVal("SharedAccessKey", hubConnParams)};";

            string getVal(string key, string connStr)
            {
                string val = null;

                foreach (var keyVal in connStr.Split(';'))
                {
                    int splIndex = keyVal.IndexOf('=');
                    string k = keyVal.Substring(0, splIndex);
                    if(k.ToLower().Trim() == key.ToLower().Trim())
                    {
                        val = keyVal.Substring(splIndex + 1, keyVal.Length - splIndex - 1);
                        break;
                    }
                }

                if(string.IsNullOrEmpty(val))
                {
                    throw new Exception($"Key '{key}' not found.");
                }
                else
                {
                    return val;
                }
            }
        }
    }
}
