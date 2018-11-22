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

        public LoRaPayloadJoinRequest(byte[] inputMessage) : base(inputMessage)
        {
            Mhdr = new Memory<byte>(inputMessage, 0, 1);
            var inputmsgstr = BitConverter.ToString(inputMessage);
            // get the joinEUI field
            AppEUI = new Memory<byte>(inputMessage,1,8) ;
            // get the DevEUI
            DevEUI = new Memory<byte>(inputMessage, 9, 8);
            // get the DevNonce
            DevNonce = new Memory<byte>(inputMessage, 17, 2);

        }

        public LoRaPayloadJoinRequest(byte[] _AppEUI, byte[] _DevEUI, byte[] _DevNonce)
        {
            // Mhdr is always 0 in case of a join request
            Mhdr = new byte[1] { 0x00 };
            AppEUI = new Memory<byte>(_AppEUI);
            DevEUI = new Memory<byte>(_DevEUI);
            DevNonce = new Memory<byte>(_DevNonce);
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
            var newDevEUI = DevEUI.ToArray();
            Array.Reverse(newDevEUI);
            var algoinput = Mhdr.ToArray().Concat(AppEUI.ToArray()).Concat(newDevEUI).Concat(DevNonce.ToArray()).ToArray();
            byte[] result = new byte[19];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            var resStr = BitConverter.ToString(result);
            return result.Take(4).ToArray();
        }

        public override byte[] PerformEncryption(string appSkey)
        {
            throw new NotImplementedException("The payload is not encrypted in case of a join message");
        }

        public override byte[] GetByteMessage()
        {
            List<byte> messageArray = new List<byte>();
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
    }
}
