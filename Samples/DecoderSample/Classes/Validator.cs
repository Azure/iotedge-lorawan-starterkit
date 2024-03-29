// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SensorDecoderModule.Classes
{
    using System.Net;

    public static class Validator
    {
        public static void ValidateParameters(string fport, string payload)
        {
            var error = string.Empty;

            if (fport == null)
            {
                error += "Fport missing";
            }

            if (payload == null)
            {
                if (!string.IsNullOrEmpty(error))
                    error += " and ";
                error += "Payload missing";
            }

            if (!string.IsNullOrEmpty(error))
            {
                throw new WebException(error);
            }
        }
    }
}
