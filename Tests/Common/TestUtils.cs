// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using LoRaTools.CommonAPI;
    using LoRaWan.NetworkServer;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public static class TestUtils
    {
        public static LoRaDevice CreateFromSimulatedDevice(
            SimulatedDevice simulatedDevice,
            ILoRaDeviceClientConnectionManager connectionManager,
            DefaultLoRaDataRequestHandler requestHandler = null)
        {
            var result = new LoRaDevice(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, connectionManager)
            {
                AppEUI = simulatedDevice.LoRaDevice.AppEUI,
                AppKey = simulatedDevice.LoRaDevice.AppKey,
                SensorDecoder = simulatedDevice.LoRaDevice.SensorDecoder,
                AppSKey = simulatedDevice.LoRaDevice.AppSKey,
                NwkSKey = simulatedDevice.LoRaDevice.NwkSKey,
                GatewayID = simulatedDevice.LoRaDevice.GatewayID,
                IsOurDevice = true,
                ClassType = (simulatedDevice.ClassType is 'C' or 'c') ? LoRaDeviceClassType.C : LoRaDeviceClassType.A,
            };

            result.SetFcntDown(simulatedDevice.FrmCntDown);
            result.SetFcntUp(simulatedDevice.FrmCntUp);
            result.AcceptFrameCountChanges();

            if (requestHandler != null)
                result.SetRequestHandler(requestHandler);

            return result;
        }

        public static Twin CreateTwin(Dictionary<string, object> desired = null, Dictionary<string, object> reported = null)
        {
            var twin = new Twin();
            if (desired != null)
            {
                foreach (var kv in desired)
                {
                    twin.Properties.Desired[kv.Key] = kv.Value;
                }
            }

            if (reported != null)
            {
                foreach (var kv in reported)
                {
                    twin.Properties.Reported[kv.Key] = kv.Value;
                }
            }

            return twin;
        }

        public static Twin CreateABPTwin(
            this SimulatedDevice simulatedDevice,
            Dictionary<string, object> desiredProperties = null,
            Dictionary<string, object> reportedProperties = null)
        {
            var finalDesiredProperties = new Dictionary<string, object>
                {
                    { TwinProperty.DevAddr, simulatedDevice.DevAddr },
                    { TwinProperty.AppSKey, simulatedDevice.AppSKey },
                    { TwinProperty.NwkSKey, simulatedDevice.NwkSKey },
                    { TwinProperty.GatewayID, simulatedDevice.LoRaDevice.GatewayID },
                    { TwinProperty.SensorDecoder, simulatedDevice.LoRaDevice.SensorDecoder },
                    { TwinProperty.ClassType, simulatedDevice.ClassType.ToString() },
                };

            if (desiredProperties != null)
            {
                foreach (var kv in desiredProperties)
                {
                    finalDesiredProperties[kv.Key] = kv.Value;
                }
            }

            var finalReportedProperties = new Dictionary<string, object>
            {
                { TwinProperty.FCntDown, simulatedDevice.FrmCntDown },
                { TwinProperty.FCntUp, simulatedDevice.FrmCntUp }
            };

            if (reportedProperties != null)
            {
                foreach (var kv in reportedProperties)
                {
                    finalReportedProperties[kv.Key] = kv.Value;
                }
            }

            return CreateTwin(desired: finalDesiredProperties, reported: finalReportedProperties);
        }

        public static Twin CreateOTAATwin(
            this SimulatedDevice simulatedDevice,
            Dictionary<string, object> desiredProperties = null,
            Dictionary<string, object> reportedProperties = null)
        {
            var finalDesiredProperties = new Dictionary<string, object>
                {
                    { TwinProperty.AppEUI, simulatedDevice.AppEUI },
                    { TwinProperty.AppKey, simulatedDevice.AppKey },
                    { TwinProperty.GatewayID, simulatedDevice.LoRaDevice.GatewayID },
                    { TwinProperty.SensorDecoder, simulatedDevice.LoRaDevice.SensorDecoder },
                    { TwinProperty.ClassType, simulatedDevice.ClassType.ToString() },
                };

            if (desiredProperties != null)
            {
                foreach (var kv in desiredProperties)
                {
                    finalDesiredProperties[kv.Key] = kv.Value;
                }
            }

            var finalReportedProperties = new Dictionary<string, object>
            {
                { TwinProperty.DevAddr, simulatedDevice.DevAddr },
                { TwinProperty.AppSKey, simulatedDevice.AppSKey },
                { TwinProperty.NwkSKey, simulatedDevice.NwkSKey },
                { TwinProperty.DevNonce, simulatedDevice.DevNonce.ToString() },
                { TwinProperty.NetID, simulatedDevice.NetId },
                { TwinProperty.FCntDown, simulatedDevice.FrmCntDown },
                { TwinProperty.FCntUp, simulatedDevice.FrmCntUp }
            };

            if (reportedProperties != null)
            {
                foreach (var kv in reportedProperties)
                {
                    finalReportedProperties[kv.Key] = kv.Value;
                }
            }

            return CreateTwin(desired: finalDesiredProperties, reported: finalReportedProperties);
        }

        /// <summary>
        /// Helper to create a <see cref="Message"/> from a <see cref="LoRaCloudToDeviceMessage"/>.
        /// </summary>
        public static Message CreateMessage(this LoRaCloudToDeviceMessage loRaMessage)
        {
            var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(loRaMessage)))
            {
                ContentType = "application/json",
            };

            return message;
        }

        public static string GeneratePayload(string allowedChars, int length)
        {
            var random = new Random();

            var chars = new char[length];
            var setLength = allowedChars.Length;

            for (var i = 0; i < length; ++i)
            {
                chars[i] = allowedChars[random.Next(setLength)];
            }

            return new string(chars, 0, length);
        }

        /// <summary>
        /// Gets the time span delay necessary to make the request be answered in 2nd receive window.
        /// </summary>
        public static TimeSpan GetStartTimeOffsetForSecondWindow()
        {
            return TimeSpan.FromMilliseconds(1000 - LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage.TotalMilliseconds + 1);
        }

        public static DirectoryInfo TryGetSolutionDirectoryInfo()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !directory.GetFiles("LoRaEngine.sln").Any())
            {
                directory = directory.Parent;
            }
            return directory;
        }

        public static void KillBasicsStation(TestConfiguration config, string temporaryDirectoryName, out string logFilePath)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));

            logFilePath = Path.GetTempFileName();
            var connection = config.RemoteConcentratorConnection;
            var sshPrivateKeyPath = config.SshPrivateKeyPath;

            Environment.SetEnvironmentVariable("BS_TEMP_LOG_FILE", logFilePath);
            // Following environment variable is needed if following bash commands are executed within WSL
            Environment.SetEnvironmentVariable("WSLENV", "BS_TEMP_LOG_FILE/up");

            // following command is:
            // - copying basic station logs to logFilePath
            // - killing basic station
            // - removing temporary folder
            using var killProcess = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"scp -i {sshPrivateKeyPath} -rp {connection}:/tmp/{temporaryDirectoryName}/logs.txt $BS_TEMP_LOG_FILE && ssh -i {sshPrivateKeyPath} -f {connection} 'kill -9 \\$(pgrep -f station.std)' && ssh -i {sshPrivateKeyPath} -f {connection} 'rm -rf /tmp/{temporaryDirectoryName}'\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            _ = killProcess.Start();
            if (!killProcess.WaitForExit(10_000))
            {
                throw new TimeoutException("Killing the process via SSH took more than expected.");
            }
        }

        public static void StartBasicsStation(TestConfiguration config, Dictionary<string, string> scriptParameters, out string randomDirectoryName)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));

            var connection = config.RemoteConcentratorConnection;
            var sshPrivateKeyPath = config.SshPrivateKeyPath;
            var rootPath = TryGetSolutionDirectoryInfo().FullName;
            var loRaBasicsStationModulePath = Path.Combine(rootPath, "LoRaEngine", "modules", "LoRaBasicsStationModule");
            var scriptName = "start_basicsstation.sh";
            var startScript = new FileInfo(Path.Combine(loRaBasicsStationModulePath, scriptName));
            var helperFunctions = new FileInfo(Path.Combine(loRaBasicsStationModulePath, "helper-functions.sh"));
            var stationConf = new FileInfo(Path.Combine(loRaBasicsStationModulePath, "station.conf"));

            // Copying needed files in a local temporary path
            var localTempPath = Path.GetTempPath();
            randomDirectoryName = Path.GetRandomFileName();
            var tempDirectory = Path.Combine(localTempPath, randomDirectoryName);
            _ = Directory.CreateDirectory(tempDirectory);
            File.Copy(config.BasicStationExecutablePath, Path.Combine(tempDirectory, "station.std"));
            File.Copy(startScript.FullName, Path.Combine(tempDirectory, scriptName));
            File.Copy(helperFunctions.FullName, Path.Combine(tempDirectory, "helper-functions.sh"));
            File.Copy(stationConf.FullName, Path.Combine(tempDirectory, "station.conf"));

            Environment.SetEnvironmentVariable("BS_TEMP_DIRECTORY", tempDirectory);
            // Following environment variable is needed if following bash commands are executed within WSL
            Environment.SetEnvironmentVariable("WSLENV", "BS_TEMP_DIRECTORY/up");

            if (!config.RunningInCI)
            {
                using var folderCreateProcess = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = $"-c \"scp -i {sshPrivateKeyPath} -rp $BS_TEMP_DIRECTORY {connection}:/tmp\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                _ = folderCreateProcess.Start();
                if (!folderCreateProcess.WaitForExit(30_000))
                {
                    throw new TimeoutException("Creating and copying over SSH the needed files took more than expected.");
                }
                tempDirectory = $"/tmp/{randomDirectoryName}";
            }

            var expandedParams = string.Join(" ", scriptParameters.Select(p => $"{p.Key}={p.Value}"));
            using var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"ssh -i {sshPrivateKeyPath} -f {connection} 'cd {tempDirectory} && chmod +x ./{scriptName} && STATION_PATH={tempDirectory} {expandedParams} ./{scriptName} &>{tempDirectory}/logs.txt &'\"",
                    RedirectStandardOutput = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            _ = process.Start();
            // Waiting for ssh command to be executed, not for the basic station to be exiting
            if (!process.WaitForExit(30_000))
            {
                throw new TimeoutException("Executing the SSH command took more than expected.");
            }
        }
    }
}
