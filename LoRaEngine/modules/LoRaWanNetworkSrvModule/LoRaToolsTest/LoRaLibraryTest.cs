// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Xunit;

    /// <summary>
    /// all these test cases were inspired from https://github.com/brocaar/lora-app-server
    /// </summary>
    public class LoRaLibraryTest
    {
        /// <summary>
        /// Test Join Accept messages
        /// </summary>
        [Fact]
        public void TestJoinAccept()
        {
            byte[] appNonce = new byte[3]
            {
                87, 11, 199,
            };
            byte[] netId1 = new byte[3]
            {
                34, 17, 1,
            };
            byte[] devAddr = new byte[4]
            {
                2, 3, 25, 128,
            };

            var netId = ConversionHelper.ByteArrayToString(netId1);
            var appkey = "00112233445566778899AABBCCDDEEFF";
            LoRaPayloadJoinAccept joinAccept = new LoRaPayloadJoinAccept(netId, devAddr, appNonce, new byte[] { 0 }, 0, null);
            var joinacceptbyte = joinAccept.Serialize(appkey, "SF10BW125", 866.349812, 10000, "test");
            var decodedJoinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(joinacceptbyte.Txpk.Data), appkey);
            byte[] joinAcceptMic = new byte[4]
            {
                67, 72, 91, 188,
                };

            Assert.True(decodedJoinAccept.Mic.ToArray().SequenceEqual(joinAcceptMic));
            var msg = ConversionHelper.ByteArrayToString(Convert.FromBase64String(joinacceptbyte.Txpk.Data));
            Assert.Equal("20493EEB51FBA2116F810EDB3742975142", msg);
        }

        /// <summary>
        /// Test mic Check
        /// </summary>
        [Fact]
        public void JoinRequest_Should_Succeed_Mic_Check()
        {
            var appEUIText = "0005100000000004";
            var appEUIBytes = ConversionHelper.StringToByteArray(appEUIText);

            var devEUIText = "0005100000000004";
            var devEUIBytes = ConversionHelper.StringToByteArray(devEUIText);

            var devNonceText = "ABCD";
            var devNonceBytes = ConversionHelper.StringToByteArray(devNonceText);

            var appKey = "00000000000000000005100000000004";

            var joinRequest = new LoRaPayloadJoinRequest(appEUIText, devEUIText, devNonceBytes);
            joinRequest.SetMic(appKey);
            Assert.True(joinRequest.CheckMic(appKey));
            Assert.True(joinRequest.CheckMic(appKey)); // ensure multiple calls work!

            var rxpk = new LoRaTools.LoRaPhysical.Rxpk()
            {
                Chan = 7,
                Rfch = 1,
                Freq = 903.700000,
                Stat = 1,
                Modu = "LORA",
                Datr = "SF10BW125",
                Codr = "4/5",
                Rssi = -17,
                Lsnr = 12.0f,
            };

            var data = joinRequest.GetByteMessage();
            rxpk.Data = Convert.ToBase64String(data);
            rxpk.Size = (uint)data.Length;

            byte[] decodedJoinRequestBytes = Convert.FromBase64String(rxpk.Data);
            var decodedJoinRequest = new LoRaTools.LoRaMessage.LoRaPayloadJoinRequest(decodedJoinRequestBytes);
            Assert.True(decodedJoinRequest.CheckMic(appKey));
        }

        /// <summary>
        /// Test the join request process
        /// </summary>
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
            TestRxpk(rxpk[0]);
        }

        private static void TestRxpk(Rxpk rxpk)
        {
            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk, out LoRaPayload loRaPayload));
            Assert.Equal(LoRaMessageType.JoinRequest, loRaPayload.LoRaMessageType);
            LoRaPayloadJoinRequest joinRequestMessage = (LoRaPayloadJoinRequest)loRaPayload;

            byte[] joinRequestAppKey = new byte[16]
            { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            var joinRequestBool = joinRequestMessage.CheckMic(ConversionHelper.ByteArrayToString(joinRequestAppKey));
            if (!joinRequestBool)
            {
                Console.WriteLine("Join Request type was not computed correclty");
            }

            byte[] joinRequestAppEui = new byte[8]
            { 1, 2, 3, 4, 1, 2, 3, 4 };

            byte[] joinRequestDevEUI = new byte[8]
           { 2, 3, 4, 5, 2, 3, 4, 5 };
            byte[] joinRequestDevNonce = new byte[2]
            { 16, 45 };

            Array.Reverse(joinRequestAppEui);
            Array.Reverse(joinRequestDevEUI);
            Array.Reverse(joinRequestDevNonce);
            Assert.True(joinRequestMessage.AppEUI.ToArray().SequenceEqual(joinRequestAppEui));
            Assert.True(joinRequestMessage.DevEUI.ToArray().SequenceEqual(joinRequestDevEUI));
            Assert.True(joinRequestMessage.DevNonce.ToArray().SequenceEqual(joinRequestDevNonce));
        }

        /// <summary>
        /// Test Confirmed Uplink Messages
        /// </summary>
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
            byte[] loRaPayloadUplinkNwkKey = new byte[16] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };

            Assert.True(loRaPayloadUplinkObj.CheckMic(ConversionHelper.ByteArrayToString(loRaPayloadUplinkNwkKey)));

            byte[] loRaPayloadUplinkAppKey = new byte[16] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            var key = ConversionHelper.ByteArrayToString(loRaPayloadUplinkAppKey);
            Assert.Equal("hello", Encoding.ASCII.GetString(loRaPayloadUplinkObj.PerformEncryption(key)));
        }

        /// <summary>
        /// Test a confirm data up message
        /// </summary>
        [Fact]
        public void TestConfirmedDataUp()
        {
            byte[] mhdr = new byte[1];
            mhdr[0] = 128;
            byte[] devAddr = new byte[4]
                { 4, 3, 2, 1 };

            byte[] fctrl = new byte[1]
            {
                0,
            };
            byte[] fcnt = new byte[2]
            { 0, 0 };
            byte[] fport = new byte[1]
            {
                    10,
            };
            byte[] frmPayload = new byte[4]
            {
               4, 3, 2, 1,
            };

            var nwkkey = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var appkey = new byte[16] { 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };

            LoRaPayloadData lora = new LoRaPayloadData(LoRaMessageType.ConfirmedDataUp, devAddr, fctrl, fcnt, null, fport, frmPayload, 0);
            lora.PerformEncryption(ConversionHelper.ByteArrayToString(appkey));
            byte[] testEncrypt = new byte[4]
            {
                226, 100, 212, 247,
            };
            Assert.Equal(testEncrypt, lora.Frmpayload.ToArray());
            lora.SetMic(ConversionHelper.ByteArrayToString(nwkkey));
            lora.CheckMic(ConversionHelper.ByteArrayToString(nwkkey));
            byte[] testMic = new byte[4]
            {
                181, 106, 14, 117,
            };
            Assert.Equal(testMic, lora.Mic.ToArray());
            var mess = lora.GetByteMessage();
            lora.ChangeEndianess();
            Assert.Equal(mess, lora.RawMessage);
        }

        /// <summary>
        /// Test keys
        /// </summary>
        [Fact]
        public void TestKeys()
        {
            // create random message
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
            Assert.Equal(LoRaMessageType.JoinRequest, loRaPayload.LoRaMessageType);
            var joinReq = (LoRaPayloadJoinRequest)loRaPayload;
            joinReq.DevAddr = new byte[4]
            {
                4, 3, 2, 1,
            };
            joinReq.DevEUI = new byte[8]
            {
                8, 7, 6, 5, 4, 3, 2, 1,
            };
            joinReq.DevNonce = new byte[2]
            {
                2, 1,
            };
            joinReq.AppEUI = new byte[8]
            {
                1, 2, 3, 4, 5, 6, 7, 8,
            };

            byte[] appNonce = new byte[3]
            {
                0, 0, 1,
            };
            byte[] netId = new byte[3]
            {
                3, 2, 1,
            };
            byte[] appKey = new byte[16]
            {
                1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8,
            };
            var key = joinReq.CalculateKey(LoRaPayloadKeyType.NwkSkey, appNonce, netId, joinReq.DevNonce.ToArray(), appKey);
            Assert.Equal(
                key,
                new byte[16] { 223, 83, 195, 95, 48, 52, 204, 206, 208, 255, 53, 76, 112, 222, 4, 223, });
        }

        /// <summary>
        /// This test will validate if creating payloads will work, using new approach
        ///  LoRaPayloadData (UnconfirmedDataUp) -> SerializeUplink -> Uplink.Rxpk[0] -> LoRaPayloadData -> check properties
        /// </summary>
        [Theory]
        [InlineData(LoRaMessageType.UnconfirmedDataUp, "1234")]
        [InlineData(LoRaMessageType.UnconfirmedDataUp, "hello world")]
        [InlineData(LoRaMessageType.ConfirmedDataUp, "1234")]
        [InlineData(LoRaMessageType.ConfirmedDataUp, "hello world")]
        public void When_Creating_Rxpk_Recreating_Payload_Should_Match_Source_Values(LoRaMessageType loRaMessageType, string data)
        {
            var devAddrText = "00000060";
            var appSKeyText = "00000060000000600000006000000060";
            var nwkSKeyText = "00000060000000600000006000000060";

            ushort fcnt = 12;
            byte[] devAddr = ConversionHelper.StringToByteArray(devAddrText);
            Array.Reverse(devAddr);
            byte[] fCtrl = new byte[] { 0x80 };
            var fcntBytes = BitConverter.GetBytes(fcnt);
            var fopts = new List<MacCommand>();
            byte[] fPort = new byte[] { 1 };
            byte[] payload = Encoding.UTF8.GetBytes(data);
            Array.Reverse(payload);

            // 0 = uplink, 1 = downlink
            int direction = 0;
            var devicePayloadData = new LoRaPayloadData(loRaMessageType, devAddr, fCtrl, fcntBytes, fopts, fPort, payload, direction);

            Assert.Equal(12, devicePayloadData.GetFcnt());
            Assert.Equal(0, devicePayloadData.Direction);
            Assert.Equal(1, devicePayloadData.GetFPort());

            var datr = "SF10BW125";
            var freq = 868.3;

            var uplinkMsg = devicePayloadData.SerializeUplink(appSKeyText, nwkSKeyText, datr, freq, 0);

            // Now try to recreate LoRaPayloadData from rxpk
            Assert.True(LoRaPayload.TryCreateLoRaPayload(uplinkMsg.Rxpk[0], out LoRaPayload parsedLoRaPayload));
            Assert.Equal(loRaMessageType, parsedLoRaPayload.LoRaMessageType);
            Assert.IsType<LoRaPayloadData>(parsedLoRaPayload);
            var parsedLoRaPayloadData = (LoRaPayloadData)parsedLoRaPayload;
            Assert.Equal(12, parsedLoRaPayloadData.GetFcnt());
            Assert.Equal(0, parsedLoRaPayloadData.Direction);
            Assert.Equal(1, parsedLoRaPayloadData.GetFPort());

            // Ensure that mic check and getting payload back works
            Assert.True(parsedLoRaPayloadData.CheckMic(nwkSKeyText)); // does not matter where the check mic happen, should always work!
            var parsedPayloadBytes = parsedLoRaPayloadData.GetDecryptedPayload(appSKeyText);
            Assert.Equal(data, Encoding.UTF8.GetString(parsedPayloadBytes));

            // checking mic and getting payload should not change the payload properties
            Assert.Equal(12, parsedLoRaPayloadData.GetFcnt());
            Assert.Equal(0, parsedLoRaPayloadData.Direction);
            Assert.Equal(1, parsedLoRaPayloadData.GetFPort());

            // checking mic should not break getting the payload
            Assert.True(parsedLoRaPayloadData.CheckMic(nwkSKeyText)); // does not matter where the check mic happen, should always work!
            var parsedPayloadBytes2 = parsedLoRaPayloadData.GetDecryptedPayload(appSKeyText);
            Assert.Equal(data, Encoding.UTF8.GetString(parsedPayloadBytes2));

            // checking mic and getting payload should not change the payload properties
            Assert.Equal(12, parsedLoRaPayloadData.GetFcnt());
            Assert.Equal(0, parsedLoRaPayloadData.Direction);
            Assert.Equal(1, parsedLoRaPayloadData.GetFPort());
        }

        /// <summary>
        /// Check Mic process
        /// </summary>
        [Theory]
        [InlineData("FBE5100000000004", "FBE5100000000004", "FBE51000000000000000000000000004")]
        public void When_Creating_Join_Request_From_Bytes_Should_Pass_Mic_Check(
            string appEUI,
            string devEUI,
            string appKey)
        {
            var rawJoinRequestBytes = new byte[] { 0, 4, 0, 0, 0, 0, 16, 229, 251, 4, 0, 0, 0, 0, 16, 229, 251, 254, 228, 147, 93, 188, 238 };
            var messageType = rawJoinRequestBytes[0];
            Assert.Equal((int)LoRaMessageType.JoinRequest, messageType);
            var joinRequest = new LoRaPayloadJoinRequest(rawJoinRequestBytes);
            Assert.NotNull(joinRequest);
            Assert.Equal(appEUI, joinRequest.GetAppEUIAsString());
            Assert.Equal(devEUI, joinRequest.GetDevEUIAsString());
            Assert.True(joinRequest.CheckMic(appKey));
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

            // create a join request
            var devNonce = ConversionHelper.StringToByteArray(devNonceText);
            Array.Reverse(devNonce);

            var join = new LoRaPayloadJoinRequest(appEUIText, devEUIText, devNonce);
            Assert.Equal(appEUIText, join.GetAppEUIAsString());
            Assert.Equal(devEUIText, join.GetDevEUIAsString());
            var uplinkMessage = join.SerializeUplink(appKeyText);

            Assert.False(join.CheckMic(wrongAppKeyText), "Mic check with wrong appKey should not pass");
            Assert.True(join.CheckMic(appKeyText), "Mic check should work after setting it");

            var rxpk = uplinkMessage.Rxpk[0];

            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk, out LoRaPayload parsedLoRaPayload));
            Assert.IsType<LoRaPayloadJoinRequest>(parsedLoRaPayload);
            var parsedLoRaJoinRequest = (LoRaPayloadJoinRequest)parsedLoRaPayload;

            Assert.True(parsedLoRaPayload.CheckMic(appKeyText), "Parsed join request should pass mic check with correct appKey");
            Assert.False(parsedLoRaJoinRequest.CheckMic(wrongAppKeyText), "Parsed join request should not pass mic check with wrong appKey");
        }

        [Fact]
        public void TestMultipleRxpks()
        {
            // create multiple Rxpk message
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

            Assert.Equal(3, rxpk.Count);
            TestRxpk(rxpk[0]);
            TestRxpk(rxpk[2]);

            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk[1], out LoRaPayload jsonUplinkUnconfirmedMessage));
            Assert.Equal(LoRaMessageType.UnconfirmedDataUp, jsonUplinkUnconfirmedMessage.LoRaMessageType);

            LoRaPayloadData loRaPayloadUplinkObj = (LoRaPayloadData)jsonUplinkUnconfirmedMessage;

            Assert.True(loRaPayloadUplinkObj.Fcnt.Span.SequenceEqual(new byte[2] { 1, 0 }));

            Assert.True(loRaPayloadUplinkObj.DevAddr.Span.SequenceEqual(new byte[4] { 1, 2, 3, 4 }));
            byte[] loRaPayloadUplinkNwkKey = new byte[16] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };

            Assert.True(loRaPayloadUplinkObj.CheckMic(ConversionHelper.ByteArrayToString(loRaPayloadUplinkNwkKey)));

            byte[] loRaPayloadUplinkAppKey = new byte[16] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            var key = ConversionHelper.ByteArrayToString(loRaPayloadUplinkAppKey);
            Assert.Equal("hello", Encoding.ASCII.GetString(loRaPayloadUplinkObj.PerformEncryption(key)));
        }
    }
}
