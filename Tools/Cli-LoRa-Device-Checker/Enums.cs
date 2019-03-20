// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cli_LoRa_Device_Checker
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public enum MessageType
    {
        Info,
        Warning,
        Error
    }

    public enum ExitCode : int
    {
        Error = -1,
        Success = 0,
        InvalidLogin = 1,
        InvalidFilename = 2,
        UnknownError = 10
    }

    class Enums
    {
    }
}
