// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1303 // Do not pass literals as localized parameters

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Linq;
    using System.Text;
    using global::LoRaTools.LoRaMessage;
    using global::LoRaTools.LoRaPhysical;
    using global::LoRaTools.Utils;
    using LoRaWan.Tests.Common;
    using Xunit;

    /// <summary>
    /// all these test cases were inspired from https://github.com/brocaar/lora-app-server.
    /// </summary>
    public class LoRaLibraryTest
    {
        /// <summary>
        /// Test Join Accept messages.
        /// </summary>
        [Fact]
        public void TestJoinAccept()
        {
            var appNonce = new byte[3]
            {
                87, 11, 199,
            };
            var netId1 = new byte[3]
            {
                34, 17, 1,
            };
            var devAddr = new byte[4]
            {
                2, 3, 25, 128,
            };

            var netId = ConversionHelper.ByteArrayToString(netId1);
            var appkey = "00112233445566778899AABBCCDDEEFF";
            var devEui = "AABBCCDDDDCCBBAA";
            var joinAccept = new LoRaPayloadJoinAccept(netId, devAddr, appNonce, new byte[] { 0 }, 0, null);
            var joinacceptbyte = joinAccept.Serialize(appkey, "SF10BW125", Hertz.Mega(866.349812), devEui, 10000);
            var decodedJoinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(joinacceptbyte.Txpk.Data), appkey);
            var joinAcceptMic = new byte[4]
            {
                67, 72, 91, 188,
            };

            Assert.True(decodedJoinAccept.Mic.ToArray().SequenceEqual(joinAcceptMic));
            var msg = ConversionHelper.ByteArrayToString(Convert.FromBase64String(joinacceptbyte.Txpk.Data));
            Assert.Equal("20493EEB51FBA2116F810EDB3742975142", msg);
        }

        /// <summary>
        /// Test mic Check.
        /// </summary>
        [Fact]
        public void JoinRequest_Should_Succeed_Mic_Check()
        {
            var appEUIText = "0005100000000004";
            var devEUIText = "0005100000000004";

            var devNonce = new DevNonce(0xABCD);
            var appKey = "00000000000000000005100000000004";

            var joinRequest = new LoRaPayloadJoinRequest(appEUIText, devEUIText, devNonce);
            joinRequest.SetMic(appKey);
            Assert.True(joinRequest.CheckMic(appKey));
            Assert.True(joinRequest.CheckMic(appKey)); // ensure multiple calls work!

            var rxpk = new Rxpk()
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

            var decodedJoinRequestBytes = Convert.FromBase64String(rxpk.Data);
            var decodedJoinRequest = new global::LoRaTools.LoRaMessage.LoRaPayloadJoinRequest(decodedJoinRequestBytes);
            Assert.True(decodedJoinRequest.CheckMic(appKey));
        }

        /// <summary>
        /// Test the join request process.
        /// </summary>
        [Fact]
        public void TestJoinRequest()
        {
            var physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            var jsonUplink = @"{ ""rxpk"":[
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
            var rxpk = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(joinRequestInput).ToArray());
            TestRxpk(rxpk[0]);
        }

        private static void TestRxpk(Rxpk rxpk)
        {
            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk, out var loRaPayload));
            Assert.Equal(MacMessageType.JoinRequest, loRaPayload.MessageType);
            var joinRequestMessage = (LoRaPayloadJoinRequest)loRaPayload;

            var joinRequestAppKey = new byte[16]
            {
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
            };
            var joinRequestBool = joinRequestMessage.CheckMic(ConversionHelper.ByteArrayToString(joinRequestAppKey));
            if (!joinRequestBool)
            {
                Console.WriteLine("Join Request type was not computed correclty");
            }

            var joinRequestAppEui = new byte[8]
            {
                1, 2, 3, 4, 1, 2, 3, 4
            };

            var joinRequestDevEUI = new byte[8]
            {
               2, 3, 4, 5, 2, 3, 4, 5
            };
            var expectedJoinRequestDevNonce = DevNonce.Read(new byte[2] { 45, 16 });

            Array.Reverse(joinRequestAppEui);
            Array.Reverse(joinRequestDevEUI);
            Assert.True(joinRequestMessage.AppEUI.ToArray().SequenceEqual(joinRequestAppEui));
            Assert.True(joinRequestMessage.DevEUI.ToArray().SequenceEqual(joinRequestDevEUI));
            Assert.Equal(expectedJoinRequestDevNonce, joinRequestMessage.DevNonce);
        }

        /// <summary>
        /// Test Confirmed Uplink Messages.
        /// </summary>
        [Fact]
        public void TestUnconfirmedUplink()
        {
            var jsonUplinkUnconfirmedDataUp = @"{ ""rxpk"":[
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

            var physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;

            var jsonUplinkUnconfirmedDataUpBytes = Encoding.Default.GetBytes(jsonUplinkUnconfirmedDataUp);
            var rxpk = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(jsonUplinkUnconfirmedDataUpBytes).ToArray());
            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk[0], out var loRaPayload));

            Assert.Equal(MacMessageType.UnconfirmedDataUp, loRaPayload.MessageType);

            var loRaPayloadUplinkObj = (LoRaPayloadData)loRaPayload;
            Assert.True(loRaPayloadUplinkObj.Fcnt.Span.SequenceEqual(new byte[2] { 1, 0 }));

            Assert.True(loRaPayloadUplinkObj.DevAddr.Span.SequenceEqual(new byte[4] { 1, 2, 3, 4 }));
            var loRaPayloadUplinkNwkKey = new byte[16] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };

            Assert.True(loRaPayloadUplinkObj.CheckMic(ConversionHelper.ByteArrayToString(loRaPayloadUplinkNwkKey)));

            var loRaPayloadUplinkAppKey = new byte[16] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            var key = ConversionHelper.ByteArrayToString(loRaPayloadUplinkAppKey);
            Assert.Equal("hello", Encoding.ASCII.GetString(loRaPayloadUplinkObj.PerformEncryption(key)));
        }

        /// <summary>
        /// Test a confirm data up message.
        /// </summary>
        [Fact]
        public void TestConfirmedDataUp()
        {
            var mhdr = new byte[1];
            mhdr[0] = 128;
            var devAddr = new byte[4]
                {
                    4, 3, 2, 1
                };

            var fcnt = new byte[2]
            {
                0, 0
            };
            const FramePort fport = (FramePort)10;
            var frmPayload = new byte[4]
            {
               4, 3, 2, 1,
            };

            var nwkkey = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var appkey = new byte[16] { 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };

            var lora = new LoRaPayloadData(MacMessageType.ConfirmedDataUp, devAddr, FrameControlFlags.None, fcnt, null, fport, frmPayload, 0);
            _ = lora.PerformEncryption(ConversionHelper.ByteArrayToString(appkey));
            var testEncrypt = new byte[4]
            {
                226, 100, 212, 247,
            };
            Assert.Equal(testEncrypt, lora.Frmpayload.ToArray());
            lora.SetMic(ConversionHelper.ByteArrayToString(nwkkey));
            _ = lora.CheckMic(ConversionHelper.ByteArrayToString(nwkkey));
            var testMic = new byte[4]
            {
                181, 106, 14, 117,
            };
            Assert.Equal(testMic, lora.Mic.ToArray());
            var mess = lora.GetByteMessage();
            lora.ChangeEndianess();
            Assert.Equal(mess, lora.RawMessage);
        }

        /// <summary>
        /// Test keys.
        /// </summary>
        [Fact]
        public void TestKeys()
        {
            // create random message
            var jsonUplink = @"{ ""rxpk"":[
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
            var physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            var rxpk = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(joinRequestInput).ToArray());
            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk[0], out var loRaPayload));
            Assert.Equal(MacMessageType.JoinRequest, loRaPayload.MessageType);
            var joinReq = (LoRaPayloadJoinRequest)loRaPayload;
            joinReq.DevAddr = new byte[4]
            {
                4, 3, 2, 1,
            };
            joinReq.DevEUI = new byte[8]
            {
                8, 7, 6, 5, 4, 3, 2, 1,
            };
            joinReq.DevNonce = DevNonce.Read(new byte[2] { 2, 1 });
            joinReq.AppEUI = new byte[8]
            {
                1, 2, 3, 4, 5, 6, 7, 8,
            };

            var appNonce = new byte[3]
            {
                0, 0, 1,
            };
            var netId = new byte[3]
            {
                3, 2, 1,
            };
            var appKey = new byte[16]
            {
                1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8,
            };
            var key = LoRaPayload.CalculateKey(LoRaPayloadKeyType.NwkSkey, appNonce, netId, joinReq.DevNonce, appKey);
            Assert.Equal(
                key,
                new byte[16] { 223, 83, 195, 95, 48, 52, 204, 206, 208, 255, 53, 76, 112, 222, 4, 223, });
        }

        [Fact]
        public void CalculateKey_Throws_When_Key_Type_Is_None()
        {
            var result = Assert.Throws<InvalidOperationException>(() => LoRaPayload.CalculateKey(LoRaPayloadKeyType.None,
                                                                                                 Array.Empty<byte>(),
                                                                                                 Array.Empty<byte>(),
                                                                                                 new DevNonce(),
                                                                                                 Array.Empty<byte>()));
            Assert.Equal("No key type selected.", result.Message);
        }


        /// <summary>
        /// Check Mic process.
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
            Assert.Equal((int)MacMessageType.JoinRequest, messageType);
            var joinRequest = new LoRaPayloadJoinRequest(rawJoinRequestBytes);
            Assert.NotNull(joinRequest);
            Assert.Equal(appEUI, joinRequest.GetAppEUIAsString());
            Assert.Equal(devEUI, joinRequest.GetDevEUIAsString());
            Assert.True(joinRequest.CheckMic(appKey));
        }

        // When creating a join request using simulated devices, rebuilding it should pass the mic check
        [Theory]
        [InlineData("000000000000AABB", "0000000000001111", "00000000000000000000000000002222", 0x6050)]
        [InlineData("0000000000000001", "0000000000000001", "00000000000000000000000000000001", 0x9FC3)]
        public void When_Creating_Join_Request_Recreating_Should_Pass_Mic_Check(
            string appEUIText,
            string devEUIText,
            string appKeyText,
            ushort devNonceRawValue)
        {
            var wrongAppKeyText = "00000000000000000000000000003333";

            // create a join request
            var devNonce = new DevNonce(devNonceRawValue);

            var join = new LoRaPayloadJoinRequest(appEUIText, devEUIText, devNonce);
            Assert.Equal(appEUIText, join.GetAppEUIAsString());
            Assert.Equal(devEUIText, join.GetDevEUIAsString());
            var uplinkMessage = join.SerializeUplink(appKeyText);

            Assert.False(join.CheckMic(wrongAppKeyText), "Mic check with wrong appKey should not pass");
            Assert.True(join.CheckMic(appKeyText), "Mic check should work after setting it");

            var rxpk = uplinkMessage.Rxpk[0];

            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk, out var parsedLoRaPayload));
            _ = Assert.IsType<LoRaPayloadJoinRequest>(parsedLoRaPayload);
            var parsedLoRaJoinRequest = (LoRaPayloadJoinRequest)parsedLoRaPayload;

            Assert.True(parsedLoRaPayload.CheckMic(appKeyText), "Parsed join request should pass mic check with correct appKey");
            Assert.False(parsedLoRaJoinRequest.CheckMic(wrongAppKeyText), "Parsed join request should not pass mic check with wrong appKey");
        }

        [Fact]
        public void TestMultipleRxpks()
        {
            // create multiple Rxpk message
            var jsonUplink = @"{""rxpk"":[
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
            var physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            var rxpk = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(multiRxpkInput).ToArray());

            Assert.Equal(3, rxpk.Count);
            TestRxpk(rxpk[0]);
            TestRxpk(rxpk[2]);

            Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk[1], out var jsonUplinkUnconfirmedMessage));
            Assert.Equal(MacMessageType.UnconfirmedDataUp, jsonUplinkUnconfirmedMessage.MessageType);

            var loRaPayloadUplinkObj = (LoRaPayloadData)jsonUplinkUnconfirmedMessage;

            Assert.True(loRaPayloadUplinkObj.Fcnt.Span.SequenceEqual(new byte[2] { 1, 0 }));

            Assert.True(loRaPayloadUplinkObj.DevAddr.Span.SequenceEqual(new byte[4] { 1, 2, 3, 4 }));
            var loRaPayloadUplinkNwkKey = new byte[16] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };

            Assert.True(loRaPayloadUplinkObj.CheckMic(ConversionHelper.ByteArrayToString(loRaPayloadUplinkNwkKey)));

            var loRaPayloadUplinkAppKey = new byte[16] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            var key = ConversionHelper.ByteArrayToString(loRaPayloadUplinkAppKey);
            Assert.Equal("hello", Encoding.ASCII.GetString(loRaPayloadUplinkObj.PerformEncryption(key)));
        }
    }
}
