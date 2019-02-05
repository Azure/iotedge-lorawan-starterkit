// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;

    public sealed class LoRaDevice : IDisposable
    {
        /// <summary>
        /// Defines the maximum amount of times an ack resubmit will be sent
        /// </summary>
        internal const int MaxConfirmationResubmitCount = 3;

        public string DevAddr { get; set; }

        // Gets if a device is activated by personalization
        public bool IsABP => string.IsNullOrEmpty(this.AppKey);

        public string DevEUI { get; set; }

        public string AppKey { get; set; }

        public string AppEUI { get; set; }

        public string NwkSKey { get; set; }

        public string AppSKey { get; set; }

        public string AppNonce { get; set; }

        public string DevNonce { get; set; }

        public string NetID { get; set; }

        public bool IsOurDevice { get; set; }

        public string LastConfirmedC2DMessageID { get; set; }

        public int FCntUp => this.fcntUp;

        public int FCntDown => this.fcntDown;

        private readonly ILoRaDeviceClient loRaDeviceClient;

        public string GatewayID { get; set; }

        public string SensorDecoder { get; set; }

        public int? ReceiveDelay1 { get; set; }

        public int? ReceiveDelay2 { get; set; }

        public bool IsABPRelaxedFrameCounter { get; set; }

        int preferredWindow;

        /// <summary>
        /// Gets or sets value indicating the preferred receive window for the device
        /// </summary>
        public int PreferredWindow
        {
            get => this.preferredWindow;

            set
            {
                if (value != 1 && value != 2)
                    throw new ArgumentOutOfRangeException(nameof(this.PreferredWindow), value, $"{nameof(this.PreferredWindow)} must bet 1 or 2");

                this.preferredWindow = value;
            }
        }

        readonly object fcntLock;
        volatile bool hasFrameCountChanges;
        byte confirmationResubmitCount = 0;
        volatile int fcntUp;
        volatile int fcntDown;

        /// <summary>
        ///  Gets or sets a value indicating whether cloud to device messages are enabled for the device
        ///  By default it is enabled. To disable, set the desired property "EnableC2D" to false
        /// </summary>
        public bool DownlinkEnabled { get; set; }

        public LoRaDevice(string devAddr, string devEUI, ILoRaDeviceClient loRaDeviceClient)
        {
            this.DevAddr = devAddr;
            this.DevEUI = devEUI;
            this.loRaDeviceClient = loRaDeviceClient;
            this.DownlinkEnabled = true;
            this.IsABPRelaxedFrameCounter = true;
            this.PreferredWindow = 1;
            this.hasFrameCountChanges = false;
            this.fcntLock = new object();
            this.confirmationResubmitCount = 0;
        }

        /// <summary>
        /// Initializes the device from twin properties
        /// Throws InvalidLoRaDeviceException if the device does contain require properties
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            var twin = await this.loRaDeviceClient.GetTwinAsync();

            if (twin != null)
            {
                // ABP requires the property AppSKey, AppNwkSKey, DevAddr to be present
                if (twin.Properties.Desired.Contains(TwinProperty.AppSKey))
                {
                    // ABP Case
                    this.AppSKey = twin.Properties.Desired[TwinProperty.AppSKey];

                    if (!twin.Properties.Desired.Contains(TwinProperty.NwkSKey))
                        throw new InvalidLoRaDeviceException("Missing NwkSKey for ABP device");

                    if (!twin.Properties.Desired.Contains(TwinProperty.DevAddr))
                        throw new InvalidLoRaDeviceException("Missing DevAddr for ABP device");

                    this.NwkSKey = twin.Properties.Desired[TwinProperty.NwkSKey];
                    this.DevAddr = twin.Properties.Desired[TwinProperty.DevAddr];

                    if (string.IsNullOrEmpty(this.NwkSKey))
                        throw new InvalidLoRaDeviceException("NwkSKey is empty");

                    if (string.IsNullOrEmpty(this.AppSKey))
                        throw new InvalidLoRaDeviceException("AppSKey is empty");

                    if (string.IsNullOrEmpty(this.DevAddr))
                        throw new InvalidLoRaDeviceException("DevAddr is empty");

                    this.IsOurDevice = true;
                }
                else
                {
                    // OTAA
                    if (!twin.Properties.Desired.Contains(TwinProperty.AppKey))
                    {
                        throw new InvalidLoRaDeviceException("Missing AppKey for OTAA device");
                    }

                    this.AppKey = twin.Properties.Desired[TwinProperty.AppKey];

                    if (!twin.Properties.Desired.Contains(TwinProperty.AppEUI))
                    {
                        throw new InvalidLoRaDeviceException("Missing AppEUI for OTAA device");
                    }

                    this.AppEUI = twin.Properties.Desired[TwinProperty.AppEUI];

                    // Check for already joined OTAA device properties
                    if (twin.Properties.Reported.Contains(TwinProperty.DevAddr))
                        this.DevAddr = twin.Properties.Reported[TwinProperty.DevAddr];

                    if (twin.Properties.Reported.Contains(TwinProperty.AppSKey))
                        this.AppSKey = twin.Properties.Reported[TwinProperty.AppSKey];

                    if (twin.Properties.Reported.Contains(TwinProperty.NwkSKey))
                        this.NwkSKey = twin.Properties.Reported[TwinProperty.NwkSKey];

                    if (twin.Properties.Reported.Contains(TwinProperty.NetID))
                        this.NetID = twin.Properties.Reported[TwinProperty.NetID];

                    if (twin.Properties.Reported.Contains(TwinProperty.DevNonce))
                        this.DevNonce = twin.Properties.Reported[TwinProperty.DevNonce];
                }

                if (twin.Properties.Desired.Contains(TwinProperty.GatewayID))
                    this.GatewayID = twin.Properties.Desired[TwinProperty.GatewayID];
                if (twin.Properties.Desired.Contains(TwinProperty.SensorDecoder))
                    this.SensorDecoder = twin.Properties.Desired[TwinProperty.SensorDecoder];
                if (twin.Properties.Reported.Contains(TwinProperty.FCntUp))
                    this.fcntUp = this.GetTwinPropertyIntValue(twin.Properties.Reported[TwinProperty.FCntUp].Value);
                if (twin.Properties.Reported.Contains(TwinProperty.FCntDown))
                    this.fcntDown = this.GetTwinPropertyIntValue(twin.Properties.Reported[TwinProperty.FCntDown].Value);

                if (twin.Properties.Desired.Contains(TwinProperty.DownlinkEnabled))
                {
                    this.DownlinkEnabled = this.GetTwinPropertyBoolValue(twin.Properties.Desired[TwinProperty.DownlinkEnabled].Value);
                }

                if (twin.Properties.Desired.Contains(TwinProperty.PreferredWindow))
                {
                    var preferredWindowTwinValue = this.GetTwinPropertyIntValue(twin.Properties.Desired[TwinProperty.PreferredWindow].Value);
                    if (preferredWindowTwinValue == 2)
                        this.PreferredWindow = preferredWindowTwinValue;
                }

                return true;
            }

            return false;
        }

        int GetTwinPropertyIntValue(dynamic value)
        {
            if (value is string valueString)
            {
                if (int.TryParse(valueString, out var fromString))
                    return fromString;

                return 0;
            }

            if (value is int valueInt)
            {
                return valueInt;
            }

            try
            {
                return System.Convert.ToInt32(value);
            }
            catch
            {
            }

            return 0;
        }

        bool GetTwinPropertyBoolValue(dynamic value)
        {
            if (value is string valueString)
            {
                return
                    string.Equals("true", valueString, StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals("1", valueString, StringComparison.InvariantCultureIgnoreCase);
            }

            if (value is bool valueBool)
            {
                return valueBool;
            }

            if (value is int valueInt)
            {
                return value == 1;
            }

            return true;
        }

        /// <summary>
        /// Saves the frame count changes
        /// </summary>
        /// <remarks>
        /// Changes will be saved only if there are actually changes to be saved
        /// </remarks>
        public async Task<bool> SaveFrameCountChangesAsync()
        {
            if (this.hasFrameCountChanges)
            {
                int savedFcntDown;
                int savedFcntUp;
                lock (this.fcntLock)
                {
                    savedFcntDown = this.FCntDown;
                    savedFcntUp = this.FCntUp;
                }

                var reportedProperties = new TwinCollection();
                reportedProperties[TwinProperty.FCntDown] = savedFcntDown;
                reportedProperties[TwinProperty.FCntUp] = savedFcntUp;

                var result = await this.loRaDeviceClient.UpdateReportedPropertiesAsync(reportedProperties);
                if (result)
                {
                    if (savedFcntUp == this.FCntUp && savedFcntDown == this.FCntDown)
                    {
                        this.AcceptFrameCountChanges();
                    }
                }

                return result;
            }

            return true;
        }

        /// <summary>
        /// Gets a value indicating whether there are pending frame count changes
        /// </summary>
        public bool HasFrameCountChanges => this.hasFrameCountChanges;

        /// <summary>
        /// Accept changes to the frame count
        /// </summary>
        public void AcceptFrameCountChanges()
        {
            lock (this.fcntLock)
            {
                this.hasFrameCountChanges = false;
            }
        }

        /// <summary>
        /// Increments <see cref="FCntDown"/>
        /// </summary>
        public int IncrementFcntDown(int value)
        {
            var newFcntDown = 0;
            lock (this.fcntLock)
            {
                this.fcntDown += value;
                this.hasFrameCountChanges = true;
                newFcntDown = this.fcntDown;
            }

            return newFcntDown;
        }

        /// <summary>
        /// Sets a new value for <see cref="FCntDown"/>
        /// </summary>
        public void SetFcntDown(int newValue)
        {
            lock (this.fcntLock)
            {
                if (newValue != this.fcntDown)
                {
                    this.fcntDown = newValue;
                    this.hasFrameCountChanges = true;
                }
            }
        }

        public void SetFcntUp(int newValue)
        {
            lock (this.fcntLock)
            {
                if (this.fcntUp != newValue)
                {
                    this.fcntUp = newValue;
                    this.confirmationResubmitCount = 0;
                    this.hasFrameCountChanges = true;
                }
            }
        }

        /// <summary>
        /// Optimized way to reset fcntUp and fcntDown to zero with a single lock
        /// </summary>
        internal void ResetFcnt()
        {
            lock (this.fcntLock)
            {
                if (!this.hasFrameCountChanges)
                {
                    this.hasFrameCountChanges = this.fcntDown != 0 || this.fcntUp != 0;
                }

                this.fcntDown = 0;
                this.fcntUp = 0;
                this.confirmationResubmitCount = 0;
            }
        }

        /// <summary>
        /// Indicates whether or not we can resubmit an ack for the confirmation up message
        /// </summary>
        /// <returns><c>true</c>, if resubmit is allowed, <c>false</c> otherwise.</returns>
        /// <param name="payloadFcnt">Payload frame count</param>
        public bool ValidateConfirmResubmit(ushort payloadFcnt)
        {
            lock (this.fcntLock)
            {
                if (this.FCntUp == payloadFcnt)
                {
                    if (this.confirmationResubmitCount < MaxConfirmationResubmitCount)
                    {
                        this.confirmationResubmitCount++;
                        return true;
                    }
                }

                return false;
            }
        }

        public Task<bool> SendEventAsync(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties = null) => this.loRaDeviceClient.SendEventAsync(telemetry, properties);

        public Task<Message> ReceiveCloudToDeviceAsync(TimeSpan timeout) => this.loRaDeviceClient.ReceiveAsync(timeout);

        public Task<bool> CompleteCloudToDeviceMessageAsync(Message cloudToDeviceMessage) => this.loRaDeviceClient.CompleteAsync(cloudToDeviceMessage);

        public Task<bool> AbandonCloudToDeviceMessageAsync(Message cloudToDeviceMessage) => this.loRaDeviceClient.AbandonAsync(cloudToDeviceMessage);

        /// <summary>
        /// Updates device on the server after a join succeeded
        /// </summary>
        internal async Task<bool> UpdateAfterJoinAsync(string devAddr, string nwkSKey, string appSKey, string appNonce, string devNonce, string netID)
        {
            var reportedProperties = new TwinCollection();
            reportedProperties[TwinProperty.AppSKey] = appSKey;
            reportedProperties[TwinProperty.NwkSKey] = nwkSKey;
            reportedProperties[TwinProperty.DevAddr] = devAddr;
            reportedProperties[TwinProperty.FCntDown] = 0;
            reportedProperties[TwinProperty.FCntUp] = 0;
            reportedProperties[TwinProperty.DevEUI] = this.DevEUI;
            reportedProperties[TwinProperty.NetID] = netID;
            reportedProperties[TwinProperty.DevNonce] = devNonce;

            var succeeded = await this.loRaDeviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            if (succeeded)
            {
                this.DevAddr = devAddr;
                this.NwkSKey = nwkSKey;
                this.AppSKey = appSKey;
                this.AppNonce = appNonce;
                this.DevNonce = devNonce;
                this.NetID = netID;
                this.ResetFcnt();
                this.AcceptFrameCountChanges();
            }

            return succeeded;
        }

        public void Dispose()
        {
            this.loRaDeviceClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
