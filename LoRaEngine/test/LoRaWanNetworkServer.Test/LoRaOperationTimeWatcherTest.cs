// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using Xunit;

    // Tests of the LoRa Operation time watcher
    public class LoRaOperationTimeWatcherTest
    {
        [Fact]
        public void After_One_Second_Join_First_Window_Should_Be_Greater_Than_3sec()
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(1)));
            var actual = target.GetRemainingTimeToJoinAcceptFirstWindow();
            Assert.InRange(actual.TotalMilliseconds, 3500, 5000);
        }

        [Fact]
        public void After_5_Seconds_Join_First_Window_Should_Be_Negative()
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(5)));
            var actual = target.GetRemainingTimeToJoinAcceptFirstWindow();
            Assert.True(actual.TotalMilliseconds < 0, $"First window is over, value should be negative");
        }

        [Fact]
        public void After_3_Seconds_Should_Be_In_Time_For_Join()
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(3)));
            Assert.True(target.InTimeForJoinAccept());
        }

        [Fact]
        public void After_5_Seconds_Should_Be_In_Time_For_Join()
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(5)));
            Assert.True(target.InTimeForJoinAccept());
        }

        [Fact]
        public void After_6_Seconds_Should_Not_Be_In_Time_For_Join()
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(6)));
            Assert.False(target.InTimeForJoinAccept());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(950)]
        [InlineData(1690)]
        public void When_In_Time_For_First_Window_But_Device_Preferes_Seconds_Should_Resolve_Window_2(int delayInMs)
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            var loRaDevice = new LoRaDevice("31312", "312321321", null)
            {
                PreferredWindow = Constants.RECEIVE_WINDOW_2,
            };

            Assert.Equal(Constants.RECEIVE_WINDOW_2, target.ResolveReceiveWindowToUse(loRaDevice));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(690)]
        public void When_In_Time_For_First_Window_Should_Resolve_Window_1(int delayInMs)
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            var loRaDevice = new LoRaDevice("31312", "312321321", null);

            Assert.Equal(Constants.RECEIVE_WINDOW_1, target.ResolveReceiveWindowToUse(loRaDevice));
        }

        [Theory]
        [InlineData(1000)]
        [InlineData(1001)]
        [InlineData(1690)]
        public void When_In_Time_For_Second_Window_Should_Resolve_Window_2(int delayInMs)
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            var loRaDevice = new LoRaDevice("31312", "312321321", null);

            Assert.Equal(Constants.RECEIVE_WINDOW_2, target.ResolveReceiveWindowToUse(loRaDevice));
        }

        [Theory]
        [InlineData(1801)]
        [InlineData(2000)]
        [InlineData(4000)]
        public void When_Missed_Both_Windows_Should_Resolve_Window_0(int delayInMs)
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            var loRaDevice = new LoRaDevice("31312", "312321321", null);

            Assert.Equal(Constants.INVALID_RECEIVE_WINDOW, target.ResolveReceiveWindowToUse(loRaDevice));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        [InlineData(2000)]
        [InlineData(3000)]
        [InlineData(4000)]
        [InlineData(4690)]
        public void When_In_Time_For_Join_Accept_First_Window_Should_Resolve_Window_1(int delayInMs)
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            var loRaDevice = new LoRaDevice("31312", "312321321", null);

            Assert.Equal(1, target.ResolveJoinAcceptWindowToUse(loRaDevice));
        }

        [Theory]
        [InlineData(4900)]
        [InlineData(5000)]
        [InlineData(5690)]
        public void When_In_Time_For_Join_Accept_Second_Window_Should_Resolve_Window_2(int delayInMs)
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            var loRaDevice = new LoRaDevice("31312", "312321321", null);

            Assert.Equal(2, target.ResolveJoinAcceptWindowToUse(loRaDevice));
        }

        [Theory]
        [InlineData(6000)]
        [InlineData(7000)]
        [InlineData(8000)]
        public void When_Out_Of_Time_For_Join_Accept_Second_Window_Should_Resolve_Window_0(int delayInMs)
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            var loRaDevice = new LoRaDevice("31312", "312321321", null);

            Assert.Equal(0, target.ResolveJoinAcceptWindowToUse(loRaDevice));
        }

        [Theory]
        [InlineData(0, 400, 600)]
        [InlineData(100, 300, 500)]
        [InlineData(400, 0, 200)]
        // RX2 results
        [InlineData(601, 800, 1400)]
        [InlineData(800, 700, 800)]
        [InlineData(1000, 400, 600)]
        // No time even for RX2 results
        [InlineData(1601, 0, 0)]
        [InlineData(1700, 0, 0)]
        [InlineData(3000, 0, 0)]
        public void When_Device_PreferredWindow1_In_Time_For_First_Window_Should_Get_Check_C2D_Avaible_Time_Correctly(int delayInMs, int expectedMinMs, int expectedMaxMs)
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            var loRaDevice = new LoRaDevice("1111", "2222", null);

            // Will be around 1000 - delay - 400
            Assert.InRange(target.GetAvailableTimeToCheckCloudToDeviceMessage(loRaDevice), TimeSpan.FromMilliseconds(expectedMinMs), TimeSpan.FromMilliseconds(expectedMaxMs));
        }

        [Theory]
        [InlineData(0, 1400, 1600)]
        [InlineData(100, 1300, 1500)]
        [InlineData(400, 1000, 1200)]
        [InlineData(600, 800, 1000)]
        [InlineData(800, 600, 800)]
        [InlineData(1000, 400, 600)]
        // No time even for RX2 results
        [InlineData(1601, 0, 0)]
        [InlineData(1700, 0, 0)]
        [InlineData(3000, 0, 0)]
        public void When_Device_PreferredWindow2_In_Time_For_First_Window_Should_Get_Check_C2D_Avaible_Time_Correctly(int delayInMs, int expectedMinMs, int expectedMaxMs)
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            var loRaDevice = new LoRaDevice("1111", "2222", null)
            {
                PreferredWindow = 2,
            };

            // Will be around 1000 - delay - 400
            Assert.InRange(target.GetAvailableTimeToCheckCloudToDeviceMessage(loRaDevice), TimeSpan.FromMilliseconds(expectedMinMs), TimeSpan.FromMilliseconds(expectedMaxMs));
        }

        [Theory]
        // Preferred Window 1
        [InlineData(1581, 1)]
        [InlineData(1600, 1)]
        [InlineData(2000, 1)]
        // Preferred Window 2
        [InlineData(1581, 2)]
        [InlineData(1600, 2)]
        [InlineData(2000, 2)]
        public void When_Device_Out_Of_Time_For_C2D_Receive_Should_Return_TimeSpan_Zero(int delayInMs, int devicePreferredReceiveWindow)
        {
            var target = new LoRaOperationTimeWatcher(RegionManager.EU868, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            var loRaDevice = new LoRaDevice("1111", "2222", null)
            {
                PreferredWindow = devicePreferredReceiveWindow,
            };

            Assert.Equal(TimeSpan.Zero, target.GetAvailableTimeToCheckCloudToDeviceMessage(loRaDevice));
        }
    }
}