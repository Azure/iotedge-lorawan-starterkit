// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI
{
    using System;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;

    public static class StatusConsole
    {
        public static void WriteIfVerbose(string message, bool isVerbose)
        {
            if (isVerbose)
            {
                Console.Write(message);
            }
        }

        public static void WriteLineIfVerbose(string message, bool isVerbose)
        {
            if (isVerbose)
            {
                Console.WriteLine(message);
            }
        }

        public static void WriteLogLine(MessageType type, string message)
        {
            WriteMessageType(type);
            Console.WriteLine(message);
        }

        public static void WriteLogLineIfVerbose(MessageType type, string message, bool isVerbose)
        {
            if (isVerbose)
            {
                WriteMessageType(type);
                Console.WriteLine(message);
            }
        }

        public static void WriteLogLineWithDevEuiWhenVerbose(MessageType type, string message, string devEui, bool isVerbose)
        {
            WriteMessageType(type);

            if (!isVerbose)
            {
                Console.Write($"Device: {devEui} ");
            }

            Console.WriteLine(message);
        }

        private static void WriteMessageType(MessageType type)
        {
            Console.Write("[");
            if (type == MessageType.Info)
                Console.ForegroundColor = ConsoleColor.Green;
            if (type == MessageType.Warning)
                Console.ForegroundColor = ConsoleColor.Yellow;
            if (type == MessageType.Error)
                Console.ForegroundColor = ConsoleColor.Red;

            Console.Write(type);
            Console.ResetColor();
            Console.Write("] ");
        }

        public static void WriteTwin(string devEui, Twin twin)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"DevEUI: {devEui}");
            Console.WriteLine(TwinToString(twin));
            Console.ResetColor();
        }

        private static string TwinToString(Twin twin)
        {
            var twinData = JObject.Parse(twin.Properties.Desired.ToJson());
            twinData.Remove("$metadata");
            twinData.Remove("$version");
            return twinData.ToString();
        }
    }
}
