// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cli_LoRa_Device_Checker
{
    using System;
    using System.Collections.Generic;

    public static class ValidationHelper
    {
        private static List<string> euValidDataranges = new List<string>()
            {
                "SF12BW125", // 0
                "SF11BW125", // 1
                "SF10BW125", // 2
                "SF9BW125", // 3
                "SF8BW125", // 4
                "SF7BW125", // 5
                "SF7BW250", // 6
                "50" // 7 FSK 50
            };

        private static List<string> usValidDataranges = new List<string>()
            {
                "SF10BW125", // 0
                "SF9BW125", // 1
                "SF8BW125", // 2
                "SF7BW125", // 3
                "SF8BW500", // 4
                "SF12BW500", // 8
                "SF11BW500", // 9
                "SF10BW500", // 10
                "SF9BW500", // 11
                "SF8BW500", // 12
                "SF8BW500" // 13
            };

        public static string GetEUDataRates()
        {
            string result = "EU: ";

            foreach (string dr in euValidDataranges)
                result += dr + ", ";

            result += ".";

            result = result.Substring(0, result.Length - 2);
            return result;
        }

        public static string GetUSDataRates()
        {
            string result = "US: ";

            foreach (string dr in usValidDataranges)
                result += dr + ", ";

            result = result.Substring(0, result.Length - 2);
            result += ".";

            return result;
        }

        public static string CleanString(string inString)
        {
            string outString = null;

            if (!string.IsNullOrEmpty(inString))
            {
                outString = inString.Replace("\'", string.Empty);
            }

            return outString;
        }

        public static bool ValidateHexStringTwinProperty(string hexString, int byteCount, out string error)
        {
            error = string.Empty;

            // hexString not dividable by 2.
            if (hexString.Length % 2 > 0)
            {
                error = "Hex string must contain an even number of characters";
                return false;
            }

            // hexString doesn't contain byteCount bytes.
            if (hexString.Length / 2 != byteCount)
            {
                error = $"Hex string doesn't contain the expected number of {byteCount} bytes";
                return false;
            }

            // Verify each individual byte for validity.
            for (int i = 0; i < hexString.Length; i += 2)
            {
                if (!int.TryParse(hexString.Substring(i, 2), System.Globalization.NumberStyles.HexNumber, null, out _))
                {
                    error = $"Hex string contains invalid byte {hexString.Substring(i, 2)}";
                    return false;
                }
            }

            return true;
        }

        public static bool ValidateBoolTwinProperty(string property, out string error)
        {
            error = string.Empty;

            property = property.Trim();

            if (string.Equals("true", property, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals("false", property, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals("1", property, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals("0", property, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else
            {
                error = "Property must be 1, True, 0 or False.";
                return false;
            }
        }

        public static bool SetBoolTwinProperty(string property)
        {
            property = property.Trim();

            if (string.Equals("true", property, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals("1", property, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool ValdateIntTwinProperty(string property, int? min, int? max, out string error)
        {
            var isValid = true;
            error = string.Empty;

            if (int.TryParse(property, out var intProperty))
            {
                if (min != null)
                {
                    if (intProperty < min)
                    {
                        error = $"Property is smaller then the expected minimum of {min}.";
                        isValid = false;
                    }
                }

                if (max != null)
                {
                    if (intProperty > max)
                    {
                        error = $"Property is larger then the expected maximum of {max}.";
                        isValid = false;
                    }
                }
            }
            else
            {
                error = "Property is not a valid integer.";
                isValid = false;
            }

            return isValid;
        }

        public static bool ValdateDataRateTwinProperty(string property, out string error)
        {
            var isValid = true;
            error = string.Empty;

            if (!euValidDataranges.Contains(property) && !usValidDataranges.Contains(property))
            {
                error = $"Property is not a valid data rate.";
                isValid = false;
            }

            return isValid;
        }

        public static bool ValidateSensorDecoder(string sensorDecoder, out string error)
        {
            var isValid = true;
            error = string.Empty;

            if (string.IsNullOrEmpty(sensorDecoder))
            {
                error += "Info: SensorDecoder is empty. No decoder will be used. ";
                return isValid;
            }

            if (sensorDecoder.StartsWith("http") || sensorDecoder.Contains('/'))
            {
                if (!Uri.TryCreate(sensorDecoder, UriKind.Absolute, out Uri validatedUri))
                {
                    error += "Error: SensorDecoder has invalid URL. ";
                    isValid = false;
                }

                // if (validatedUri.Host.Any(char.IsUpper))
                if (!sensorDecoder.Contains(validatedUri.Host))
                {
                    error += "Error: SensorDecoder Hostname must be all lowercase. ";
                    isValid = false;
                }

                if (validatedUri.AbsolutePath.IndexOf("/api/") < 0)
                {
                    error += "Error: SensorDecoder is missing \"api\" keyword. ";
                    isValid = false;
                }
            }

            if (!isValid)
            {
                error += "\nMake sure the URI based SensorDecoder Twin desired property looks like \"http://containername/api/decodername\".";
            }

            return isValid;
        }

        public static string GetTwinPropertyValue(dynamic value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is string valueString)
            {
                return valueString.Trim();
            }

            if (value is bool valueBool)
            {
                if (valueBool)
                    return "true";
                else
                    return "false";
            }

            if (value is int valueInt)
            {
                return valueInt.ToString();
            }

            return value.ToString();
        }
    }
}
