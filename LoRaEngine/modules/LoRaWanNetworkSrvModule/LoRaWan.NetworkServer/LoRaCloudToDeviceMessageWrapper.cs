// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.CommonAPI;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;

    internal class LoRaCloudToDeviceMessageWrapper : IReceivedLoRaCloudToDeviceMessage
    {
        private readonly LoRaDevice loRaDevice;
        private readonly Message message;
        private ReceivedLoRaCloudToDeviceMessage parseCloudToDeviceMessage;
        private string invalidErrorMessage;

        public LoRaCloudToDeviceMessageWrapper(LoRaDevice loRaDevice, Message message)
        {
            this.loRaDevice = loRaDevice ?? throw new ArgumentNullException(nameof(loRaDevice));
            this.message = message ?? throw new ArgumentNullException(nameof(message));

            ParseMessage();
        }

        /// <summary>
        /// Tries to parse the <see cref="Message.GetBytes"/> to a json representation of <see cref="LoRaCloudToDeviceMessage"/>.
        /// </summary>
        private void ParseMessage()
        {
            var bytes = this.message.GetBytes();
            if (bytes?.Length > 0)
            {
                var json = Encoding.UTF8.GetString(bytes);
                try
                {
                    this.parseCloudToDeviceMessage = JsonConvert.DeserializeObject<ReceivedLoRaCloudToDeviceMessage>(json);
                }
                catch (Exception ex) when (ex is JsonReaderException or JsonSerializationException)
                {
                    this.invalidErrorMessage = $"could not parse cloud to device message: {json}: {ex.Message}";
                }
            }
            else
            {
                this.invalidErrorMessage = "cloud message does not have a body";
            }
        }

        public FramePort Fport => this.parseCloudToDeviceMessage?.Fport ?? 0;

        public bool Confirmed
        {
            get
            {
                if (this.parseCloudToDeviceMessage != null)
                    return this.parseCloudToDeviceMessage.Confirmed;

                return false;
            }
        }

        public string MessageId => this.parseCloudToDeviceMessage?.MessageId ?? this.message.MessageId;

        public DevEui? DevEUI => this.loRaDevice.DevEUI;

        public byte[] GetPayload()
        {
            if (this.parseCloudToDeviceMessage != null)
                return this.parseCloudToDeviceMessage.GetPayload();

            return Array.Empty<byte>();
        }

        public IList<MacCommand> MacCommands
        {
            get
            {
                if (this.parseCloudToDeviceMessage != null)
                {
                    return this.parseCloudToDeviceMessage.MacCommands;
                }

                return null;
            }
        }

        public Task<bool> CompleteAsync() => this.loRaDevice.CompleteCloudToDeviceMessageAsync(this.message);

        public Task<bool> AbandonAsync() => this.loRaDevice.AbandonCloudToDeviceMessageAsync(this.message);

        public Task<bool> RejectAsync() => this.loRaDevice.RejectCloudToDeviceMessageAsync(this.message);

        public bool IsValid(out string errorMessage)
        {
            if (this.parseCloudToDeviceMessage == null)
            {
                errorMessage = this.invalidErrorMessage;
                return false;
            }

            return this.parseCloudToDeviceMessage.IsValid(out errorMessage);
        }
    }
}
