// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cli_LoRa_Device_Provisioning
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

        public static string CleanNetId(string inNetId)
        {
            string outNetId = null;

            if (!string.IsNullOrEmpty(inNetId))
            {
                outNetId = inNetId.Replace("\'", string.Empty);
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
                if (valueBool)
                    return "true";
                else
                    return "false";
            }

            if (value is uint valueUInt)
            {
                return valueUInt.ToString();
            }

            if (value is int valueInt)
            {
                return valueInt.ToString();
            }

            return value.ToString();
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

        public static dynamic SetBoolTwinProperty(string property)
        {
            property = property.Trim();

            if (string.Equals("true", property, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals("1", property, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else if (string.Equals("false", property, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals("0", property, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }
            else if (string.Equals("null", property, StringComparison.InvariantCultureIgnoreCase))
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
                error = "Property must be 1, True, 0 or False";
                return false;
            }
        }

        public static string SetStringTwinProperty(string property)
        {
            property = property.Trim();

            if (string.Equals("null", property, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }
            else
            {
                return property;
            }
        }

        public static dynamic SetUIntTwinProperty(string property)
        {
            if (string.Equals("null", property, StringComparison.InvariantCultureIgnoreCase))
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

        public static uint? SetUIntProperty(string property)
        {
            if (uint.TryParse(property, out var uintProperty))
                return uintProperty;
            else
                return null;
        }

        public static bool ValidateUIntTwinProperty(string property, uint? min, uint? max, out string error)
        {
            var isValid = true;
            error = string.Empty;

            if (uint.TryParse(property, out var uintProperty))
            {
                if (min != null)
                {
                    if (uintProperty < min)
                    {
                        error = $"Property is smaller then the expected minimum of {min}";
                        isValid = false;
                    }
                }

                if (max != null)
                {
                    if (uintProperty > max)
                    {
                        error = $"Property is larger then the expected maximum of {max}";
                        isValid = false;
                    }
                }
            }
            else
            {
                error = "Property is not a valid integer";
                isValid = false;
            }

            return isValid;
        }

        public static bool ValidateDataRateTwinProperty(string property, out string error)
        {
            var isValid = true;
            error = string.Empty;

            if (!euValidDataranges.Contains(property) && !usValidDataranges.Contains(property))
            {
                error = "Property is not a valid data rate";
                isValid = false;
            }

            return isValid;
        }

        public static bool ValidateSensorDecoder(string sensorDecoder)
        {
            var isValid = true;
            var isWarning = false;

            if (sensorDecoder == null)
            {
                StatusConsole.WriteLine(MessageType.Error, "SensorDecoder is missing.");
                return false;
            }

            if (sensorDecoder == string.Empty)
            {
                StatusConsole.WriteLine(MessageType.Info, "SensorDecoder is empty. No decoder will be used.");
                return true;
            }

            if (sensorDecoder.StartsWith("http") || sensorDecoder.Contains('/'))
            {
                if (!Uri.TryCreate(sensorDecoder, UriKind.Absolute, out Uri validatedUri))
                {
                    StatusConsole.WriteLine(MessageType.Error, "SensorDecoder has invalid URL.");
                    isValid = false;
                }

                // if (validatedUri.Host.Any(char.IsUpper))
                if (!sensorDecoder.Contains(validatedUri.Host))
                {
                    StatusConsole.WriteLine(MessageType.Error, "SensorDecoder Hostname must be all lowercase.");
                    isValid = false;
                }

                if (validatedUri.AbsolutePath.IndexOf("/api/") < 0)
                {
                    StatusConsole.WriteLine(MessageType.Warning, "SensorDecoder is missing \"api\" keyword.");
                    isWarning = true;
                }
            }

            if (!isValid || isWarning)
            {
                StatusConsole.WriteLine(MessageType.Info, "Make sure the URI based SensorDecoder Twin desired property looks like \"http://containername/api/decodername\".");
            }
            else
            {
                StatusConsole.WriteLine(MessageType.Info, $"SensorDecoder {sensorDecoder} is valid.");
            }

            return isValid;
        }

        public static bool ValidateFcntSettings(Program.AddOptions opts, string fCntUpStartReported, string fCntDownStartReported, string fCntResetCounterReported)
        {
            var isValid = true;

            var abpRelaxMode = string.IsNullOrEmpty(opts.ABPRelaxMode) ||
                string.Equals(opts.ABPRelaxMode, "1", StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals(opts.ABPRelaxMode, "true", StringComparison.InvariantCultureIgnoreCase);

            var fCntUpStart = SetUIntProperty(opts.FCntUpStart);
            var fCntDownStart = SetUIntProperty(opts.FCntDownStart);
            var fCntResetCounter = SetUIntProperty(opts.FCntResetCounter);

            var fCntUpStartRep = SetUIntProperty(fCntUpStartReported);
            var fCntDownStartRep = SetUIntProperty(fCntDownStartReported);
            var fCntResetCounterRep = SetUIntProperty(fCntResetCounterReported);

            // AbpRelaxMode TRUE
            if (abpRelaxMode)
            {
                if (fCntUpStart != null)
                {
                    StatusConsole.WriteLine(MessageType.Warning, $"FCntUpStart {fCntUpStart} is unused if ABPRelaxMode is true or empty.");
                }

                if (fCntDownStart != null)
                {
                    StatusConsole.WriteLine(MessageType.Warning, $"FCntDownStart {fCntDownStart} is unused if ABPRelaxMode is true or empty.");
                }

                if (fCntResetCounter != null)
                {
                    StatusConsole.WriteLine(MessageType.Warning, $"FCntResetCounter {fCntResetCounter} is unused if ABPRelaxMode is true or empty.");
                }
            }

            // AbpRelaxMode FALSE
            else
            {
                if (!ValidateSingleFcnt(fCntUpStart, fCntUpStartRep, fCntResetCounter, fCntResetCounterRep, "FCntUpStart"))
                {
                    isValid = false;
                }

                if (!ValidateSingleFcnt(fCntDownStart, fCntDownStartRep, fCntResetCounter, fCntResetCounterRep, "FCntDownStart"))
                {
                    isValid = false;
                }
            }

            return isValid;
        }

        private static bool ValidateSingleFcnt(uint? fCntStart, uint? fCntStartRep, uint? fCntResetCounter, uint? fCntResetCounterRep, string fCntStartType)
        {
            bool isValid = true;

            if (fCntStart == null)
            {
                // fCntStart not set. Nothing to do.
                return isValid;
            }

            // fCntStartRep not null
            if (fCntStartRep == null)
            {
                StatusConsole.WriteLine(MessageType.Info, $"{fCntStartType} {fCntStart} will be set on gateway.");
                return isValid;
            }

            // fCntStartRep not null, fCntStartRep not null
            if (fCntStart > fCntStartRep)
            {
                StatusConsole.WriteLine(MessageType.Info, $"{fCntStartType} {fCntStart} will be set on gateway.");
                return isValid;
            }

            // fCntStartRep not null, fCntStartRep not null, fCntStart <= fCntStartRep
            if (fCntResetCounter == null)
            {
                StatusConsole.WriteLine(MessageType.Warning, $"{fCntStartType} {fCntStart} will not be set on gateway. Reported {fCntStartType} {fCntStartRep} is larger or equal and FCntResetCounter is not set.");
                return isValid;
            }

            // fCntStartRep not null, fCntStartRep not null, fCntStart <= fCntStartRep,
            // fCntResetCounter not null
            if (fCntResetCounterRep == null)
            {
                StatusConsole.WriteLine(MessageType.Info, $"{fCntStartType} {fCntStart} will be set on gateway.");
                return isValid;
            }

            // fCntStartRep not null, fCntStartRep not null, fCntStart <= fCntStartRep,
            // fCntResetCounter not null, fCntResetCounterRep not null
            if (fCntResetCounter > fCntResetCounterRep)
            {
                StatusConsole.WriteLine(MessageType.Info, $"{fCntStartType} {fCntStart} will be set on gateway.");
                return isValid;
            }

            // fCntStartRep not null, fCntStartRep not null, fCntStart <= fCntStartRep,
            // fCntResetCounter not null, fCntResetCounterRep not null, fCntResetCounter <= fCntResetCounterRep
            else
            {
                StatusConsole.WriteLine(MessageType.Warning, $"{fCntStartType} {fCntStart} will not be set on gateway. Reported {fCntStartType} {fCntStartRep} is larger or equal and FCntResetCounter {fCntResetCounter} is not larger than reported FCntResetCounter {fCntResetCounterRep}.");
                return isValid;
            }
        }
    }
}
