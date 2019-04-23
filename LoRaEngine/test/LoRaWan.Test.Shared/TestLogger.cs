// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Helper class enabling logging in Integration Test
    /// When running the tests in Visual Studio Log does not output
    /// </summary>
    public static class TestLogger
    {
        public static bool LogDate { get; set; }

        /// <summary>
        /// Logs
        /// </summary>
        public static void Log(string text)
        {
            if (LogDate)
            {
                text = string.Concat(DateTime.UtcNow.ToString("HH:mm:ss.fff"), " ", text);
            }

            // Log to diagnostics if a debbuger is attached
            if (Debugger.IsAttached)
            {
                System.Diagnostics.Debug.WriteLine(text);
            }
            else
            {
                Console.WriteLine(text);
            }
        }
    }
}
