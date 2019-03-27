﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI
{
    using System;

    public static class StatusConsole
    {
        public static void WriteLine(MessageType type, string message)
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
            Console.Write("]: ");

            Console.WriteLine(message);
        }
    }
}
