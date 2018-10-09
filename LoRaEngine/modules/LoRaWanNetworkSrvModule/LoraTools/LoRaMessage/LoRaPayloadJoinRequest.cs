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
    public class LoRaPayloadJoinRequest : LoRaDataPayload
    {
        /// <summary>
        /// aka JoinEUI
        /// </summary>
        public Memory<byte> AppEUI { get; set; }
        public Memory<byte> DevEUI { get; set; }
        public Memory<byte> DevNonce { get; set; }

        public LoRaPayloadJoinRequest(byte[] inputMessage) : base(inputMessage)
        {
            var inputmsgstr = BitConverter.ToString(inputMessage);
            // get the joinEUI field
            AppEUI = new Memory<byte>(inputMessage,1,8) ;
            // get the DevEUI
            DevEUI = new Memory<byte>(inputMessage, 9, 8);
            // get the DevNonce
            DevNonce = new Memory<byte>(inputMessage, 17, 2);

        }



        public override bool CheckMic(string appKey)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");

            KeyParameter key = new KeyParameter(StringToByteArray(appKey));
            mac.Init(key);
            var algoinput = Mic.ToArray().Concat(AppEUI.ToArray()).Concat(DevEUI.ToArray()).Concat(DevNonce.ToArray()).ToArray();
            byte[] result = new byte[19];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            var resStr = BitConverter.ToString(result);
            return Mic.ToArray().SequenceEqual(result.Take(4).ToArray());
        }

        public override byte[] PerformEncryption(string appSkey)
        {
            throw new NotImplementedException("The payload is not encrypted in case of a join message");
        }

        public override byte[] GetByteMessage()
        {
            throw new NotImplementedException();
        }

        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}
