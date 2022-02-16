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
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Newtonsoft.Json;
    using Xunit;

    public static class TestUtils
    {
        public static readonly Region TestRegion = RegionManager.EU868;

        public static LoRaDevice CreateFromSimulatedDevice(
            SimulatedDevice simulatedDevice,
            ILoRaDeviceClientConnectionManager connectionManager,
            DefaultLoRaDataRequestHandler requestHandler = null)
        {
            var result = new LoRaDevice(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DevEui, connectionManager)
            {
                AppEui = simulatedDevice.LoRaDevice.AppEui,
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
                    { TwinProperty.DevAddr, simulatedDevice.DevAddr.ToString() },
                    { TwinProperty.AppSKey, simulatedDevice.AppSKey?.ToString() },
                    { TwinProperty.NwkSKey, simulatedDevice.NwkSKey?.ToString() },
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
                    { TwinProperty.AppEui, simulatedDevice.AppEui?.ToString() },
                    { TwinProperty.AppKey, simulatedDevice.AppKey?.ToString() },
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
                { TwinProperty.DevAddr, simulatedDevice.DevAddr?.ToString() },
                { TwinProperty.AppSKey, simulatedDevice.AppSKey?.ToString() },
                { TwinProperty.NwkSKey, simulatedDevice.NwkSKey?.ToString() },
                { TwinProperty.DevNonce, simulatedDevice.DevNonce.ToString() },
                { TwinProperty.NetId, simulatedDevice.NetId?.ToString() },
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
            var stationConf = new FileInfo(Path.Combine(loRaBasicsStationModulePath, config.IsCorecellBasicStation ? "corecell.station.conf"
                                                                                                                   : "sx1301.station.conf"));

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

        public static RadioMetadata GenerateTestRadioMetadata(
                DataRateIndex dataRate = DataRateIndex.DR2,
                Hertz? frequency = null,
                uint antennaPreference = 1,
                ulong xtime = 100000,
                uint gpstime = 100000,
                double rssi = 2.0,
                float snr = 0.1f) =>
            new RadioMetadata(dataRate, frequency ?? Hertz.Mega(868.3),
                              new RadioMetadataUpInfo(antennaPreference, xtime, gpstime, rssi, snr));

        public static void CheckDRAndFrequencies(WaitableLoRaRequest request, LoRaTools.LoRaPhysical.DownlinkMessage downlinkMessage, bool sendToRx2 = false)
        {
            var euRegion = RegionManager.EU868;

            // If we want to send to force to rx 2, we will set rx1 to default and rx2 settings to proper values.
            // Ensure RX DR
            if (sendToRx2)
                Assert.Null(downlinkMessage.Rx1);
            else
                Assert.Equal(euRegion.GetDownstreamDataRate(request.RadioMetadata.DataRate), downlinkMessage.Rx1.Value.DataRate);

            Assert.Equal(euRegion.GetDownstreamRX2DataRate(null, null, null, NullLogger.Instance), downlinkMessage.Rx2.DataRate);

            // Ensure RX freq
            Assert.True(euRegion.TryGetDownstreamChannelFrequency(request.RadioMetadata.Frequency, request.RadioMetadata.DataRate, null, out var channelFrequency));

            if (sendToRx2)
                Assert.Null(downlinkMessage.Rx1);
            else
                Assert.Equal(channelFrequency, downlinkMessage.Rx1.Value.Frequency);

            Assert.Equal(euRegion.GetDownstreamRX2Freq(null, null, logger: NullLogger.Instance), downlinkMessage.Rx2.Frequency);
        }
    }
}
