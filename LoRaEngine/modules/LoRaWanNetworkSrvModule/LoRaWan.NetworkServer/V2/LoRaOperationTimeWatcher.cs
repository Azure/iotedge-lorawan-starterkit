//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools.Regions;
using System;

namespace LoRaWan.NetworkServer.V2
{
    public class LoRaOperationTimeWatcher
    {
        DateTimeOffset startTime;
        Region loraRegion;


        public static TimeSpan ExpectedTimeToPackageAndSendMessage => TimeSpan.FromMilliseconds(200);

        public static TimeSpan ExpectedTimeToCheckCloudToDeviceMessage => TimeSpan.FromMilliseconds(200);

        public LoRaOperationTimeWatcher(Region loraRegion) : this(loraRegion, DateTimeOffset.UtcNow)
        {
        }

        public LoRaOperationTimeWatcher(Region loraRegion, DateTimeOffset startTime)
        {
            this.startTime = startTime;
            this.loraRegion = loraRegion;
        }

        /// <summary>
        /// Gets the time to second window
        /// </summary>
        /// <returns></returns>
        public TimeSpan GetTimeToSecondWindow(LoRaDevice loRaDevice)
        {
            var timePassed = (DateTimeOffset.UtcNow - this.startTime);
            var receiveDelay1 = loRaDevice.ReceiveDelay1 ?? (int)this.loraRegion.receive_delay1;
            var receiveDelay2 = loRaDevice.ReceiveDelay2 ?? (int)this.loraRegion.receive_delay2;
            return TimeSpan.FromSeconds(receiveDelay1 + receiveDelay2).Subtract(timePassed);
        }

        internal bool InTimeForFirstWindow(LoRaDevice loraDeviceInfo)
        {
            var timePassed = (DateTimeOffset.UtcNow - this.startTime);
            var receiveDelay1 = loraDeviceInfo.ReceiveDelay1 ?? (int)this.loraRegion.receive_delay1;
            return timePassed.TotalSeconds < receiveDelay1;
        }

        internal bool InTimeForSecondWindow(LoRaDevice loraDeviceInfo)
        {
            var timePassed = (DateTimeOffset.UtcNow - this.startTime);
            var receiveDelay1 = loraDeviceInfo.ReceiveDelay1 ?? (int)this.loraRegion.receive_delay1;
            var receiveDelay2 = loraDeviceInfo.ReceiveDelay2 ?? (int)this.loraRegion.receive_delay2;
            return timePassed.TotalSeconds < (receiveDelay1 + receiveDelay2);
        }

        /// <summary>
        /// Calculate if there is still time to send join accept response
        /// </summary>
        /// <returns></returns>
        internal bool InTimeForJoinAccept()
        {
            var timePassed = (DateTimeOffset.UtcNow - this.startTime);
            return timePassed < TimeSpan.FromSeconds(this.loraRegion.join_accept_delay2);
        }

        /// <summary>
        /// Calculates the time remaining to response in first join accept window
        /// </summary>
        /// <returns></returns>
        internal TimeSpan GetTimeToJoinAcceptFirstWindow()
        {
            var timePassed = (DateTimeOffset.UtcNow - this.startTime);
            return timePassed - TimeSpan.FromSeconds(this.loraRegion.join_accept_delay1);
        }

        /// <summary>
        /// Gets time passed since start
        /// </summary>
        /// <returns></returns>
        internal TimeSpan GetElapsedTime() => (DateTimeOffset.UtcNow - this.startTime);
    }
}