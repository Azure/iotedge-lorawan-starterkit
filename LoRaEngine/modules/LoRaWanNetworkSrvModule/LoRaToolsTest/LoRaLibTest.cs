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
            
            byte[] joinAcceptMic = new byte[4]{
                67, 72, 91, 188
                };

            Assert.True(joinAccept.Mic.ToArray().SequenceEqual(joinAcceptMic));    
            var msg = ConversionHelper.ByteArrayToString(joinAccept.GetByteMessage());
            Assert.Equal("20493EEB51FBA2116F810EDB3742975142", msg);

        }

        [Fact]
        public void JoinRequest_Should_Succeed_Mic_Check()
        {
            var appEUIText = "0005100000000004";
            var appEUIBytes = LoRaTools.Utils.ConversionHelper.StringToByteArray(appEUIText);

            var devEUIText = "0005100000000004";
            var devEUIBytes = LoRaTools.Utils.ConversionHelper.StringToByteArray(devEUIText);

            var devNonceText = "ABCD";
            var devNonceBytes = LoRaTools.Utils.ConversionHelper.StringToByteArray(devNonceText);

            var appKey = "00000000000000000005100000000004";

            var joinRequest = new LoRaPayloadJoinRequest(appEUIBytes, devEUIBytes, devNonceBytes);
            joinRequest.SetMic(appKey);
            Assert.True(joinRequest.CheckMic(appKey));

            

            var rxpk = new LoRaTools.LoRaPhysical.Rxpk()
            {
                chan = 7,
                rfch = 1,
                freq = 903.700000,
                stat = 1,
                modu = "LORA",
                datr = "SF10BW125",
                codr = "4/5",
                rssi = -17,
                lsnr = 12.0f
            };

            var data = joinRequest.GetByteMessage();
            rxpk.data = Convert.ToBase64String(data);
            rxpk.size = (uint)data.Length;

            byte[] decodedJoinRequestBytes = Convert.FromBase64String(rxpk.data);
            var decodedJoinRequest = new LoRaTools.LoRaMessage.LoRaPayloadJoinRequest(decodedJoinRequestBytes);
            Assert.True(decodedJoinRequest.CheckMic(appKey));
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
            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk, out LoRaPayload loRaPayload));
            Assert.Equal(LoRaMessageType.JoinRequest, loRaPayload.LoRaMessageType);
            LoRaPayloadJoinRequest joinRequestMessage = (LoRaPayloadJoinRequest)loRaPayload;


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
            Assert.True(joinRequestMessage.AppEUI.ToArray().SequenceEqual(joinRequestAppEui));
            Assert.True(joinRequestMessage.DevEUI.ToArray().SequenceEqual(joinRequestDevEUI));
            Assert.True(joinRequestMessage.DevNonce.ToArray().SequenceEqual(joinRequestDevNonce));
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
            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk[0], out LoRaPayload loRaPayload));

            Assert.Equal(LoRaMessageType.UnconfirmedDataUp, loRaPayload.LoRaMessageType);
  
            LoRaPayloadData loRaPayloadUplinkObj = (LoRaPayloadData)loRaPayload;
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


            LoRaPayloadData lora = new LoRaPayloadData(LoRaMessageType.ConfirmedDataUp, devAddr, fctrl, fcnt, null, fport, frmPayload, 0);
            lora.PerformEncryption(ConversionHelper.ByteArrayToString(appkey));
            byte[] testEncrypt = new byte[4]
            {
                226, 100, 212, 247
            };
            Assert.Equal(testEncrypt, lora.Frmpayload.ToArray());
            lora.SetMic(ConversionHelper.ByteArrayToString(nwkkey));
            lora.CheckMic(ConversionHelper.ByteArrayToString(nwkkey));
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
            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk[0], out LoRaPayload loRaPayload));
            Assert.Equal(LoRaMessageType.JoinRequest,loRaPayload.LoRaMessageType);
            var joinReq = (LoRaPayloadJoinRequest)loRaPayload;
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


        // This test will validate if creating payloads will work
        // LoRaPayloadData (UnconfirmedDataUp) -> Rxpk -> LoRaPayloadData -> check properties
        [Theory(Skip="MIK needs to look at this first")]
        [InlineData("1234")]
        [InlineData("hello world")]
        public void When_Creating_Rxpk_Recreating_Payload_Should_Match_Source_Values_Old_Way(string data)
        {
            var devAddrText = "00000060";
            var appSKeyText = "00000060000000600000006000000060";
            var nwkSKeyText = "00000060000000600000006000000060";

            byte[] devAddr = ConversionHelper.StringToByteArray(devAddrText);
            Array.Reverse(devAddr);
            byte[] fCtrl = new byte[] { 0x80 };

            var fcnt = 12;
            var fcntBytes = BitConverter.GetBytes(fcnt);

            byte[] fopts = null;
            byte[] fPort = new byte[] { 1 };            
            byte[] payload = Encoding.UTF8.GetBytes(data);
            Array.Reverse(payload);

            // 0 = uplink, 1 = downlink
            int direction = 0;
            var standardData = new LoRaPayloadData(LoRaMessageType.UnconfirmedDataUp, devAddr, fCtrl, fcntBytes, fopts, fPort, payload, direction);
            // Need to create Fops. If not, then MIC won't be correct
            standardData.Fopts = new byte[0];
            // First encrypt the data
            standardData.PerformEncryption(appSKeyText);
            // Now we have the full package, create the MIC
            standardData.SetMic(nwkSKeyText); //"99D58493D1205B43EFF938F0F66C339E");         


            var rxpk = new Rxpk()
            {
                chan = 7,
                rfch = 1,
                freq = 868.3,
                stat = 1,
                modu = "LORA",
                datr = "SF10BW125",
                codr = "4/5",
                rssi = -17,
                lsnr = 12.0f
            };

            var loraPayloadAsBytes = standardData.GetByteMessage();
            rxpk.data = Convert.ToBase64String(loraPayloadAsBytes);
            rxpk.size = (uint)loraPayloadAsBytes.Length;


            // Now try to recreate LoRaPayloadData from rxpk
            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk, out LoRaPayload parsedLoRaPayload));
            Assert.Equal(LoRaMessageType.UnconfirmedDataUp, parsedLoRaPayload.LoRaMessageType);
            Assert.IsType<LoRaPayloadData>(parsedLoRaPayload);
            var parsedLoRaPayloadData = (LoRaPayloadData)parsedLoRaPayload;
            Assert.Equal(12, parsedLoRaPayloadData.GetFcnt());
            Assert.Equal(0, parsedLoRaPayloadData.Direction);
            Assert.Equal(1, parsedLoRaPayloadData.GetFPort());

            // How to get the payload back?
            var parsedPayloadBytes = parsedLoRaPayload.PerformEncryption(appSKeyText);
            Assert.Equal(data, Encoding.UTF8.GetString(parsedPayloadBytes));


        }

        // This test will validate if creating payloads will work, using new approach
        // LoRaPayloadData (UnconfirmedDataUp) -> SerializeUplink -> Uplink.Rxpk[0] -> LoRaPayloadData -> check properties
        [Theory(Skip="MIK needs to look at this first")]
        [InlineData("1234")]
        [InlineData("hello world")]
        public void When_Creating_Rxpk_Recreating_Payload_Should_Match_Source_Values(string data)
        {
            var devAddrText = "00000060";
            var appSKeyText = "00000060000000600000006000000060";
            var nwkSKeyText = "00000060000000600000006000000060";

            var fcnt = 12;
            byte[] devAddr = ConversionHelper.StringToByteArray(devAddrText);
            Array.Reverse(devAddr);
            byte[] fCtrl = new byte[] { 0x80 };
            var fcntBytes = BitConverter.GetBytes(fcnt);
            byte[] fopts = new byte[0];
            byte[] fPort = new byte[] { 1 };
            byte[] payload = Encoding.UTF8.GetBytes(data);
            Array.Reverse(payload);
            // 0 = uplink, 1 = downlink
            int direction = 0;
            var devicePayloadData = new LoRaPayloadData(LoRaMessageType.UnconfirmedDataUp, devAddr, fCtrl, fcntBytes, fopts, fPort, payload, direction);

            var datr = "SF10BW125";
            var freq = 868.3;

            var uplinkMsg = devicePayloadData.SerializeUplink(appSKeyText, nwkSKeyText, datr, freq, 0);



            // Now try to recreate LoRaPayloadData from rxpk
            Assert.True(LoRaPayload.TryCreateLoRaPayload(uplinkMsg.rxpk[0], out LoRaPayload parsedLoRaPayload));
            Assert.Equal(LoRaMessageType.UnconfirmedDataUp, parsedLoRaPayload.LoRaMessageType);
            Assert.IsType<LoRaPayloadData>(parsedLoRaPayload);
            var parsedLoRaPayloadData = (LoRaPayloadData)parsedLoRaPayload;
            Assert.Equal(12, parsedLoRaPayloadData.GetFcnt());
            Assert.Equal(0, parsedLoRaPayloadData.Direction);
            Assert.Equal(1, parsedLoRaPayloadData.GetFPort());

            // How to get the payload back?
            var parsedPayloadBytes = parsedLoRaPayload.PerformEncryption(appSKeyText);
            Assert.Equal(data, Encoding.UTF8.GetString(parsedPayloadBytes));
        }

        // When creating a join request using simulated devices, rebuilding it should pass the mic check
        [Theory]
        [InlineData("000000000000AABB", "0000000000001111", "00000000000000000000000000002222", "5060")]
        [InlineData("0000000000000001", "0000000000000001", "00000000000000000000000000000001", "C39F")]
        public void When_Creating_Join_Request_Recreating_Should_Pass_Mic_Check(
            string appEUIText,
            string devEUIText,
            string appKeyText,
            string devNonceText)
        {
            var wrongAppKeyText = "00000000000000000000000000003333";

            //create a join request
            byte[] appEUI = ConversionHelper.StringToByteArray(appEUIText);
            Array.Reverse(appEUI);
            byte[] devEUI = ConversionHelper.StringToByteArray(devEUIText);
            Array.Reverse(devEUI);

            var devNonce = ConversionHelper.StringToByteArray(devNonceText);
            Array.Reverse(devNonce);

            var join = new LoRaPayloadJoinRequest(appEUI, devEUI, devNonce);
            join.SetMic(appKeyText);

            Assert.False(join.CheckMic(wrongAppKeyText), "Mic check with wrong appKey should not pass");
            Assert.True(join.CheckMic(appKeyText), "Mic check should work after setting it");               

            // Create rxpk with join request
            var rxpk = new Rxpk()
            {
                chan = 7,
                rfch = 1,
                freq = 868.3,
                stat = 1,
                modu = "LORA",
                datr = "SF10BW125",
                codr = "4/5",
                rssi = -17,
                lsnr = 12.0f
            };

            var data = join.GetByteMessage();
            rxpk.data = Convert.ToBase64String(data);
            rxpk.size = (uint)data.Length;        
            rxpk.tmst = 0;


            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk, out LoRaPayload parsedLoRaPayload));
            Assert.IsType<LoRaPayloadJoinRequest>(parsedLoRaPayload);
            var parsedLoRaJoinRequest = (LoRaPayloadJoinRequest)parsedLoRaPayload;
            Assert.False(parsedLoRaJoinRequest.CheckMic(wrongAppKeyText), "Parsed join request should not pass mic check with wrong appKey");
            Assert.True(parsedLoRaPayload.CheckMic(appKeyText), "Parsed join request should pass mic check with correct appKey");
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
           
            Assert.Equal(3,rxpk.Count);
            testRxpk(rxpk[0]);
            testRxpk(rxpk[2]);

            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk[1], out LoRaPayload jsonUplinkUnconfirmedMessage));
            Assert.Equal(LoRaMessageType.UnconfirmedDataUp, jsonUplinkUnconfirmedMessage.LoRaMessageType);

            LoRaPayloadData loRaPayloadUplinkObj = (LoRaPayloadData)jsonUplinkUnconfirmedMessage;

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
