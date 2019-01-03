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

        public bool IsABP { get; set; }

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
        public bool IsABPRelaxedFrameCounter { get; internal set; } = true;
        public bool AlwaysUseSecondWindow { get; internal set; } = false;

        public LoRaDevice(string devAddr, string devEUI, ILoRaDeviceClient loRaDeviceClient)
        {
            DevAddr = devAddr;
            DevEUI = devEUI;
            this.loRaDeviceClient = loRaDeviceClient;
        }

        public async Task InitializeAsync()
        {
            var twin = await this.loRaDeviceClient.GetTwinAsync();

            if (twin != null)
            {
                //ABP Case
                if (twin.Properties.Desired.Contains(TwinProperty.AppSKey))
                {
                    this.AppSKey = twin.Properties.Desired[TwinProperty.AppSKey];
                    this.NwkSKey = twin.Properties.Desired[TwinProperty.NwkSKey];
                    this.DevAddr = twin.Properties.Desired[TwinProperty.DevAddr];
                    this.IsABP = true;
                }
                //OTAA Case
                else if (twin.Properties.Reported.Contains(TwinProperty.AppSKey))
                {
                    this.AppSKey = twin.Properties.Reported[TwinProperty.AppSKey];
                    this.NwkSKey = twin.Properties.Reported[TwinProperty.NwkSKey];
                    this.DevAddr = twin.Properties.Reported[TwinProperty.DevAddr];
                    this.DevNonce = twin.Properties.Reported[TwinProperty.DevNonce];

                    //todo check if appkey and appeui is needed in the flow
                    this.AppEUI = twin.Properties.Desired[TwinProperty.AppEUI];
                    this.AppKey = twin.Properties.Desired[TwinProperty.AppKey];
                }
                else
                {
                    Logger.Log(this.DevEUI, $"AppSKey not present neither in Desired or in Reported properties", Logger.LoggingLevel.Error);
                }

                if (twin.Properties.Desired.Contains(TwinProperty.GatewayID))
                    this.GatewayID = twin.Properties.Desired[TwinProperty.GatewayID];
                if (twin.Properties.Desired.Contains(TwinProperty.SensorDecoder))
                    this.SensorDecoder = twin.Properties.Desired[TwinProperty.SensorDecoder];
                this.IsOurDevice = true;
                if (twin.Properties.Reported.Contains(TwinProperty.FCntUp))
                    this.fcntUp = twin.Properties.Reported[TwinProperty.FCntUp];
                if (twin.Properties.Reported.Contains(TwinProperty.FCntDown))
                    this.fcntDown = twin.Properties.Reported[TwinProperty.FCntDown];

                Logger.Log(this.DevEUI, $"done getting twins", Logger.LoggingLevel.Info);

            }    
        }

        public async Task<Twin> GetTwinAsync() => await this.loRaDeviceClient.GetTwinAsync();

        public async Task SaveFrameCountChangesAsync()
        {
            var reportedProperties = new TwinCollection();
            reportedProperties[TwinProperty.FCntDown] = this.FCntDown;
            reportedProperties[TwinProperty.FCntUp] = this.FCntUp;
            await this.loRaDeviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        public int IncrementFcntDown(int value) => Interlocked.Add(ref fcntDown, value);

        public void SetFcntUp(int newValue) => Interlocked.Exchange(ref fcntUp, newValue);

        public void SetFcntDown(int newValue) => Interlocked.Exchange(ref fcntDown, newValue);

        public async Task SendEventAsync(string messageBody, Dictionary<string, string> properties = null) => await this.loRaDeviceClient.SendEventAsync(messageBody, properties);

        public async Task<Message> ReceiveCloudToDeviceAsync(TimeSpan timeout) => await this.loRaDeviceClient.ReceiveAsync(timeout);

        public async Task CompleteCloudToDeviceMessageAsync(Message cloudToDeviceMessage) => await this.loRaDeviceClient.CompleteAsync(cloudToDeviceMessage);


        public async Task AbandonCloudToDeviceMessageAsync(Message cloudToDeviceMessage) => await this.loRaDeviceClient.AbandonAsync(cloudToDeviceMessage);


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
            }

            return succeeded;
        }
    }


}
