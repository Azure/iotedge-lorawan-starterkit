// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoRaWan.Tools.CLI.Options;

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

        public static string GetDataRatesforLocale(string locale)
        {
            StringBuilder result = new StringBuilder();
            result.Append(locale + ": ");

            if (string.Equals(locale, "EU", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string dr in euValidDataranges)
                    result.Append(dr + ", ");
            }
            else
            {
                foreach (string dr in usValidDataranges)
                    result.Append(dr + ", ");
            }

            result.Remove(result.Length - 2, 2);
            result.Append(".");

            return result.ToString();
        }

        public static string CleanString(string workString)
        {
            if (!string.IsNullOrEmpty(workString))
            {
                workString = workString.Trim().Replace("\'", string.Empty);
            }

            return workString;
        }

        public static string CleanNetId(string inNetId)
        {
            string outNetId = null;

            if (!string.IsNullOrEmpty(inNetId))
            {
                outNetId = inNetId.Trim().Replace("\'", string.Empty);
                if (outNetId.Length < 6)
                    outNetId = string.Concat(new string('0', 6).Substring(outNetId.Length), outNetId);

                if (outNetId.Length > 6)
                    outNetId = outNetId.Substring(outNetId.Length - 6);
            }

            return outNetId;
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
                return valueBool ? bool.TrueString.ToLower() : bool.FalseString.ToLower();
            }

            return value.ToString();
        }

        public static bool ValidateHexStringTwinProperty(string hexString, int byteCount, out string error)
        {
            error = null;

            // hexString not dividable by 2.
            if (hexString.Length % 2 > 0)
            {
                error = "Hex string must contain an even number of characters";
                return false;
            }

            // hexString doesn't contain byteCount bytes.
            if (hexString.Length >> 1 != byteCount)
            {
                error = $"Hex string doesn't contain the expected number of {byteCount} bytes";
                return false;
            }

            // Verify each individual byte for validity.
            for (int i = 0; i + 1 < hexString.Length; i += 2)
            {
                if (!int.TryParse(hexString.Substring(i, 2), System.Globalization.NumberStyles.HexNumber, null, out _))
                {
                    error = $"Hex string contains invalid byte {hexString.Substring(i, 2)}";
                    return false;
                }
            }

            return true;
        }

        public static dynamic ConvertToBoolTwinProperty(string property)
        {
            property = property.Trim();

            if (string.Equals(bool.TrueString, property, StringComparison.OrdinalIgnoreCase) ||
                string.Equals("1", property, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (string.Equals(bool.FalseString, property, StringComparison.OrdinalIgnoreCase) ||
                string.Equals("0", property, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else if (string.Equals("null", property, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            else
            {
                return property;
            }
        }

        public static bool ValidateBoolTwinProperty(string property, out string error)
        {
            error = null;

            if (!string.IsNullOrEmpty(property))
            {
                property = property.Trim();
            }

            if (string.Equals(bool.TrueString, property, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(bool.FalseString, property, StringComparison.OrdinalIgnoreCase) ||
                string.Equals("1", property, StringComparison.OrdinalIgnoreCase) ||
                string.Equals("0", property, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else
            {
                error = "Property must be \"1\", \"True\", \"0\" or \"False\" - or \"null\" to clear.";
                return false;
            }
        }

        public static string ConvertToStringTwinProperty(string property)
        {
            if (!string.IsNullOrEmpty(property))
            {
                property = property.Trim();
            }

            if (string.Equals("null", property, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            else
            {
                return property;
            }
        }

        public static dynamic ConvertToUIntTwinProperty(string property)
        {
            // This method is just used for casting, not for validation.
            // In case the value passed is not a valid uint, the original string is to be returned.
            if (string.Equals("null", property, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            else
            {
                if (uint.TryParse(property, out var uintProperty))
                    return uintProperty;
                else
                    return property;
            }
        }

        public static uint? ConvertToUIntProperty(string property)
        {
            if (uint.TryParse(property, out var uintProperty))
                return uintProperty;
            else
                return null;
        }

        public static bool ValidateUIntRangeTwinProperty(string property, uint? min, uint? max, out string error)
        {
            var isValid = true;
            error = null;

            if (uint.TryParse(property, out var uintProperty))
            {
                if (min != null)
                {
                    if (uintProperty < min)
                    {
                        error = $"Property is smaller then the expected minimum of {min}.";
                        isValid = false;
                    }
                }

                if (max != null)
                {
                    if (uintProperty > max)
                    {
                        error = $"Property is larger then the expected maximum of {max}.";
                        isValid = false;
                    }
                }
            }
            else
            {
                error = $"Property is not valid. Needs to be a non-negative number between {min} and {max}.";
                isValid = false;
            }

            return isValid;
        }

        public static bool ValidateUIntTwinProperty(string property, uint expected, out string error)
        {
            var isValid = true;
            error = null;

            if (uint.TryParse(property, out var uintProperty))
            {
                if (uintProperty != expected)
                {
                    isValid = false;
                }
            }
            else
            {
                isValid = false;
            }

            if (!isValid)
                error = $"Property is not the expected value of {expected}.";

            return isValid;
        }

        public static bool ValidateDataRateTwinProperty(string property, out string error)
        {
            error = null;

            if (!euValidDataranges.Contains(property) && !usValidDataranges.Contains(property))
            {
                error = "Property is not a valid data rate";
                return false;
            }

            return true;
        }

        public static bool ValidateSensorDecoder(string sensorDecoder, bool isVerbose)
        {
            var isValid = true;
            var isWarning = false;

            if (sensorDecoder == null)
            {
                StatusConsole.WriteLogLine(MessageType.Error, "SensorDecoder is missing.");
                return false;
            }

            if (sensorDecoder == string.Empty)
            {
                if (isVerbose)
                    StatusConsole.WriteLogLine(MessageType.Info, "SensorDecoder is empty. No decoder will be used.");

                return true;
            }

            if (sensorDecoder.StartsWith("http", StringComparison.OrdinalIgnoreCase) || sensorDecoder.Contains('/'))
            {
                if (!Uri.TryCreate(sensorDecoder, UriKind.Absolute, out Uri validatedUri))
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "SensorDecoder has an invalid URL.");
                    isValid = false;
                }

                // if (validatedUri.Host.Any(char.IsUpper))
                if (!sensorDecoder.StartsWith(validatedUri.Scheme, StringComparison.OrdinalIgnoreCase)
                    || sensorDecoder.IndexOf(validatedUri.Host) != validatedUri.Scheme.Length + 3)
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "SensorDecoder Hostname must be all lowercase.");
                    isValid = false;
                }

                if (validatedUri.AbsolutePath.IndexOf("/api/") < 0)
                {
                    if (isVerbose)
                        StatusConsole.WriteLogLine(MessageType.Warning, "SensorDecoder is missing \"api\" keyword.");

                    isWarning = true;
                }
            }

            if (isVerbose)
            {
                if (!isValid || isWarning)
                {
                    StatusConsole.WriteLogLine(MessageType.Info, "Make sure the URI based SensorDecoder Twin desired property looks like \"http://containername/api/decodername\".");
                }
                else
                {
                    StatusConsole.WriteLogLine(MessageType.Info, $"SensorDecoder {sensorDecoder} is valid.");
                }
            }

            return isValid;
        }

        public static bool ValidateFcntSettings(AddOptions opts, string fCntUpStartReportedString, string fCntDownStartReportedString, string fCntResetCounterReportedString)
        {
            var isValid = true;

            var abpRelaxMode = string.IsNullOrEmpty(opts.ABPRelaxMode) ||
                string.Equals(opts.ABPRelaxMode, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(opts.ABPRelaxMode, "true", StringComparison.OrdinalIgnoreCase);

            var fCntUpStart = ConvertToUIntProperty(opts.FCntUpStart);
            var fCntDownStart = ConvertToUIntProperty(opts.FCntDownStart);
            var fCntResetCounter = ConvertToUIntProperty(opts.FCntResetCounter);

            var fCntUpStartReported = ConvertToUIntProperty(fCntUpStartReportedString);
            var fCntDownStartReported = ConvertToUIntProperty(fCntDownStartReportedString);
            var fCntResetCounterReported = ConvertToUIntProperty(fCntResetCounterReportedString);

            // AbpRelaxMode FALSE
            if (!abpRelaxMode)
            {
                isValid &= ValidateSingleFcnt(fCntUpStart, fCntUpStartReported, fCntResetCounter, fCntResetCounterReported, "FCntUpStart");
                isValid &= ValidateSingleFcnt(fCntDownStart, fCntDownStartReported, fCntResetCounter, fCntResetCounterReported, "FCntDownStart");
            }

            return isValid;
        }

        private static bool ValidateSingleFcnt(uint? fCntStart, uint? fCntStartRep, uint? fCntResetCounter, uint? fCntResetCounterRep, string fCntStartType)
        {
            if (fCntStart == null)
            {
                // fCntStart not set. Nothing to do.
                return true;
            }

            // fCntStartRep not null
            if (fCntStartRep == null || fCntStart > fCntStartRep)
            {
                StatusConsole.WriteLogLine(MessageType.Info, $"{fCntStartType} {fCntStart} will be set on gateway.");
                return true;
            }

            // fCntStartRep not null, fCntStartRep not null, fCntStart <= fCntStartRep
            if (fCntResetCounter == null)
            {
                StatusConsole.WriteLogLine(MessageType.Warning, $"{fCntStartType} {fCntStart} will not be set on gateway. Reported {fCntStartType} {fCntStartRep} is larger or equal and FCntResetCounter is not set.");
                return true;
            }

            // fCntStartRep not null, fCntStartRep not null, fCntStart <= fCntStartRep,
            // fCntResetCounter not null
            if (fCntResetCounterRep == null || fCntResetCounter > fCntResetCounterRep)
            {
                StatusConsole.WriteLogLine(MessageType.Info, $"{fCntStartType} {fCntStart} will be set on gateway.");
                return true;
            }

            // fCntStartRep not null, fCntStartRep not null, fCntStart <= fCntStartRep,
            // fCntResetCounter not null, fCntResetCounterRep not null, fCntResetCounter <= fCntResetCounterRep
            StatusConsole.WriteLogLine(MessageType.Warning, $"{fCntStartType} {fCntStart} will not be set on gateway. Reported {fCntStartType} {fCntStartRep} is larger or equal and FCntResetCounter {fCntResetCounter} is not larger than reported FCntResetCounter {fCntResetCounterRep}.");
            return true;
        }
    }
}
