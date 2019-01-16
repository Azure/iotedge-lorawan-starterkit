//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools.Regions;
using System;

namespace LoRaWan.NetworkServer.V2
{
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
        DateTimeOffset startTime;

        // Gets start time
        public DateTimeOffset Start => this.startTime;

        readonly Region loraRegion;

        /// <summary>
        /// Returns the expected time required to package and send message back to package forwarder
        /// 200ms
        /// </summary>
        public static TimeSpan ExpectedTimeToPackageAndSendMessage => TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Returns the expected time required to check for cloud to device messages
        /// 200ms
        /// </summary>
        public static TimeSpan ExpectedTimeToCheckCloudToDeviceMessage => TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Returns the expected time required to check for cloud to device messages and package a response
        /// 400ms
        /// </summary>
        public static TimeSpan ExpectedTimeToCheckCloudToDeviceMessagePackageAndSendMessage => TimeSpan.FromMilliseconds(400);

        public LoRaOperationTimeWatcher(Region loraRegion) : this(loraRegion, DateTimeOffset.UtcNow)
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
        /// <returns></returns>
        public TimeSpan GetRemainingTimeToReceiveSecondWindow(LoRaDevice loRaDevice)
        {
            var timePassed = (DateTimeOffset.UtcNow - this.startTime);
            var receiveDelay2 = loRaDevice.ReceiveDelay2 ?? (int)this.loraRegion.receive_delay2;
            return TimeSpan.FromSeconds(receiveDelay2).Subtract(timePassed);
        }

        bool InTimeForReceiveFirstWindow(LoRaDevice loRaDevice, TimeSpan elapsed)
        {
            var receiveDelay1 = loRaDevice.ReceiveDelay1 ?? (int)this.loraRegion.receive_delay1;
            return elapsed.Add(ExpectedTimeToPackageAndSendMessage).TotalSeconds <= receiveDelay1;
        }

        bool InTimeForReceiveSecondWindow(LoRaDevice loRaDevice, TimeSpan elapsed)
        {
            var receiveDelay2 = loRaDevice.ReceiveDelay2 ?? (int)this.loraRegion.receive_delay2;
            return elapsed.Add(ExpectedTimeToPackageAndSendMessage).TotalSeconds <= (receiveDelay2);
        }

        /// <summary>
        /// Calculate if there is still time to send join accept response
        /// </summary>
        /// <returns></returns>
        public bool InTimeForJoinAccept()
        {
            var elapsed = (DateTimeOffset.UtcNow - this.startTime);
            return elapsed.Add(ExpectedTimeToPackageAndSendMessage) < TimeSpan.FromSeconds(this.loraRegion.join_accept_delay2);
        }

        /// <summary>
        /// Calculates the time remaining to response in first join accept window
        /// </summary>
        /// <returns></returns>
        public TimeSpan GetRemainingTimeToJoinAcceptFirstWindow()
        {
            var elapsed = (DateTimeOffset.UtcNow - this.startTime);
            return TimeSpan.FromSeconds(this.loraRegion.join_accept_delay1) - elapsed;
        }

        /// <summary>
        /// Gets time passed since start
        /// </summary>
        /// <returns></returns>
        internal TimeSpan GetElapsedTime() => (DateTimeOffset.UtcNow - this.startTime);

        /// <summary>
        /// Resolves the receive window to use
        /// </summary>
        /// <param name="loRaDevice"></param>
        /// <returns></returns>
        public int ResolveReceiveWindowToUse(LoRaDevice loRaDevice)
        {
            var elapsed = GetElapsedTime();
            if (!loRaDevice.AlwaysUseSecondWindow && InTimeForReceiveFirstWindow(loRaDevice, elapsed))
            {
                return 1;
            }
            else if (InTimeForReceiveSecondWindow(loRaDevice, elapsed))
            {
                return 2;
            }

            return 0;            
        }


        /// <summary>
        /// Gets the join accept window to be used
        /// </summary>
        /// <param name="loRaDevice"></param>
        /// <returns></returns>
        public int ResolveJoinAcceptWindowToUse(LoRaDevice loRaDevice)
        {
            var elapsed = GetElapsedTime();
            if (InTimeForJoinAcceptFirstWindow(loRaDevice, elapsed))
            {
                return 1;
            }
            else if (InTimeForJoinAcceptSecondWindow(loRaDevice, elapsed))
            {
                return 2;
            }

            return 0;
        }


        bool InTimeForJoinAcceptFirstWindow(LoRaDevice loRaDevice, TimeSpan elapsed)
        {
            return elapsed.Add(ExpectedTimeToPackageAndSendMessage).TotalSeconds <= (double)this.loraRegion.join_accept_delay1;
        }

        bool InTimeForJoinAcceptSecondWindow(LoRaDevice loRaDevice, TimeSpan elapsed)
        {
            return elapsed.Add(ExpectedTimeToPackageAndSendMessage).TotalSeconds <= (double)this.loraRegion.join_accept_delay2;

        }
    }
}