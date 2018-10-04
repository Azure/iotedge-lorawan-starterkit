using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;


namespace PacketManager
{
    public class OTAAKeysGenerator
    {

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }


        public static string getDevAddr(Byte[] netId)
        {
            int nwkPart = (netId[2] << 1);

            byte[] devAddr = new byte[4];
            Random rnd = new Random();
            rnd.NextBytes(devAddr);
            //loosing a bit
            devAddr[0] = (byte)nwkPart;

            return BitConverter.ToString(devAddr).Replace("-", "");
        }

        //type NwkSKey = 0x01 , AppSKey = 0x02
        //don't work with CFLIST atm
        public static string calculateKey(byte[] type, byte[] appnonce, byte[] netid, byte[] devnonce, byte[] appKey)
        {


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

        public static string  getAppNonce()
        {
            Random rnd = new Random();
            byte[] appNonce = new byte[3];
            rnd.NextBytes(appNonce);
            return BitConverter.ToString(appNonce).Replace("-", "");
        }
    }
}
