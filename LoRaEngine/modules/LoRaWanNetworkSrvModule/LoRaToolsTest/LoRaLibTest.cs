//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Xunit;
using System.Text;
using System.Linq;
using LoRaTools.LoRaMessage;
using LoRaTools.Utils;
using static LoRaTools.LoRaMessage.LoRaPayload;
using static LoRaTools.LoRaMessage.LoRaPayloadData;
using LoRaTools.LoRaPhysical;
using System.Collections.Generic;

namespace LoRaWanTest
{

    /// <summary>
    /// all these test cases were inspired from https://github.com/brocaar/lora-app-server
    /// </summary>
    public class LoRaLibTest
    {
        [Fact]

        public void TestJoinAccept()
        {
            byte[] AppNonce = new byte[3]{
                87,11,199
            };
            byte[] NetId = new byte[3]{
                34,17,1
            };
            byte[] DevAddr = new byte[4]{
                2,3,25,128
            };
            var netId = ConversionHelper.ByteArrayToString(NetId);
            LoRaPayloadJoinAccept joinAccept = new LoRaPayloadJoinAccept(netId, "00112233445566778899AABBCCDDEEFF", DevAddr, AppNonce, new byte[] { 0 }, new byte[] { 0 }, null);
            Console.WriteLine(BitConverter.ToString(joinAccept.GetByteMessage()));
            LoRaMessageWrapper joinAcceptMessage = new LoRaMessageWrapper(joinAccept, LoRaMessageType.JoinAccept);
            byte[] joinAcceptMic = new byte[4]{
                67, 72, 91, 188
                };

            Assert.True((((LoRaPayloadJoinAccept)joinAcceptMessage.LoRaPayloadMessage).Mic.ToArray().SequenceEqual(joinAcceptMic)));

            var msg = ConversionHelper.ByteArrayToString(joinAcceptMessage.LoRaPayloadMessage.GetByteMessage());
            Assert.Equal("20493EEB51FBA2116F810EDB3742975142", msg);

        }


