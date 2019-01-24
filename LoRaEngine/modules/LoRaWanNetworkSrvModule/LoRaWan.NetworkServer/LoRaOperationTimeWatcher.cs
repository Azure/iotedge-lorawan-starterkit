// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.Regions;

    /// <summary>
    /// Timer for LoRa operations
    /// </summary>
    /// <remarks>
    /// Calculates:
    /// - first and second receive windows
    /// - first and second join receive windows
    /// </remarks>
    public class LoRaOperationTimeWatcher
    {
        /// <summary>
        /// Gets the expected time required to package and send message back to package forwarder
        /// 300ms
        /// </summary>
        public static TimeSpan ExpectedTimeToPackageAndSendMessage => expectedTimeToPackageAndSendMessage;

        /// <summary>
        /// Gets the minium required time to check for cloud to device messages
        /// 100ms
        /// </summary>
        public static TimeSpan MinimumTimeAvailableToCheckForCloudMessage => minimumTimeAvailableToCheckForCloudMessage;

        /// <summary>
        /// Gets the estimated overhead of calling receive message async (context switch, etc)
        /// 100ms
        /// </summary>
        public static TimeSpan CheckForCloudMessageCallEstimatedOverhead => checkForCloudMessageCallEstimatedOverhead;

        /// <summary>
        /// Gets the expected time required to package and send message back to package forwarder plus the checking for cloud to device message overhead
        /// 400ms
        /// </summary>
        public static TimeSpan ExpectedTimeToPackageAndSendMessageAndCheckForCloudMessageOverhead => expectedTimeToPackageAndSendMessageAndCheckForCloudMessageOverhead;

        /// <summary>
        /// Gets the allocated time to check for additional cloud to device message
        /// 20ms
        /// </summary>
        public static TimeSpan AdditionalCloudToDeviceMessageAvailableTime => additionalCloudToDeviceMessageAvailableTime;

        static TimeSpan minimumTimeAvailableToCheckForCloudMessage;
        static TimeSpan expectedTimeToPackageAndSendMessage;
        static TimeSpan checkForCloudMessageCallEstimatedOverhead;
        static TimeSpan expectedTimeToPackageAndSendMessageAndCheckForCloudMessageOverhead;
        static TimeSpan additionalCloudToDeviceMessageAvailableTime;

        static LoRaOperationTimeWatcher()
        {
            additionalCloudToDeviceMessageAvailableTime = TimeSpan.FromMilliseconds(20);
            expectedTimeToPackageAndSendMessage = TimeSpan.FromMilliseconds(300);
            minimumTimeAvailableToCheckForCloudMessage = TimeSpan.FromMilliseconds(100);
            checkForCloudMessageCallEstimatedOverhead = TimeSpan.FromMilliseconds(100);
            expectedTimeToPackageAndSendMessageAndCheckForCloudMessageOverhead = expectedTimeToPackageAndSendMessage + checkForCloudMessageCallEstimatedOverhead;
        }

        DateTimeOffset startTime;

        // Gets start time
        public DateTimeOffset Start => this.startTime;

        readonly Region loraRegion;

        public LoRaOperationTimeWatcher(Region loraRegion)
            : this(loraRegion, DateTimeOffset.UtcNow)
        {
        }

        public LoRaOperationTimeWatcher(Region loraRegion, DateTimeOffset startTime)
        {
            this.startTime = startTime;
            this.loraRegion = loraRegion;
        }

        /// <summary>
        /// Gets the remaining time to second receive window
        /// </summary>
        public TimeSpan GetRemainingTimeToReceiveSecondWindow(LoRaDevice loRaDevice)
        {
            var elapsed = DateTimeOffset.UtcNow - this.startTime;
            return TimeSpan.FromSeconds(this.GetReceiveWindow2Delay(loRaDevice)).Subtract(elapsed);
        }

        /// <summary>
        /// Gets the remaining time to first receive window
        /// </summary>
        public TimeSpan GetRemainingTimeToReceiveFirstWindow(LoRaDevice loRaDevice)
        {
            var elapsed = DateTimeOffset.UtcNow - this.startTime;
            return TimeSpan.FromSeconds(this.GetReceiveWindow1Delay(loRaDevice)).Subtract(elapsed);
        }

        /// <summary>
        /// Gets the receive window 1 (RX1) delay in seconds
        /// It takes into consideration region and device settings
        /// </summary>
        /// <returns>Integer containing the delay in seconds</returns>
        public int GetReceiveWindow1Delay(LoRaDevice loRaDevice) => loRaDevice.ReceiveDelay1 ?? (int)this.loraRegion.Receive_delay1;

        bool InTimeForReceiveFirstWindow(LoRaDevice loRaDevice, TimeSpan elapsed) => elapsed.Add(ExpectedTimeToPackageAndSendMessage).TotalSeconds <= this.GetReceiveWindow1Delay(loRaDevice);

        /// <summary>
        /// Gets the receive window 2 (RX2) delay in seconds
        /// It takes into consideration region and device settings
        /// </summary>
        /// <returns>Integer containing the delay in seconds</returns>
        public int GetReceiveWindow2Delay(LoRaDevice loRaDevice) => loRaDevice.ReceiveDelay2 ?? (int)this.loraRegion.Receive_delay2;

        bool InTimeForReceiveSecondWindow(LoRaDevice loRaDevice, TimeSpan elapsed) => elapsed.Add(ExpectedTimeToPackageAndSendMessage).TotalSeconds <= this.GetReceiveWindow2Delay(loRaDevice);

        /// <summary>
        /// Calculate if there is still time to send join accept response
        /// </summary>
        public bool InTimeForJoinAccept()
        {
            var elapsed = DateTimeOffset.UtcNow - this.startTime;
            return elapsed.Add(ExpectedTimeToPackageAndSendMessage) < TimeSpan.FromSeconds(this.loraRegion.Join_accept_delay2);
        }

        /// <summary>
        /// Calculates the time remaining to response in first join accept window
        /// </summary>
        public TimeSpan GetRemainingTimeToJoinAcceptFirstWindow()
        {
            var elapsed = DateTimeOffset.UtcNow - this.startTime;
            return TimeSpan.FromSeconds(this.loraRegion.Join_accept_delay1) - elapsed;
        }

        /// <summary>
        /// Gets time passed since start
        /// </summary>
        internal TimeSpan GetElapsedTime() => DateTimeOffset.UtcNow - this.startTime;

        /// <summary>
        /// Resolves the receive window to use
        /// </summary>
        public int ResolveReceiveWindowToUse(LoRaDevice loRaDevice)
        {
            var elapsed = this.GetElapsedTime();
            if (loRaDevice.PreferredWindow == 1 && this.InTimeForReceiveFirstWindow(loRaDevice, elapsed))
            {
                return 1;
            }
            else if (this.InTimeForReceiveSecondWindow(loRaDevice, elapsed))
            {
                return 2;
            }

            return 0;
        }

        /// <summary>
        /// Gets the join accept window to be used
        /// </summary>
        public int ResolveJoinAcceptWindowToUse(LoRaDevice loRaDevice)
        {
            var elapsed = this.GetElapsedTime();
            if (this.InTimeForJoinAcceptFirstWindow(loRaDevice, elapsed))
            {
                return 1;
            }
            else if (this.InTimeForJoinAcceptSecondWindow(loRaDevice, elapsed))
            {
                return 2;
            }

            return 0;
        }

        bool InTimeForJoinAcceptFirstWindow(LoRaDevice loRaDevice, TimeSpan elapsed)
        {
            return elapsed.Add(ExpectedTimeToPackageAndSendMessage).TotalSeconds <= (double)this.loraRegion.Join_accept_delay1;
        }

        bool InTimeForJoinAcceptSecondWindow(LoRaDevice loRaDevice, TimeSpan elapsed)
        {
            return elapsed.Add(ExpectedTimeToPackageAndSendMessage).TotalSeconds <= (double)this.loraRegion.Join_accept_delay2;
        }

        /// <summary>
        /// Gets the available time to check for cloud to device messages
        /// It takes into consideration available time and <see cref="LoRaDevice.PreferredWindow"/>
        /// </summary>
        /// <returns><see cref="TimeSpan.Zero"/> if there is no enough time or a positive <see cref="TimeSpan"/> value</returns>
        public TimeSpan GetAvailableTimeToCheckCloudToDeviceMessage(LoRaDevice loRaDevice)
        {
            var elapsed = this.GetElapsedTime();
            if (loRaDevice.PreferredWindow == 1)
            {
                var availableTimeForFirstWindow = TimeSpan.FromSeconds(this.GetReceiveWindow1Delay(loRaDevice)).Subtract(elapsed.Add(ExpectedTimeToPackageAndSendMessageAndCheckForCloudMessageOverhead));
                if (availableTimeForFirstWindow > TimeSpan.Zero)
                {
                    return availableTimeForFirstWindow;
                }
            }

            // 2nd window
            var availableTimeForSecondWindow = TimeSpan.FromSeconds(this.GetReceiveWindow2Delay(loRaDevice)).Subtract(elapsed.Add(ExpectedTimeToPackageAndSendMessageAndCheckForCloudMessageOverhead));
            if (availableTimeForSecondWindow > TimeSpan.Zero)
            {
                return availableTimeForSecondWindow;
            }

            return TimeSpan.Zero;
        }
    }
}