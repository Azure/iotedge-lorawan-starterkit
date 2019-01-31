// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    // Defines how IoTHub message validation should occur
    public enum LogValidationAssertLevel
    {
        // Ignore it assertion, don't even try to validated
        Ignore,

        // Validate returning warnings if something does not work as expected
        Warning,

        // Threat unexpected behavior as error (strict)
        Error,
    }
}