        [Fact]
        public void TestJoinRequest()
        {
            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            string jsonUplink = @"{ ""rxpk"":[
 	            {
		            ""time"":""2013-03-31T16:21:17.528002Z"",
 		            ""tmst"":3512348611,
 		            ""chan"":2,
 		            ""rfch"":0,
 		            ""freq"":866.349812,
 		            ""stat"":1,
 		            ""modu"":""LORA"",
 		            ""datr"":""SF7BW125"",
 		            ""codr"":""4/6"",
 		            ""rssi"":-35,
 		            ""lsnr"":5.1,
 		            ""size"":32,
 		            ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=""
                }]}";
            var joinRequestInput = Encoding.Default.GetBytes(jsonUplink);
            List<Rxpk> rxpk = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(joinRequestInput).ToArray());
            testRxpk(rxpk[0]);

        }

        private static void testRxpk(Rxpk rxpk)
        {
            LoRaMessageWrapper joinRequestMessage = new LoRaMessageWrapper(rxpk);

            if (joinRequestMessage.LoRaMessageType != LoRaMessageType.JoinRequest)
                Console.WriteLine("Join Request type was not parsed correclty");
            byte[] joinRequestAppKey = new byte[16]
            { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1};
            var joinRequestBool = joinRequestMessage.CheckMic(ConversionHelper.ByteArrayToString(joinRequestAppKey));
            if (!joinRequestBool)
            {
                Console.WriteLine("Join Request type was not computed correclty");
            }

            byte[] joinRequestAppEui = new byte[8]
            {1, 2, 3, 4, 1, 2, 3, 4};

            byte[] joinRequestDevEUI = new byte[8]
           {2, 3, 4, 5, 2, 3, 4, 5};
            byte[] joinRequestDevNonce = new byte[2]
            {16,45};

            Array.Reverse(joinRequestAppEui);
            Array.Reverse(joinRequestDevEUI);
            Array.Reverse(joinRequestDevNonce);
            Assert.True(joinRequestMessage.LoRaPayloadMessage.GetLoRaMessage().AppEUI.ToArray().SequenceEqual(joinRequestAppEui));
            Assert.True(joinRequestMessage.LoRaPayloadMessage.GetLoRaMessage().DevEUI.ToArray().SequenceEqual(joinRequestDevEUI));
            Assert.True(joinRequestMessage.LoRaPayloadMessage.GetLoRaMessage().DevNonce.ToArray().SequenceEqual(joinRequestDevNonce));
        }

        [Fact]
        public void TestUnconfirmedUplink()
        {
            string jsonUplinkUnconfirmedDataUp = @"{ ""rxpk"":[
               {
               ""time"":""2013-03-31T16:21:17.528002Z"",
                ""tmst"":3512348611,
                ""chan"":2,
                ""rfch"":0,
                ""freq"":866.349812,
                ""stat"":1,
                ""modu"":""LORA"",
                ""datr"":""SF7BW125"",
                ""codr"":""4/6"",
                ""rssi"":-35,
                ""lsnr"":5.1,
                ""size"":32,
                ""data"":""QAQDAgGAAQABppRkJhXWw7WC""
                 }]}";

            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;

            var jsonUplinkUnconfirmedDataUpBytes = Encoding.Default.GetBytes(jsonUplinkUnconfirmedDataUp);
            List<Rxpk> rxpk = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(jsonUplinkUnconfirmedDataUpBytes).ToArray());
            LoRaMessageWrapper jsonUplinkUnconfirmedMessage = new LoRaMessageWrapper(rxpk[0]);
            Assert.Equal(LoRaMessageType.UnconfirmedDataUp, jsonUplinkUnconfirmedMessage.LoRaMessageType);

            LoRaPayloadData loRaPayloadUplinkObj = (LoRaPayloadData)jsonUplinkUnconfirmedMessage.LoRaPayloadMessage;


            Assert.True(loRaPayloadUplinkObj.Fcnt.Span.SequenceEqual(new byte[2] { 1, 0 }));


            Assert.True(loRaPayloadUplinkObj.DevAddr.Span.SequenceEqual(new byte[4] { 1, 2, 3, 4 }));
            byte[] LoRaPayloadUplinkNwkKey = new byte[16] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };


            Assert.True(loRaPayloadUplinkObj.CheckMic(ConversionHelper.ByteArrayToString(LoRaPayloadUplinkNwkKey)));

            byte[] LoRaPayloadUplinkAppKey = new byte[16] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            var key = ConversionHelper.ByteArrayToString(LoRaPayloadUplinkAppKey);
            Assert.Equal("hello", Encoding.ASCII.GetString((loRaPayloadUplinkObj.PerformEncryption(key))));

        }

        [Fact]
        public void TestConfirmedDataUp()
        {
            byte[] mhdr = new byte[1];
            mhdr[0] = 128;
            byte[] devAddr = new byte[4]
                {4,3,2,1};

            byte[] fctrl = new byte[1]{
                0 };
            byte[] fcnt = new byte[2]
            {0,0 };
            byte[] fport = new byte[1]
            {
                    10
            };
            byte[] frmPayload = new byte[4]
            {
               4,3,2,1
            };

            var nwkkey = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var appkey = new byte[16] { 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };


            LoRaPayloadData lora = new LoRaPayloadData(MType.ConfirmedDataUp, devAddr, fctrl, fcnt, null, fport, frmPayload, 0);
            lora.PerformEncryption(ConversionHelper.ByteArrayToString(appkey));
            byte[] testEncrypt = new byte[4]
            {
                226, 100, 212, 247
            };
            Assert.Equal(testEncrypt, lora.Frmpayload.ToArray());
            lora.SetMic(ConversionHelper.ByteArrayToString(nwkkey));
            byte[] testMic = new byte[4]
            {
                181, 106, 14, 117
            };
            Assert.Equal(testMic, lora.Mic.ToArray());
            var mess = lora.GetByteMessage();
            lora.ChangeEndianess();
            Assert.Equal(mess, lora.RawMessage);
            //TODO

        }

        [Fact]
        public void TestKeys()
        {

            //create random message
            string jsonUplink = @"{ ""rxpk"":[
 	            {
		            ""time"":""2013-03-31T16:21:17.528002Z"",
 		            ""tmst"":3512348611,
 		            ""chan"":2,
 		            ""rfch"":0,
 		            ""freq"":866.349812,
 		            ""stat"":1,
 		            ""modu"":""LORA"",
 		            ""datr"":""SF7BW125"",
 		            ""codr"":""4/6"",
 		            ""rssi"":-35,
 		            ""lsnr"":5.1,
 		            ""size"":32,
 		            ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=""
                }]}";
            var joinRequestInput = Encoding.Default.GetBytes(jsonUplink);
            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            List<Rxpk> rxpk = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(joinRequestInput).ToArray());

            LoRaMessageWrapper joinRequestMessage = new LoRaMessageWrapper(rxpk[0]);

            var joinReq = (LoRaPayloadJoinRequest)joinRequestMessage.LoRaPayloadMessage;
            joinReq.DevAddr = new byte[4]
            {
                4,3,2,1
            };
            joinReq.DevEUI = new byte[8]
            {
                8,7,6,5,4,3,2,1
            };
            joinReq.DevNonce = new byte[2]
            {
                2,1
            };
            joinReq.AppEUI = new byte[8]
            {
                1,2,3,4,5,6,7,8
            };

            byte[] appNonce = new byte[3]
            {
                0,0,1
            };
            byte[] netId = new byte[3]
            {
                3,2,1
            };
            byte[] appKey = new byte[16]
            {
                1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8
            };
            var key = joinReq.CalculateKey(KeyType.NwkSKey, appNonce, netId, joinReq.DevNonce.ToArray(), appKey);
            Assert.Equal(key, new byte[16]{
                223, 83, 195, 95, 48, 52, 204, 206, 208, 255, 53, 76, 112, 222, 4, 223
            }
            );
        }

        [Fact]
        public void TestMultipleRxpks()
        {
            //create multiple Rxpk message
            string jsonUplink = @"{""rxpk"":[
	{
		            ""time"":""2013-03-31T16:21:17.528002Z"",
 		            ""tmst"":3512348611,
 		            ""chan"":2,
 		            ""rfch"":0,
 		            ""freq"":866.349812,
 		            ""stat"":1,
 		            ""modu"":""LORA"",
 		            ""datr"":""SF7BW125"",
 		            ""codr"":""4/6"",
 		            ""rssi"":-35,
 		            ""lsnr"":5.1,
 		            ""size"":32,
 		            ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=""
                }, {
               ""time"":""2013-03-31T16:21:17.528002Z"",
                ""tmst"":3512348611,
                ""chan"":2,
                ""rfch"":0,
                ""freq"":866.349812,
                ""stat"":1,
                ""modu"":""LORA"",
                ""datr"":""SF7BW125"",
                ""codr"":""4/6"",
                ""rssi"":-35,
                ""lsnr"":5.1,
                ""size"":32,
                ""data"":""QAQDAgGAAQABppRkJhXWw7WC""
                 },{
		            ""time"":""2013-03-31T16:21:17.528002Z"",
 		            ""tmst"":3512348611,
 		            ""chan"":2,
 		            ""rfch"":0,
 		            ""freq"":866.349812,
 		            ""stat"":1,
 		            ""modu"":""LORA"",
 		            ""datr"":""SF7BW125"",
 		            ""codr"":""4/6"",
 		            ""rssi"":-35,
 		            ""lsnr"":5.1,
 		            ""size"":32,
 		            ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=""
                }
]}";
            var multiRxpkInput = Encoding.Default.GetBytes(jsonUplink);
            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            List<Rxpk> rxpk = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(multiRxpkInput).ToArray());
            List<LoRaMessageWrapper> rxpkMessages = new List<LoRaMessageWrapper>();
            foreach (var rxpkmessage in rxpk)
            {
                rxpkMessages.Add(new LoRaMessageWrapper(rxpkmessage));
            }
            Assert.Equal(rxpkMessages.Count, 3);
            testRxpk(rxpk[0]);
            testRxpk(rxpk[2]);

              LoRaMessageWrapper jsonUplinkUnconfirmedMessage = new LoRaMessageWrapper(rxpk[1]);
            Assert.Equal(LoRaMessageType.UnconfirmedDataUp, jsonUplinkUnconfirmedMessage.LoRaMessageType);

            LoRaPayloadData loRaPayloadUplinkObj = (LoRaPayloadData)jsonUplinkUnconfirmedMessage.LoRaPayloadMessage;


            Assert.True(loRaPayloadUplinkObj.Fcnt.Span.SequenceEqual(new byte[2] { 1, 0 }));


            Assert.True(loRaPayloadUplinkObj.DevAddr.Span.SequenceEqual(new byte[4] { 1, 2, 3, 4 }));
            byte[] LoRaPayloadUplinkNwkKey = new byte[16] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };


            Assert.True(loRaPayloadUplinkObj.CheckMic(ConversionHelper.ByteArrayToString(LoRaPayloadUplinkNwkKey)));

            byte[] LoRaPayloadUplinkAppKey = new byte[16] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            var key = ConversionHelper.ByteArrayToString(LoRaPayloadUplinkAppKey);
            Assert.Equal("hello", Encoding.ASCII.GetString((loRaPayloadUplinkObj.PerformEncryption(key))));

        }



    }
}
