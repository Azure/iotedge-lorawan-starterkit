using LoRaTools.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;


namespace LoRaTools
{
    public class OTAAKeysGenerator
    {




        public static string getDevAddr(Byte[] netId)
        {
            int nwkPart = (netId[2] << 1);

            byte[] devAddr = new byte[4];
            Random rnd = new Random();
            rnd.NextBytes(devAddr);
            //loosing a bit
            devAddr[0] = (byte)nwkPart;

            return ConversionHelper.ByteArrayToString(devAddr);
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
            return ConversionHelper.ByteArrayToString(key);
        }

        public static string  getAppNonce()
        {
            Random rnd = new Random();
            byte[] appNonce = new byte[3];
            rnd.NextBytes(appNonce);
            return ConversionHelper.ByteArrayToString(appNonce);
        }
    }
}
