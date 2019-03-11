// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
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

        private readonly ILoRaDeviceClient loRaDeviceClient;

        public string GatewayID { get; set; }

        public string SensorDecoder { get; set; }

        public int? ReceiveDelay1 { get; set; }

        public int? ReceiveDelay2 { get; set; }

        public bool IsABPRelaxedFrameCounter { get; set; }

        public bool Supports32BitFCnt { get; set; }

        public int DataRate { get; set; }

        public int TxPower { get; set; }

        public int NbRep { get; set; }

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

        public LoRaDeviceClassType ClassType
        {
            get => this.classType;
        }

        /// <summary>
        /// Used to synchronize the async save operation to the twins for this particular device.
        /// </summary>
        private readonly SemaphoreSlim syncSave = new SemaphoreSlim(1, 1);
        private readonly object fcntSync = new object();
        private readonly object queueSync = new object();
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
        private ILoRaDataRequestHandler dataRequestHandler;
        LoRaDeviceClassType classType;

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
            this.confirmationResubmitCount = 0;
            this.classType = LoRaDeviceClassType.A;
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
                        Enum.TryParse<DeduplicationMode>(val, out DeduplicationMode mode);
                        this.Deduplication = mode;
                    }

                    if (twin.Properties.Desired.Contains(TwinProperty.RX2DataRate))
                    {
                        this.DesiredRX2DataRate = (ushort)GetTwinPropertyIntValue(twin.Properties.Desired[TwinProperty.RX2DataRate].Value);
                    }

                    if (twin.Properties.Desired.Contains(TwinProperty.RX1DROffset))
                    {
                        this.DesiredRX1DROffset = (ushort)GetTwinPropertyIntValue(twin.Properties.Desired[TwinProperty.RX1DROffset].Value);
                    }

                    if (twin.Properties.Desired.Contains(TwinProperty.ClassType))
                    {
                        if (string.Equals("c", (string)twin.Properties.Desired[TwinProperty.ClassType], StringComparison.InvariantCultureIgnoreCase))
                        {
                            this.classType = LoRaDeviceClassType.C;
                        }
                    }

                    if (twin.Properties.Desired.Contains(TwinProperty.Supports32BitFCnt))
                    {
                        this.Supports32BitFCnt = GetTwinPropertyBoolValue(twin.Properties.Desired[TwinProperty.Supports32BitFCnt].Value);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log(this.DevEUI, $"failed to initialize device from twins: {ex.Message}. Desired: {twin.Properties?.Desired?.ToJson()}. Reported: {twin.Properties?.Reported?.ToJson()}", LogLevel.Debug);
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
                _ = this.SaveFrameCountChangesAsync(toReport, true);
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
                Logger.Log(this.DevEUI, $"Set {fcntPropertyName} from {propertyNameStart} with {newfCnt.Value}, reset: {reset}", LogLevel.Information);
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

        public async Task<bool> SaveFrameCountChangesAsync(bool force = false)
        {
            return await this.SaveFrameCountChangesAsync(new TwinCollection(), force);
        }

        /// <summary>
        /// Saves the frame count changes
        /// </summary>
        /// <remarks>
        /// Changes will be saved only if there are actually changes to be saved
        /// </remarks>
        public async Task<bool> SaveFrameCountChangesAsync(TwinCollection reportedProperties, bool force = false)
        {
            if (reportedProperties == null)
            {
                throw new ArgumentNullException(nameof(reportedProperties));
            }

            try
            {
                // We only ever want a single save operation per device
                // to happen. The save to the twins can be delayed for multiple
                // seconds, subsequent updates should be waiting for this to complete
                // before checking the current state and update again.
                await this.syncSave.WaitAsync();

                if (this.hasFrameCountChanges)
                {
                    var fcntUpDelta = this.FCntUp >= this.LastSavedFCntUp ? this.FCntUp - this.LastSavedFCntUp : this.LastSavedFCntUp - this.FCntUp;
                    var fcntDownDelta = this.FCntDown >= this.LastSavedFCntDown ? this.FCntDown - this.LastSavedFCntDown : this.LastSavedFCntDown - this.FCntDown;

                    if (force || fcntDownDelta >= Constants.MAX_FCNT_UNSAVED_DELTA || fcntUpDelta >= Constants.MAX_FCNT_UNSAVED_DELTA)
                    {
                        uint savedFcntDown;
                        uint savedFcntUp;

                        lock (this.fcntSync)
                        {
                            savedFcntDown = this.FCntDown;
                            savedFcntUp = this.FCntUp;
                        }

                        reportedProperties[TwinProperty.FCntDown] = savedFcntDown;
                        reportedProperties[TwinProperty.FCntUp] = savedFcntUp;

                        var result = await this.loRaDeviceClient.UpdateReportedPropertiesAsync(reportedProperties);
                        if (result)
                        {
                            this.AcceptFrameCountChanges(savedFcntUp, savedFcntDown);
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
            this.AcceptFrameCountChanges(this.fcntUp, this.fcntDown);
        }

        /// <summary>
        /// Accept changes to the frame count
        /// </summary>
        void AcceptFrameCountChanges(uint savedFcntUp, uint savedFcntDown)
        {
            lock (this.fcntSync)
            {
                this.lastSavedFcntUp = savedFcntUp;
                this.lastSavedFcntDown = savedFcntDown;

                this.hasFrameCountChanges = this.fcntDown != this.lastSavedFcntDown || this.fcntUp != this.lastSavedFcntUp;
            }
        }

        /// <summary>
        /// Increments <see cref="FCntDown"/>
        /// </summary>
        public uint IncrementFcntDown(uint value)
        {
            lock (this.fcntSync)
            {
                this.fcntDown += value;
                this.hasFrameCountChanges = true;
                return this.fcntDown;
            }
        }

        /// <summary>
        /// Increments <see cref="FCntDown"/> and the <see cref="LastSavedFCntDown"/>.
        /// Called by device initializer, incrementing by 10 but should not trigger a save
        /// </summary>
        internal uint IncrementFcntDownAndLastSaved(uint value)
        {
            lock (this.fcntSync)
            {
                this.fcntDown += value;
                this.lastSavedFcntDown += value;
                this.hasFrameCountChanges = true;
                return this.fcntDown;
            }
        }

        /// <summary>
        /// Sets a new value for <see cref="FCntDown"/>
        /// </summary>
        public void SetFcntDown(uint newValue)
        {
            lock (this.fcntSync)
            {
                if (newValue != this.fcntDown)
                {
                    this.fcntDown = newValue;
                    this.hasFrameCountChanges = true;
                }
            }
        }

        public void SetFcntUp(uint newValue)
        {
            lock (this.fcntSync)
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
            lock (this.fcntSync)
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
        /// Disconnects device from IoT Hub
        /// </summary>
        public Task<bool> DisconnectAsync() => this.loRaDeviceClient.DisconnectAsync();

        /// <summary>
        /// Indicates whether or not we can resubmit an ack for the confirmation up message
        /// </summary>
        /// <returns><c>true</c>, if resubmit is allowed, <c>false</c> otherwise.</returns>
        /// <param name="payloadFcnt">Payload frame count</param>
        public bool ValidateConfirmResubmit(uint payloadFcnt)
        {
            lock (this.fcntSync)
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

        public Task<bool> RejectCloudToDeviceMessageAsync(Message cloudToDeviceMessage) => this.loRaDeviceClient.RejectAsync(cloudToDeviceMessage);

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

            // Additional Join Property Saved
            if (this.DesiredRX1DROffset != DefaultJoinValues)
            {
                reportedProperties[TwinProperty.RX1DROffset] = this.DesiredRX1DROffset;
            }
            else
            {
                reportedProperties[TwinProperty.RX1DROffset] = null;
            }

            if (this.DesiredRX2DataRate != DefaultJoinValues)
            {
                reportedProperties[TwinProperty.RX2DataRate] = this.DesiredRX2DataRate;
            }
            else
            {
                reportedProperties[TwinProperty.RX2DataRate] = null;
            }

            var devAddrBeforeSave = this.DevAddr;
            var succeeded = await this.loRaDeviceClient.UpdateReportedPropertiesAsync(reportedProperties);

            // Only save if the devAddr remains the same, otherwise ignore the save
            if (succeeded && devAddrBeforeSave == this.DevAddr)
            {
                this.DevAddr = devAddr;
                this.NwkSKey = nwkSKey;
                this.AppSKey = appSKey;
                this.AppNonce = appNonce;
                this.DevNonce = devNonce;
                this.NetID = netID;
                this.ReportedRX1DROffset = this.DesiredRX1DROffset;
                this.ReportedRX2DataRate = this.DesiredRX2DataRate;
                this.ResetFcnt();
                this.AcceptFrameCountChanges();
            }

            return succeeded;
        }

        internal void SetRequestHandler(ILoRaDataRequestHandler dataRequestHandler) => this.dataRequestHandler = dataRequestHandler;

        public void Queue(LoRaRequest request)
        {
            // Access to runningRequest and queuedRequests must be
            // thread safe
            lock (this.queueSync)
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
            lock (this.queueSync)
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
                Logger.Log(this.DevEUI, $"Error processing request: {ex.Message}", LogLevel.Error);
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
            this.loRaDeviceClient?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<bool> TrySaveADRPropertiesAsync()
        {
            var reportedProperties = new TwinCollection();
            reportedProperties[TwinProperty.DataRate] = this.DataRate;
            reportedProperties[TwinProperty.TxPower] = this.TxPower;
            reportedProperties[TwinProperty.NbRep] = this.NbRep;

            if (this.hasFrameCountChanges)
            {
                // combining the save with the framecounter update
                return await this.SaveFrameCountChangesAsync(reportedProperties, true);
            }

            return await this.loRaDeviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }
    }
}