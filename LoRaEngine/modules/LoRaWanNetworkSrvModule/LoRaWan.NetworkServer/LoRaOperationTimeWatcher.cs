//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools.Regions;
using System;

namespace LoRaWan.NetworkServer
{
    public partial class MessageProcessor2
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
            public TimeSpan GetTimeToSecondWindow(ILoRaDevice loRaDevice)
            {
                var timePassed = (DateTimeOffset.UtcNow - this.startTime);
                var receiveDelay1 = loRaDevice.ReceiveDelay1 ?? this.loraRegion.receive_delay1;
                var receiveDelay2 = loRaDevice.ReveiveDelay2 ?? this.loraRegion.receive_delay2;
                return TimeSpan.FromSeconds(receiveDelay1 + receiveDelay2).Subtract(timePassed);                
            }

            internal bool InTimeForFirstWindow(ILoRaDevice loraDeviceInfo)
            {
                var timePassed = (DateTimeOffset.UtcNow - this.startTime);
                var receiveDelay1 = loraDeviceInfo.ReceiveDelay1 ?? this.loraRegion.receive_delay1;
                return timePassed.TotalSeconds < receiveDelay1;
            }

            internal bool InTimeForSecondWindow(ILoRaDevice loraDeviceInfo)
            {
                var timePassed = (DateTimeOffset.UtcNow - this.startTime);
                var receiveDelay1 = loraDeviceInfo.ReceiveDelay1 ?? this.loraRegion.receive_delay1;
                var receiveDelay2 = loraDeviceInfo.ReveiveDelay2 ?? this.loraRegion.receive_delay2;
                return timePassed.TotalSeconds < (receiveDelay1 + receiveDelay2);
            }
        }
    }
}