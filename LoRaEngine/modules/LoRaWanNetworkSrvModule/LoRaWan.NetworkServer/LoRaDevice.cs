// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Regions;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public sealed class LoRaDevice : IDisposable, ILoRaDeviceRequestQueue
    {
        /// <summary>
        /// Defines the maximum amount of times an ack resubmit will be sent
        /// </summary>
        internal const int MaxConfirmationResubmitCount = 3;

        /// <summary>
        /// The default values for RX1DROffset, RX2DR, RXDelay
        /// </summary>
        internal const ushort DefaultJoinValues = 0;

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

        public uint FCntUp => this.fcntUp;

        /// <summary>
        /// Gets the last saved value for <see cref="FCntUp"/>
        /// </summary>
        public uint LastSavedFCntUp => this.lastSavedFcntUp;

        public uint FCntDown => this.fcntDown;

        /// <summary>
        /// Gets the last saved value for <see cref="FCntDown"/>
        /// </summary>
        public uint LastSavedFCntDown => this.lastSavedFcntDown;

        public string GatewayID { get; set; }

        public string SensorDecoder { get; set; }

        public int? ReceiveDelay1 { get; set; }

        public int? ReceiveDelay2 { get; set; }

        public bool IsABPRelaxedFrameCounter { get; set; }

        public bool Supports32BitFCnt { get; set; }

        ChangeTrackingProperty<int> dataRate = new ChangeTrackingProperty<int>(TwinProperty.DataRate);

        public int DataRate => this.dataRate.Get();

        ChangeTrackingProperty<int> txPower = new ChangeTrackingProperty<int>(TwinProperty.TxPower);

        ILoRaDeviceClientConnectionManager connectionManager;

        public int TxPower => this.txPower.Get();

        ChangeTrackingProperty<int> nbRep = new ChangeTrackingProperty<int>(TwinProperty.NbRep);

        public int NbRep => this.nbRep.Get();

        public DeduplicationMode Deduplication { get; set; }

        int preferredWindow;

        /// <summary>
        /// Gets or sets value indicating the preferred receive window for the device
        /// </summary>
        public int PreferredWindow
        {
            get => this.preferredWindow;

            set
            {
                if (value != Constants.RECEIVE_WINDOW_1 && value != Constants.RECEIVE_WINDOW_2)
                    throw new ArgumentOutOfRangeException(nameof(this.PreferredWindow), value, $"{nameof(this.PreferredWindow)} must bet 1 or 2");

                this.preferredWindow = value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="LoRaDeviceClassType"/>
        /// </summary>
        public LoRaDeviceClassType ClassType { get; set; }

        ChangeTrackingProperty<LoRaRegionType> region = new ChangeTrackingProperty<LoRaRegionType>(TwinProperty.Region, LoRaRegionType.NotSet);

        /// <summary>
        /// Gets or sets the <see cref="LoRaTools.Regions.LoRaRegionType"/> of the device
        /// Relevant only for <see cref="LoRaDeviceClassType.C"/>
        /// </summary>
        public LoRaRegionType LoRaRegion
        {
            get { return this.region.Get(); }
            set { this.region.Set(value); }
        }

        ChangeTrackingProperty<string> preferredGatewayID = new ChangeTrackingProperty<string>(TwinProperty.PreferredGatewayID, string.Empty);

        /// <summary>
        /// Gets the device preferred gateway identifier
        /// Relevant only for <see cref="LoRaDeviceClassType.C"/>
        /// </summary>
        public string PreferredGatewayID => this.preferredGatewayID.Get();

        /// <summary>
        /// Used to synchronize the async save operation to the twins for this particular device.
        /// </summary>
        private readonly SemaphoreSlim syncSave = new SemaphoreSlim(1, 1);
        private readonly object processingSyncLock = new object();
        private readonly Queue<LoRaRequest> queuedRequests = new Queue<LoRaRequest>();

        public ushort DesiredRX2DataRate { get; set; }

        public ushort DesiredRX1DROffset { get; set; }

        public ushort ReportedRX2DataRate { get; set; }

        public ushort ReportedRX1DROffset { get; set; }

        private volatile bool hasFrameCountChanges;

        private byte confirmationResubmitCount = 0;
        private volatile uint fcntUp;
        private volatile uint fcntDown;
        private volatile uint lastSavedFcntUp;
        private volatile uint lastSavedFcntDown;
        private volatile LoRaRequest runningRequest;

        public ushort ReportedRXDelay { get; set; }

        public ushort DesiredRXDelay { get; set; }

        private ILoRaDataRequestHandler dataRequestHandler;

        private volatile int deviceClientConnectionActivityCounter;

        /// <summary>
        ///  Gets or sets a value indicating whether cloud to device messages are enabled for the device
        ///  By default it is enabled. To disable, set the desired property "EnableC2D" to false
        /// </summary>
        public bool DownlinkEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the timeout value in seconds for the device client connection
        /// </summary>
        public int KeepAliveTimeout { get; set; }

        public LoRaDevice(string devAddr, string devEUI, ILoRaDeviceClientConnectionManager connectionManager)
        {
            this.DevAddr = devAddr;
            this.DevEUI = devEUI;
            this.connectionManager = connectionManager;
            this.DownlinkEnabled = true;
            this.IsABPRelaxedFrameCounter = true;
            this.PreferredWindow = 1;
            this.hasFrameCountChanges = false;
            this.confirmationResubmitCount = 0;
            this.queuedRequests = new Queue<LoRaRequest>();
            this.ClassType = LoRaDeviceClassType.A;
        }

        /// <summary>
        /// Initializes the device from twin properties
        /// Throws InvalidLoRaDeviceException if the device does contain require properties
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            var twin = await this.connectionManager.Get(this)?.GetTwinAsync();

            if (twin != null)
            {
                try
                {
                    // ABP requires the property AppSKey, AppNwkSKey, DevAddr to be present
                    if (twin.Properties.Desired.Contains(TwinProperty.AppSKey))
                    {
                        // ABP Case
                        this.AppSKey = twin.Properties.Desired[TwinProperty.AppSKey].Value as string;

                        if (!twin.Properties.Desired.Contains(TwinProperty.NwkSKey))
                            throw new InvalidLoRaDeviceException("Missing NwkSKey for ABP device");

                        if (!twin.Properties.Desired.Contains(TwinProperty.DevAddr))
                            throw new InvalidLoRaDeviceException("Missing DevAddr for ABP device");

                        this.NwkSKey = twin.Properties.Desired[TwinProperty.NwkSKey].Value as string;
                        this.DevAddr = twin.Properties.Desired[TwinProperty.DevAddr].Value as string;

                        if (string.IsNullOrEmpty(this.NwkSKey))
                            throw new InvalidLoRaDeviceException("NwkSKey is empty");

                        if (string.IsNullOrEmpty(this.AppSKey))
                            throw new InvalidLoRaDeviceException("AppSKey is empty");

                        if (string.IsNullOrEmpty(this.DevAddr))
                            throw new InvalidLoRaDeviceException("DevAddr is empty");

                        if (twin.Properties.Desired.Contains(TwinProperty.ABPRelaxMode))
                        {
                            this.IsABPRelaxedFrameCounter = GetTwinPropertyBoolValue(twin.Properties.Desired[TwinProperty.ABPRelaxMode].Value);
                        }

                        this.IsOurDevice = true;
                    }
                    else
                    {
                        // OTAA
                        if (!twin.Properties.Desired.Contains(TwinProperty.AppKey))
                        {
                            throw new InvalidLoRaDeviceException("Missing AppKey for OTAA device");
                        }

                        this.AppKey = twin.Properties.Desired[TwinProperty.AppKey].Value as string;

                        if (!twin.Properties.Desired.Contains(TwinProperty.AppEUI))
                        {
                            throw new InvalidLoRaDeviceException("Missing AppEUI for OTAA device");
                        }

                        this.AppEUI = twin.Properties.Desired[TwinProperty.AppEUI].Value as string;

                        // Check for already joined OTAA device properties
                        if (twin.Properties.Reported.Contains(TwinProperty.DevAddr))
                            this.DevAddr = twin.Properties.Reported[TwinProperty.DevAddr].Value as string;

                        if (twin.Properties.Reported.Contains(TwinProperty.AppSKey))
                            this.AppSKey = twin.Properties.Reported[TwinProperty.AppSKey].Value as string;

                        if (twin.Properties.Reported.Contains(TwinProperty.NwkSKey))
                            this.NwkSKey = twin.Properties.Reported[TwinProperty.NwkSKey].Value as string;

                        if (twin.Properties.Reported.Contains(TwinProperty.NetID))
                            this.NetID = twin.Properties.Reported[TwinProperty.NetID].Value as string;

                        if (twin.Properties.Reported.Contains(TwinProperty.DevNonce))
                            this.DevNonce = twin.Properties.Reported[TwinProperty.DevNonce].Value as string;

                        // Currently the RX2DR, RX1DROffset and RXDelay are only implemented as part of OTAA
                        if (twin.Properties.Desired.Contains(TwinProperty.RX2DataRate))
                        {
                            this.DesiredRX2DataRate = (ushort)GetTwinPropertyIntValue(twin.Properties.Desired[TwinProperty.RX2DataRate].Value);
                        }

                        if (twin.Properties.Desired.Contains(TwinProperty.RX1DROffset))
                        {
                            this.DesiredRX1DROffset = (ushort)GetTwinPropertyIntValue(twin.Properties.Desired[TwinProperty.RX1DROffset].Value);
                        }

                        if (twin.Properties.Desired.Contains(TwinProperty.RXDelay))
                        {
                            this.DesiredRXDelay = (ushort)GetTwinPropertyIntValue(twin.Properties.Desired[TwinProperty.RXDelay].Value);
                        }

                        if (twin.Properties.Reported.Contains(TwinProperty.RX2DataRate))
                        {
                            this.ReportedRX2DataRate = (ushort)GetTwinPropertyIntValue(twin.Properties.Reported[TwinProperty.RX2DataRate].Value);
                        }

                        if (twin.Properties.Reported.Contains(TwinProperty.RX1DROffset))
                        {
                            this.ReportedRX1DROffset = (ushort)GetTwinPropertyIntValue(twin.Properties.Reported[TwinProperty.RX1DROffset].Value);
                        }

                        if (twin.Properties.Reported.Contains(TwinProperty.RXDelay))
                        {
                            this.ReportedRXDelay = (ushort)GetTwinPropertyIntValue(twin.Properties.Reported[TwinProperty.RXDelay].Value);
                        }
                    }

                    if (twin.Properties.Desired.Contains(TwinProperty.GatewayID))
                        this.GatewayID = twin.Properties.Desired[TwinProperty.GatewayID].Value as string;
                    if (twin.Properties.Desired.Contains(TwinProperty.SensorDecoder))
                        this.SensorDecoder = twin.Properties.Desired[TwinProperty.SensorDecoder].Value as string;

                    this.InitializeFrameCounters(twin);

                    if (twin.Properties.Desired.Contains(TwinProperty.DownlinkEnabled))
                    {
                        this.DownlinkEnabled = GetTwinPropertyBoolValue(twin.Properties.Desired[TwinProperty.DownlinkEnabled].Value);
                    }

                    if (twin.Properties.Desired.Contains(TwinProperty.PreferredWindow))
                    {
                        var preferredWindowTwinValue = GetTwinPropertyIntValue(twin.Properties.Desired[TwinProperty.PreferredWindow].Value);
                        if (preferredWindowTwinValue == Constants.RECEIVE_WINDOW_2)
                            this.PreferredWindow = preferredWindowTwinValue;
                    }

                    if (twin.Properties.Desired.Contains(TwinProperty.Deduplication))
                    {
                        var val = twin.Properties.Desired[TwinProperty.Deduplication].Value as string;
                        Enum.TryParse<DeduplicationMode>(val, true, out DeduplicationMode mode);
                        this.Deduplication = mode;
                    }

                    if (twin.Properties.Desired.Contains(TwinProperty.ClassType))
                    {
                        if (string.Equals("c", (string)twin.Properties.Desired[TwinProperty.ClassType], StringComparison.InvariantCultureIgnoreCase))
                        {
                            this.ClassType = LoRaDeviceClassType.C;
                        }
                    }

                    if (twin.Properties.Reported.Contains(TwinProperty.PreferredGatewayID))
                    {
                        this.preferredGatewayID = new ChangeTrackingProperty<string>(TwinProperty.PreferredGatewayID, twin.Properties.Reported[TwinProperty.PreferredGatewayID].Value as string);
                    }

                    if (twin.Properties.Reported.Contains(TwinProperty.Region))
                    {
                        var regionValue = twin.Properties.Reported[TwinProperty.Region].Value as string;
                        if (Enum.TryParse<LoRaRegionType>(regionValue, true, out var loRaRegion))
                        {
                            if (Enum.IsDefined(typeof(LoRaRegionType), loRaRegion))
                            {
                                this.region = new ChangeTrackingProperty<LoRaRegionType>(TwinProperty.Region, loRaRegion);
                            }
                        }

                        if (this.LoRaRegion == LoRaRegionType.NotSet)
                        {
                            Logger.Log(this.DevEUI, $"invalid region value: {regionValue}", LogLevel.Error);
                        }
                    }

                    if (twin.Properties.Desired.Contains(TwinProperty.Supports32BitFCnt))
                    {
                        this.Supports32BitFCnt = GetTwinPropertyBoolValue(twin.Properties.Desired[TwinProperty.Supports32BitFCnt].Value);
                    }

                    if (twin.Properties.Desired.Contains(TwinProperty.KeepAliveTimeout))
                    {
                        var value = GetTwinPropertyIntValue(twin.Properties.Desired[TwinProperty.KeepAliveTimeout].Value);
                        if (value > 0)
                        {
                            this.KeepAliveTimeout = Math.Max(value, Constants.MIN_KEEP_ALIVE_TIMEOUT);
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log(this.DevEUI, $"failed to initialize device from twin: {ex.Message}", LogLevel.Debug);
                    throw;
                }
            }

            return false;
        }

        private void InitializeFrameCounters(Twin twin)
        {
            var toReport = new TwinCollection();

            bool reset = false;
            // check if there is a reset we need to process
            if (twin.Properties.Desired.Contains(TwinProperty.FCntResetCounter))
            {
                var resetDesired = GetTwinPropertyIntValue(twin.Properties.Desired[TwinProperty.FCntResetCounter].Value);
                int? resetReported = null;
                if (twin.Properties.Reported.Contains(TwinProperty.FCntResetCounter))
                {
                    resetReported = GetTwinPropertyIntValue(twin.Properties.Reported[TwinProperty.FCntResetCounter].Value);
                }

                reset = !resetReported.HasValue || resetReported.Value < resetDesired;
                if (reset)
                {
                    toReport[TwinProperty.FCntResetCounter] = resetDesired;
                }
            }

            // up
            var fcnt = this.InitializeFcnt(twin, reset, TwinProperty.FCntUpStart, TwinProperty.FCntUp, toReport);
            if (fcnt.HasValue)
            {
                this.fcntUp = fcnt.Value;
                this.lastSavedFcntUp = this.fcntUp;
            }

            // down
            fcnt = this.InitializeFcnt(twin, reset, TwinProperty.FCntDownStart, TwinProperty.FCntDown, toReport);
            if (fcnt.HasValue)
            {
                this.fcntDown = fcnt.Value;
                this.lastSavedFcntDown = this.fcntDown;
            }

            if (toReport.Count > 0)
            {
                _ = this.SaveChangesAsync(toReport, true);
            }
        }

        private uint? InitializeFcnt(Twin twin, bool reset, string propertyNameStart, string fcntPropertyName, TwinCollection toReport)
        {
            var desired = twin.Properties.Desired;
            var reported = twin.Properties.Reported;
            uint? newfCnt = null;

            var frameCounterStartDesired = GetUintFromTwin(desired, propertyNameStart);
            var frameCounterStartReported = GetUintFromTwin(reported, propertyNameStart);
            if (frameCounterStartDesired.HasValue && (reset || frameCounterStartReported != frameCounterStartDesired))
            {
                // force this counter in the start desired
                newfCnt = frameCounterStartDesired;
                toReport = toReport ?? new TwinCollection();
                toReport[propertyNameStart] = newfCnt.Value;
                this.hasFrameCountChanges = true;
                Logger.Log(this.DevEUI, $"set {fcntPropertyName} from {propertyNameStart} with {newfCnt.Value}, reset: {reset}", LogLevel.Debug);
            }
            else
            {
                newfCnt = GetUintFromTwin(reported, fcntPropertyName);
            }

            return newfCnt;
        }

        static uint? GetUintFromTwin(TwinCollection collection, string propertyName)
        {
            if (!collection.Contains(propertyName))
            {
                return null;
            }

            return GetTwinPropertyUIntValue(collection[propertyName].Value);
        }

        static int GetTwinPropertyIntValue(dynamic value)
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

        static uint GetTwinPropertyUIntValue(dynamic value)
        {
            if (value is string valueString)
            {
                if (uint.TryParse(valueString, out var fromString))
                    return fromString;

                return 0;
            }

            if (value is uint valueUint)
            {
                return value;
            }

            try
            {
                return System.Convert.ToUInt32(value);
            }
            catch
            {
            }

            return 0;
        }

        static bool GetTwinPropertyBoolValue(dynamic value)
        {
            if (value is string valueString)
            {
                valueString = valueString.Trim();

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
        /// Saves device changes in reported twin properties
        /// It will only save if required. Frame counters are only saved if the difference since last value is equal or greater than <see cref="Constants.MAX_FCNT_UNSAVED_DELTA"/>
        /// </summary>
        /// <param name="reportedProperties">Pre populate reported properties</param>
        /// <param name="force">Indicates if changes should be saved even if the difference between last saved and current frame counter are less than <see cref="Constants.MAX_FCNT_UNSAVED_DELTA"/></param>
        public async Task<bool> SaveChangesAsync(TwinCollection reportedProperties = null, bool force = false)
        {
            try
            {
                // We only ever want a single save operation per device
                // to happen. The save to the twins can be delayed for multiple
                // seconds, subsequent updates should be waiting for this to complete
                // before checking the current state and update again.
                await this.syncSave.WaitAsync();

                if (reportedProperties == null)
                {
                    reportedProperties = new TwinCollection();
                }

                var savedProperties = new List<IChangeTrackingProperty>();
                foreach (var prop in this.GetTrackableProperties())
                {
                    if (prop.IsDirty())
                    {
                        reportedProperties[prop.PropertyName] = prop.Value;
                        savedProperties.Add(prop);
                    }
                }

                var fcntUpDelta = this.FCntUp >= this.LastSavedFCntUp ? this.FCntUp - this.LastSavedFCntUp : this.LastSavedFCntUp - this.FCntUp;
                var fcntDownDelta = this.FCntDown >= this.LastSavedFCntDown ? this.FCntDown - this.LastSavedFCntDown : this.LastSavedFCntDown - this.FCntDown;

                if (reportedProperties.Count > 0 ||
                    fcntDownDelta >= Constants.MAX_FCNT_UNSAVED_DELTA ||
                    fcntUpDelta >= Constants.MAX_FCNT_UNSAVED_DELTA ||
                    (this.hasFrameCountChanges && force))
                {
                    var savedFcntDown = this.FCntDown;
                    var savedFcntUp = this.FCntUp;

                    reportedProperties[TwinProperty.FCntDown] = savedFcntDown;
                    reportedProperties[TwinProperty.FCntUp] = savedFcntUp;

                    // For class C devices this might be the only moment the connection is established
                    using (var deviceClientActivityScope = this.BeginDeviceClientConnectionActivity())
                    {
                        if (deviceClientActivityScope == null)
                        {
                            // Logging as information because the real error was logged as error
                            Logger.Log(this.DevEUI, "failed to save twin, could not reconnect", LogLevel.Debug);
                            return false;
                        }

                        var result = await this.connectionManager.Get(this).UpdateReportedPropertiesAsync(reportedProperties);
                        if (result)
                        {
                            this.InternalAcceptFrameCountChanges(savedFcntUp, savedFcntDown);

                            for (int i = 0; i < savedProperties.Count; i++)
                                savedProperties[i].AcceptChanges();
                        }
                        else
                        {
                            for (int i = 0; i < savedProperties.Count; i++)
                                savedProperties[i].Rollback();
                        }

                        return result;
                    }
                }

                return true;
            }
            finally
            {
                this.syncSave.Release();
            }
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
            this.syncSave.Wait();
            try
            {
                this.InternalAcceptFrameCountChanges(this.fcntUp, this.fcntDown);
            }
            finally
            {
                this.syncSave.Release();
            }
        }

        /// <summary>
        /// Accept changes to the frame count
        /// This method is not protected by locks
        /// </summary>
        void InternalAcceptFrameCountChanges(uint savedFcntUp, uint savedFcntDown)
        {
            this.lastSavedFcntUp = savedFcntUp;
            this.lastSavedFcntDown = savedFcntDown;

            this.hasFrameCountChanges = this.fcntDown != this.lastSavedFcntDown || this.fcntUp != this.lastSavedFcntUp;
        }

        /// <summary>
        /// Increments <see cref="FCntDown"/>
        /// </summary>
        public uint IncrementFcntDown(uint value)
        {
            this.syncSave.Wait();
            try
            {
                this.fcntDown += value;
                this.hasFrameCountChanges = true;
                return this.fcntDown;
            }
            finally
            {
                this.syncSave.Release();
            }
        }

        /// <summary>
        /// Sets a new value for <see cref="FCntDown"/>
        /// </summary>
        public void SetFcntDown(uint newValue)
        {
            this.syncSave.Wait();
            try
            {
                if (newValue != this.fcntDown)
                {
                    this.fcntDown = newValue;
                    this.hasFrameCountChanges = true;
                }
            }
            finally
            {
                this.syncSave.Release();
            }
        }

        public void SetFcntUp(uint newValue)
        {
            this.syncSave.Wait();
            try
            {
                if (this.fcntUp != newValue)
                {
                    this.fcntUp = newValue;
                    this.confirmationResubmitCount = 0;
                    this.hasFrameCountChanges = true;
                }
            }
            finally
            {
                this.syncSave.Release();
            }
        }

        /// <summary>
        /// Optimized way to reset fcntUp and fcntDown to zero with a single lock
        /// </summary>
        internal void ResetFcnt()
        {
            this.syncSave.Wait();
            try
            {
                if (this.hasFrameCountChanges)
                {
                    // if there are changes, reset them if the last saved was 0, 0
                    this.hasFrameCountChanges = this.lastSavedFcntDown != 0 || this.lastSavedFcntUp != 0;
                }
                else
                {
                    // if there aren't changes, reset if fcnt was not 0, 0
                    this.hasFrameCountChanges = this.fcntDown != 0 || this.fcntUp != 0;
                }

                this.fcntDown = 0;
                this.fcntUp = 0;
                this.confirmationResubmitCount = 0;
            }
            finally
            {
                this.syncSave.Release();
            }
        }

        /// <summary>
        /// Ensures that the device is connected. Calls the connection manager that keeps track of device connection lifetime.
        /// </summary>
        internal IDisposable BeginDeviceClientConnectionActivity()
        {
            // Most devices won't have a connection timeout
            // In that case check without lock and return a cached disposable
            if (this.KeepAliveTimeout == 0)
            {
                return NullDisposable.Instance;
            }

            lock (this.processingSyncLock)
            {
                if (this.connectionManager.EnsureConnected(this))
                {
                    this.deviceClientConnectionActivityCounter++;
                    return new ScopedDeviceClientConnection(this);
                }
            }

            return null;
        }

        /// <summary>
        /// Indicates whether or not we can resubmit an ack for the confirmation up message
        /// </summary>
        /// <returns><c>true</c>, if resubmit is allowed, <c>false</c> otherwise.</returns>
        /// <param name="payloadFcnt">Payload frame count</param>
        public bool ValidateConfirmResubmit(uint payloadFcnt)
        {
            this.syncSave.Wait();
            try
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
            finally
            {
                this.syncSave.Release();
            }
        }

        public Task<bool> SendEventAsync(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties = null) => this.connectionManager.Get(this).SendEventAsync(telemetry, properties);

        public Task<Message> ReceiveCloudToDeviceAsync(TimeSpan timeout) => this.connectionManager.Get(this).ReceiveAsync(timeout);

        public Task<bool> CompleteCloudToDeviceMessageAsync(Message cloudToDeviceMessage) => this.connectionManager.Get(this).CompleteAsync(cloudToDeviceMessage);

        public Task<bool> AbandonCloudToDeviceMessageAsync(Message cloudToDeviceMessage) => this.connectionManager.Get(this).AbandonAsync(cloudToDeviceMessage);

        public Task<bool> RejectCloudToDeviceMessageAsync(Message cloudToDeviceMessage) => this.connectionManager.Get(this).RejectAsync(cloudToDeviceMessage);

        /// <summary>
        /// Updates device on the server after a join succeeded
        /// </summary>
        internal async Task<bool> UpdateAfterJoinAsync(LoRaDeviceJoinUpdateProperties updateProperties)
        {
            var reportedProperties = new TwinCollection();
            reportedProperties[TwinProperty.AppSKey] = updateProperties.AppSKey;
            reportedProperties[TwinProperty.NwkSKey] = updateProperties.NwkSKey;
            reportedProperties[TwinProperty.DevAddr] = updateProperties.DevAddr;
            reportedProperties[TwinProperty.FCntDown] = 0;
            reportedProperties[TwinProperty.FCntUp] = 0;
            reportedProperties[TwinProperty.DevEUI] = this.DevEUI;
            reportedProperties[TwinProperty.NetID] = updateProperties.NetID;
            reportedProperties[TwinProperty.DevNonce] = updateProperties.DevNonce;

            if (updateProperties.SaveRegion)
            {
                this.region.Set(updateProperties.Region);
                if (this.region.IsDirty())
                {
                    reportedProperties[this.region.PropertyName] = updateProperties.Region.ToString();
                }
            }

            if (RegionManager.TryTranslateToRegion(updateProperties.Region, out var currentRegion))
            {
                // Additional Join Property Saved
                if (this.DesiredRX1DROffset != DefaultJoinValues && currentRegion.IsValidRX1DROffset(this.DesiredRX1DROffset))
                {
                    reportedProperties[TwinProperty.RX1DROffset] = this.DesiredRX1DROffset;
                }
                else
                {
                    reportedProperties[TwinProperty.RX1DROffset] = null;
                }

                if (this.DesiredRX2DataRate != DefaultJoinValues && currentRegion.RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(this.DesiredRX2DataRate))
                {
                    reportedProperties[TwinProperty.RX2DataRate] = this.DesiredRX2DataRate;
                }
                else
                {
                    reportedProperties[TwinProperty.RX2DataRate] = null;
                }

                if (this.DesiredRXDelay != DefaultJoinValues && currentRegion.IsValidRXDelay(this.DesiredRXDelay))
                {
                    reportedProperties[TwinProperty.RXDelay] = this.DesiredRXDelay;
                }
                else
                {
                    reportedProperties[TwinProperty.RXDelay] = null;
                }
            }
            else
            {
                Logger.Log(this.DevEUI, "the region provided in the device twin is not a valid value", LogLevel.Error);
            }

            if (updateProperties.SavePreferredGateway)
            {
                if (string.IsNullOrEmpty(this.GatewayID))
                {
                    this.preferredGatewayID.Set(updateProperties.PreferredGatewayID);
                }
                else if (!string.IsNullOrEmpty(this.preferredGatewayID.Get()))
                {
                    this.preferredGatewayID.Set(null);
                }

                if (this.preferredGatewayID.IsDirty())
                {
                    reportedProperties[this.preferredGatewayID.PropertyName] = updateProperties.PreferredGatewayID;
                }
            }

            using (var activityScope = this.BeginDeviceClientConnectionActivity())
            {
                if (activityScope == null)
                {
                    // Logging as information because the real error was logged as error
                    Logger.Log(this.DevEUI, "failed to update twin after join, could not reconnect", LogLevel.Debug);
                    return false;
                }

                var devAddrBeforeSave = this.DevAddr;
                var succeeded = await this.connectionManager.Get(this).UpdateReportedPropertiesAsync(reportedProperties);

                // Only save if the devAddr remains the same, otherwise ignore the save
                if (succeeded && devAddrBeforeSave == this.DevAddr)
                {
                    this.DevAddr = updateProperties.DevAddr;
                    this.NwkSKey = updateProperties.NwkSKey;
                    this.AppSKey = updateProperties.AppSKey;
                    this.AppNonce = updateProperties.AppNonce;
                    this.DevNonce = updateProperties.DevNonce;
                    this.NetID = updateProperties.NetID;

                    if (currentRegion.IsValidRX1DROffset(this.DesiredRX1DROffset))
                    {
                        this.ReportedRX1DROffset = this.DesiredRX1DROffset;
                    }
                    else
                    {
                        Logger.Log(this.DevEUI, "the provided RX1DROffset is not valid", LogLevel.Error);
                    }

                    if (currentRegion.RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(this.DesiredRX2DataRate))
                    {
                        this.ReportedRX2DataRate = this.DesiredRX2DataRate;
                    }
                    else
                    {
                        Logger.Log(this.DevEUI, "the provided RX2DataRate is not valid", LogLevel.Error);
                    }

                    if (currentRegion.IsValidRXDelay(this.DesiredRXDelay))
                    {
                        this.ReportedRXDelay = this.DesiredRXDelay;
                    }
                    else
                    {
                        Logger.Log(this.DevEUI, "the provided RXDelay is not valid", LogLevel.Error);
                    }

                    this.region.AcceptChanges();
                    this.preferredGatewayID.AcceptChanges();

                    this.ResetFcnt();
                    this.InternalAcceptFrameCountChanges(this.fcntUp, this.fcntDown);
                }
                else
                {
                    this.region.Rollback();
                    this.preferredGatewayID.Rollback();
                }

                return succeeded;
            }
        }

        internal void SetRequestHandler(ILoRaDataRequestHandler dataRequestHandler) => this.dataRequestHandler = dataRequestHandler;

        public void Queue(LoRaRequest request)
        {
            // Access to runningRequest and queuedRequests must be
            // thread safe
            lock (this.processingSyncLock)
            {
                if (this.runningRequest == null)
                {
                    this.runningRequest = request;

                    // Ensure that this is schedule in a new thread, releasing the lock asap
                    Task.Run(() => { _ = this.RunAndQueueNext(request); });
                }
                else
                {
                    this.queuedRequests.Enqueue(request);
                }
            }
        }

        private void ProcessNext()
        {
            // Access to runningRequest and queuedRequests must be
            // thread safe
            lock (this.processingSyncLock)
            {
                this.runningRequest = null;
                if (this.queuedRequests.TryDequeue(out var nextRequest))
                {
                    this.runningRequest = nextRequest;
                    // Ensure that this is schedule in a new thread, releasing the lock asap
                    Task.Run(() => { _ = this.RunAndQueueNext(nextRequest); });
                }
            }
        }

        internal bool ValidateMic(LoRaPayload payload)
        {
            var payloadData = payload as LoRaPayloadData;

            var adjusted32bit = payloadData != null ? this.Get32BitAjustedFcntIfSupported(payloadData) : null;
            var ret = payload.CheckMic(this.NwkSKey, adjusted32bit);
            if (!ret && payloadData != null && this.CanRolloverToNext16Bits(payloadData.GetFcnt()))
            {
                payloadData.Reset32BitBlockInfo();
                // if the upper 16bits changed on the client, it can be that we can't decrypt
                ret = payloadData.CheckMic(this.NwkSKey, this.Get32BitAjustedFcntIfSupported(payloadData, true));
                if (ret)
                {
                    // this is an indication that the lower 16 bits rolled over on the client
                    // we adjust the server to the new higher 16bits and keep the lower 16bits
                    this.Rollover32BitFCnt();
                }
            }

            return ret;
        }

        internal uint? Get32BitAjustedFcntIfSupported(LoRaPayloadData payload, bool rollHi = false)
        {
            if (!this.Supports32BitFCnt || payload == null)
                return null;

            var serverValue = this.FCntUp;

            if (rollHi)
            {
                serverValue = IncrementUpper16bit(serverValue);
            }

            return LoRaPayloadData.InferUpper32BitsForClientFcnt(payload.GetFcnt(), serverValue);
        }

        internal bool CanRolloverToNext16Bits(ushort payloadFcntUp)
        {
            if (!this.Supports32BitFCnt)
            {
                // rollovers are only supported on 32bit devices
                return false;
            }

            var delta = payloadFcntUp + (ushort.MaxValue - (ushort)this.fcntUp);
            return delta <= Constants.MAX_FCNT_GAP;
        }

        internal void Rollover32BitFCnt()
        {
            this.SetFcntUp(IncrementUpper16bit(this.fcntUp));
        }

        private static uint IncrementUpper16bit(uint val)
        {
            val |= 0x0000FFFF;
            return ++val;
        }

        async Task RunAndQueueNext(LoRaRequest request)
        {
            LoRaDeviceRequestProcessResult result = null;
            Exception processingError = null;

            try
            {
                result = await this.dataRequestHandler.ProcessRequestAsync(request, this);
            }
            catch (Exception ex)
            {
                Logger.Log(this.DevEUI, $"error processing request: {ex.Message}", LogLevel.Error);
                processingError = ex;
            }
            finally
            {
                this.ProcessNext();
            }

            if (processingError != null)
            {
                request.NotifyFailed(this, processingError);
            }
            else if (result.FailedReason.HasValue)
            {
                request.NotifyFailed(this, result.FailedReason.Value);
            }
            else
            {
                request.NotifySucceeded(this, result?.DownlinkMessage);
            }
        }

        public void Dispose()
        {
            this.connectionManager.Release(this);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Updates the ADR properties of device
        /// </summary>
        public void UpdatedADRProperties(int dataRate, int txPower, int nbRep)
        {
            this.dataRate.Set(dataRate);
            this.txPower.Set(txPower);
            this.nbRep.Set(nbRep);
        }

        /// <summary>
        /// Gets the properties that are trackable
        /// </summary>
        IEnumerable<IChangeTrackingProperty> GetTrackableProperties()
        {
            yield return this.preferredGatewayID;
            yield return this.region;
            yield return this.dataRate;
            yield return this.txPower;
            yield return this.nbRep;
        }

        internal void UpdatePreferredGatewayID(string value, bool acceptChanges)
        {
            this.preferredGatewayID.Set(value);
            if (acceptChanges)
                this.preferredGatewayID.AcceptChanges();
        }

        internal void UpdateRegion(LoRaRegionType value, bool acceptChanges)
        {
            this.region.Set(value);
            if (acceptChanges)
                this.region.AcceptChanges();
        }

        /// <summary>
        /// Accepts changes in properties, for testing only!
        /// </summary>
        internal void InternalAcceptChanges()
        {
            foreach (var prop in this.GetTrackableProperties())
            {
                prop.AcceptChanges();
            }
        }

        /// <summary>
        /// Ends a device client connection activity
        /// Called by <see cref="ScopedDeviceClientConnection.Dispose"/>
        /// </summary>
        private void EndDeviceClientConnectionActivity()
        {
            lock (this.processingSyncLock)
            {
                if (this.deviceClientConnectionActivityCounter == 0)
                {
                    throw new InvalidOperationException("Cannot decrement count, already at zero");
                }

                this.deviceClientConnectionActivityCounter--;
            }
        }

        /// <summary>
        /// Disconnects the <see cref="ILoRaDeviceClient"/> if there is no pending activity
        /// </summary>
        internal bool TryDisconnect()
        {
            lock (this.processingSyncLock)
            {
                if (this.deviceClientConnectionActivityCounter == 0)
                {
                    this.connectionManager.Get(this).Disconnect();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Defines a <see cref="ILoRaDeviceClient"/> scope.
        /// While a connection activity is open the connection cannot be closed
        /// </summary>
        private class ScopedDeviceClientConnection : IDisposable
        {
            private readonly LoRaDevice loRaDevice;

            internal ScopedDeviceClientConnection(LoRaDevice loRaDevice)
            {
                if (loRaDevice.KeepAliveTimeout == 0)
                {
                    throw new InvalidOperationException("Scoped device client connection can be created only for devices with a connection timeout");
                }

                this.loRaDevice = loRaDevice;
            }

            public void Dispose()
            {
                this.loRaDevice.EndDeviceClientConnectionActivity();
            }
        }
    }
}