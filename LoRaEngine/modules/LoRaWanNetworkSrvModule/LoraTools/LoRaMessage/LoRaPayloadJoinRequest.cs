using LoRaTools.LoRaPhysical;
using LoRaTools.Utils;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LoRaTools.LoRaMessage
{
    /// <summary>
    /// Implementation of the Join Request message type.
    /// </summary>
    public class LoRaPayloadJoinRequest : LoRaPayload
    {
        /// <summary>
        /// aka JoinEUI
        /// </summary>
        public Memory<byte> AppEUI { get; set; }
        public Memory<byte> DevEUI { get; set; }
        public Memory<byte> DevNonce { get; set; }

        /// <summary>
        /// Gets the value of DevEUI as <see cref="string"/>
        /// </summary>
        /// <returns></returns>
        public string GetDevEUIAsString() => ConversionHelper.ReverseByteArrayToString(this.DevEUI);

        /// <summary>
        /// Gets the value of AppEUI as <see cref="string"/>
        /// </summary>
        /// <returns></returns>
        public string GetAppEUIAsString() => ConversionHelper.ReverseByteArrayToString(this.AppEUI);

        /// <summary>
        /// Gets the value <see cref="DevNonce"/> as <see cref="string"/>
        /// </summary>
        /// <returns></returns>
        public string GetDevNonceAsString() => ConversionHelper.ByteArrayToString(DevNonce);

        public LoRaPayloadJoinRequest(byte[] inputMessage) : base(inputMessage)
        {
            Mhdr = new Memory<byte>(inputMessage, 0, 1);
            // get the joinEUI field
            AppEUI = new Memory<byte>(inputMessage,1,8) ;
            // get the DevEUI
            DevEUI = new Memory<byte>(inputMessage, 9, 8);
            // get the DevNonce
            DevNonce = new Memory<byte>(inputMessage, 17, 2);

        }

        public LoRaPayloadJoinRequest(string appEUI, string devEUI, byte[] devNonce)
        {
            // Mhdr is always 0 in case of a join request
            Mhdr = new byte[1] { 0x00 };

            var appEUIBytes = ConversionHelper.StringToByteArray(appEUI);
            var devEUIBytes = ConversionHelper.StringToByteArray(devEUI);


            // Store as reversed value
            // When coming from real device and pktfwd is is reversed
            // message processor reverses both values before getting it
            Array.Reverse(appEUIBytes);
            Array.Reverse(devEUIBytes);
            AppEUI = new Memory<byte>(appEUIBytes);
            DevEUI = new Memory<byte>(devEUIBytes);
            DevNonce = new Memory<byte>(devNonce);
            Mic = new Memory<byte>();
        }

        public override bool CheckMic(string appKey)
        {
            return Mic.ToArray().SequenceEqual(PerformMic(appKey));
        }

        public void SetMic(string appKey)
        {
            Mic = PerformMic(appKey);
        }

        private byte[] PerformMic(string appKey)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");

            KeyParameter key = new KeyParameter(ConversionHelper.StringToByteArray(appKey));
            mac.Init(key);
            var algoInputBytes = new byte[19];
            var algoInput = new Memory<byte>(algoInputBytes);
            
            var offset = 0;
            this.Mhdr.CopyTo(algoInput);
            offset += Mhdr.Length;
            this.AppEUI.CopyTo(algoInput.Slice(offset));
            offset += this.AppEUI.Length;
            this.DevEUI.CopyTo(algoInput.Slice(offset));
            offset += this.DevEUI.Length;
            this.DevNonce.CopyTo(algoInput.Slice(offset));
            
            mac.BlockUpdate(algoInputBytes, 0, algoInputBytes.Length);
            
            var result = MacUtilities.DoFinal(mac);
            return result.Take(4).ToArray();
        }

        public override byte[] PerformEncryption(string appSkey) => throw new NotImplementedException("The payload is not encrypted in case of a join message");

        public override byte[] GetByteMessage()
        {
            List<byte> messageArray = new List<byte>(23);
            messageArray.AddRange(Mhdr.ToArray());
            messageArray.AddRange(AppEUI.ToArray());
            messageArray.AddRange(DevEUI.ToArray());
            messageArray.AddRange(DevNonce.ToArray());
            if (!Mic.Span.IsEmpty)
            {
                messageArray.AddRange(Mic.ToArray());
            }
            return messageArray.ToArray();
        }

        /// <summary>
        /// Serializes uplink message, used by simulator
        /// </summary>
        /// <param name="appKey"></param>
        /// <param name="datr"></param>
        /// <param name="freq"></param>
        /// <param name="tmst"></param>
        /// <returns></returns>
        public UplinkPktFwdMessage SerializeUplink(string appKey, string datr = "SF10BW125", double freq = 868.3, uint tmst = 0)
        {
            SetMic(appKey);
            return new UplinkPktFwdMessage(this.GetByteMessage(), datr, freq, tmst);
        }

        
    }
}
