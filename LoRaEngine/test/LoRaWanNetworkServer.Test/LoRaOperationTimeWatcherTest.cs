//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaWan.NetworkServer;
using LoRaWan.NetworkServer.V2;
using LoRaWan.Shared;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.NetworkServer.Test
{
    // Tests of the LoRa Operation time watcher
    public class LoRaOperationTimeWatcherTest
    {
        [Fact]
        public async Task After_One_Second_Join_First_Window_Should_Be_Greater_Than_3sec()
        {
            var target = new LoRaOperationTimeWatcher(LoRaTools.Regions.Region.EU);
            await Task.Delay(TimeSpan.FromSeconds(1));
            var actual = target.GetTimeToJoinAcceptFirstWindow();
            Assert.InRange(actual.TotalMilliseconds, 3500, 5000);
        }

        [Fact]
        public async Task After_5_Seconds_Join_First_Window_Should_Be_Negative()
        {
            var target = new LoRaOperationTimeWatcher(LoRaTools.Regions.Region.EU);
            await Task.Delay(TimeSpan.FromSeconds(5));
            var actual = target.GetTimeToJoinAcceptFirstWindow();
            Assert.True(actual.TotalMilliseconds < 0, $"First window is over, value should be negative");
        }

        [Fact]
        public async Task After_3_Seconds_Should_Be_In_Time_For_Join()
        {
            var target = new LoRaOperationTimeWatcher(LoRaTools.Regions.Region.EU);
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.True(target.InTimeForJoinAccept());
        }

        [Fact]
        public async Task After_5_Seconds_Should_Be_In_Time_For_Join()
        {
            var target = new LoRaOperationTimeWatcher(LoRaTools.Regions.Region.EU);
            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.True(target.InTimeForJoinAccept());
        }

        [Fact]
        public async Task After_6_Seconds_Should_Not_Be_In_Time_For_Join()
        {
            var target = new LoRaOperationTimeWatcher(LoRaTools.Regions.Region.EU);
            await Task.Delay(TimeSpan.FromSeconds(6));
            Assert.False(target.InTimeForJoinAccept());
        }
    }
}