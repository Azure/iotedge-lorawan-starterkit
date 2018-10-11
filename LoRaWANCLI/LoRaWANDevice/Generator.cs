using System;

namespace LoRaWANDevice
{
    public partial class CredentialGenerator
    {
        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public static string genNonce()
        {
            Random rnd = new Random();
            byte[] nonce = new byte[3];
            rnd.NextBytes(nonce);
            return BitConverter.ToString(nonce).Replace("-", "");
        }

        public static string genDevAddr()
        {
            int nwkPart = 1;

            byte[] devAddr = new byte[4];
            Random rnd = new Random();
            rnd.NextBytes(devAddr);
            //losing a bit
            devAddr[0] = (byte)nwkPart;

            return BitConverter.ToString(devAddr).Replace("-", "");
        }

        public static string genDevAddr(Byte[] netId)
        {
            int nwkPart = (netId[2] << 1);

            byte[] devAddr = new byte[4];
            Random rnd = new Random();
            rnd.NextBytes(devAddr);
            //losing a bit
            devAddr[0] = (byte)nwkPart;

            return BitConverter.ToString(devAddr).Replace("-", "");
        }

         public static string genKey (byte[] type, byte[] appnonce, byte[] netid, byte[] devnonce, byte[] appKey)
        {
            //type NwkSKey = 0x01 , AppSKey = 0x02
            //don't work with CFLIST atm

            Aes aes = new AesManaged();
            aes.Key = appKey;

            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            byte[] pt = type.Concat(appnonce).Concat(netid).Concat(devnonce).Concat(new byte[7]).ToArray();

            aes.IV = new byte[16];
            ICryptoTransform cipher;
            cipher = aes.CreateEncryptor();

            var key = cipher.TransformFinalBlock(pt, 0, pt.Length);          
            return BitConverter.ToString(key).Replace("-", "");
        }

        public static string genEUI ()
        {
            Random rnd = new Random();
            byte[] EUI = new byte[8];
            rnd.NextBytes(EUI);
            return BitConverter.ToString(EUI).Replace("-", "");
        }
    }
}
