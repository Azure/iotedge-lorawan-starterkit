//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;

namespace LoRaWan.NetworkServer.V2
{

    public class LoRaDevice
    {
        public string DevAddr { get; set; }

        //public bool IsABP { get; set; }
        public bool IsABP => string.IsNullOrEmpty(this.AppEUI);

        public string DevEUI { get; set; }
        public string AppKey { get; set; }
        public string AppEUI { get; set; }
        public string NwkSKey { get; set; }
        public string AppSKey { get; set; }
        public string AppNonce { get; set; }
        public string DevNonce { get; set; }
        public string NetId { get; set; }
        public bool IsOurDevice = false;
        public bool IsJoinValid = false;
        
        int fcntUp;
        public int FCntUp => this.fcntUp;

        int fcntDown;
        public int FCntDown => this.fcntDown;
        private readonly ILoRaDeviceClient loRaDeviceClient;

        public string GatewayID { get; set; }
        public string SensorDecoder { get; set; }
        public int? ReceiveDelay1 { get; internal set; }
        public int? ReceiveDelay2 { get; internal set; }
        public bool IsABPRelaxedFrameCounter { get; set; } = true;
        public bool AlwaysUseSecondWindow { get; set; } = false;

        int hasFrameCountChanges;

        public LoRaDevice(string devAddr, string devEUI, ILoRaDeviceClient loRaDeviceClient)
        {
            DevAddr = devAddr;
            DevEUI = devEUI;
            this.loRaDeviceClient = loRaDeviceClient;
            this.hasFrameCountChanges = 0;
        }

        /// <summary>
        /// Initializes the device
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {
            var twin = await this.loRaDeviceClient.GetTwinAsync();

            if (twin != null)
            {                
                if (twin.Properties.Desired.Contains(TwinProperty.AppSKey))
                {
                    //ABP Case
                    this.AppSKey = twin.Properties.Desired[TwinProperty.AppSKey];
                    this.NwkSKey = twin.Properties.Desired[TwinProperty.NwkSKey];
                    this.DevAddr = twin.Properties.Desired[TwinProperty.DevAddr];
                }                
                else if (twin.Properties.Reported.Contains(TwinProperty.AppSKey))
                {
                    //OTAA Case
                    this.AppSKey = twin.Properties.Reported[TwinProperty.AppSKey];
                    this.NwkSKey = twin.Properties.Reported[TwinProperty.NwkSKey];
                    this.DevAddr = twin.Properties.Reported[TwinProperty.DevAddr];
                    this.DevNonce = twin.Properties.Reported[TwinProperty.DevNonce];
                }                

                if (twin.Properties.Desired.Contains(TwinProperty.AppEUI))
                    this.AppEUI = twin.Properties.Desired[TwinProperty.AppEUI];
                if (twin.Properties.Desired.Contains(TwinProperty.AppKey))
                    this.AppKey = twin.Properties.Desired[TwinProperty.AppKey];
                if (twin.Properties.Desired.Contains(TwinProperty.GatewayID))
                    this.GatewayID = twin.Properties.Desired[TwinProperty.GatewayID];
                if (twin.Properties.Desired.Contains(TwinProperty.SensorDecoder))
                    this.SensorDecoder = twin.Properties.Desired[TwinProperty.SensorDecoder];
                if (twin.Properties.Reported.Contains(TwinProperty.FCntUp))
                    this.fcntUp = twin.Properties.Reported[TwinProperty.FCntUp];
                if (twin.Properties.Reported.Contains(TwinProperty.FCntDown))
                    this.fcntDown = twin.Properties.Reported[TwinProperty.FCntDown];

                Logger.Log(this.DevEUI, $"done getting twins", Logger.LoggingLevel.Info);
            }    
        }

        /// <summary>
        /// Saves the frame count changes
        /// </summary>
        /// <remarks>
        /// Changes will be saved only if there are actually changes to be saved
        /// </remarks>
        /// <returns></returns>
        public async Task<bool> SaveFrameCountChangesAsync()
        {
            if (this.hasFrameCountChanges == 1)
            {
                var reportedProperties = new TwinCollection();
                reportedProperties[TwinProperty.FCntDown] = this.FCntDown;
                reportedProperties[TwinProperty.FCntUp] = this.FCntUp;
                var result = await this.loRaDeviceClient.UpdateReportedPropertiesAsync(reportedProperties);
                if (result)
                {
                    AcceptFrameCountChanges();
                }

                return result;
            }

            return true;
        }

        /// <summary>
        /// Gets if there are frame count changes pending
        /// </summary>
        /// <returns></returns>
        public bool HasFrameCountChanges => hasFrameCountChanges == 1;

        /// <summary>
        /// Accept changes to the frame count
        /// </summary>
        public void AcceptFrameCountChanges() => Interlocked.Exchange(ref hasFrameCountChanges, 0);

        /// <summary>
        /// Increments <see cref="FCntDown"/>
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int IncrementFcntDown(int value)
        {
            var result = Interlocked.Add(ref fcntDown, value);
            Interlocked.Exchange(ref hasFrameCountChanges, 1);
            return result;
        }

        /// <summary>
        /// Sets a new value for <see cref="FCntDown"/>
        /// </summary>
        /// <param name="newValue"></param>
        public void SetFcntDown(int newValue)
        {
            var oldValue = Interlocked.Exchange(ref fcntDown, newValue);
            if (newValue != oldValue)
                Interlocked.Exchange(ref hasFrameCountChanges, 1);
        }


        public void SetFcntUp(int newValue)
        {
            var oldValue = Interlocked.Exchange(ref fcntUp, newValue);
            if (newValue != oldValue)
                Interlocked.Exchange(ref hasFrameCountChanges, 1);
        }



        public Task SendEventAsync(object payload, Dictionary<string, string> properties = null) => this.loRaDeviceClient.SendEventAsync(payload, properties);

        public Task<Message> ReceiveCloudToDeviceAsync(TimeSpan timeout) => this.loRaDeviceClient.ReceiveAsync(timeout);

        public Task<bool> CompleteCloudToDeviceMessageAsync(Message cloudToDeviceMessage) => this.loRaDeviceClient.CompleteAsync(cloudToDeviceMessage);

        public Task<bool> AbandonCloudToDeviceMessageAsync(Message cloudToDeviceMessage) => this.loRaDeviceClient.AbandonAsync(cloudToDeviceMessage);


        /// <summary>
        /// Updates device on the server after a join succeeded
        /// </summary>
        /// <param name="devAddr"></param>
        /// <param name="nwkSKey"></param>
        /// <param name="appSKey"></param>
        /// <param name="appNonce"></param>
        /// <param name="devNonce"></param>
        /// <param name="netID"></param>
        /// <returns></returns>
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
                this.NetId = netID;
                this.SetFcntDown(0);
                this.SetFcntUp(0);
                this.AcceptFrameCountChanges();
            }

            return succeeded;
        }        
    }
}
