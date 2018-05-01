using System;
using System.Text;


namespace LoRaTools
{
    /// <summary>
    /// IPacket represents the raw byte representation of a LoRaWan device
    /// packet. The interface mainly exists to facilitate tests that simulate
    /// LoRaWan devices. Simulation is important for unit tests that run in
    /// a CI environment.
    /// </summary>
    public interface IPacket
    {
        // Return the packet bytes as they would be received over the air.
        byte[] GetRawWireBytes();
    }


    /// <summary>
    /// RecordedPacket is an IPacket that is constructed from raw bytes
    /// as received over the air.
    /// </summary>
    public class RecordedPacket : IPacket
    {
        byte[] m_rawBytes;

        public RecordedPacket(string packet)
        {
            m_rawBytes = Encoding.UTF8.GetBytes(packet);
        }

        byte[] IPacket.GetRawWireBytes()
        {
            return m_rawBytes;
        }
    }


    /// <summary>
    /// SyntheticPacket is an IPacket that is synthesized from logical parts
    /// like the device address, network key, application key, frameCounter,
    /// and payload.
    /// </summary>
    public class SyntheticPacket : RecordedPacket
    {
        public SyntheticPacket(
            int devAddr,
            Int64 nwkskey,
            Int64 appsKey,
            Int16 frameCounter, 
            byte [] payload)
            : base(null)
        {
            // TODO: using constructor parameters, build and initialize
            // m_rawBytes here.
            
            // TODO: consider adding methods that allow the specification of
            // various JSON attributes like time, tmms, tmst, freq, etc.
            // There are too many of these specify via constructor parameters,
            // so the model would be
            //    1. Constructor initializes JSON parameters to default values.
            //    2. User then calls methods to specify new values for some items.
            //    3. GetRawWireBytes() method computes bytes on demand, instead
            //       relying on cached value.

            throw new System.NotImplementedException("SyntheticPacket not implemented.");
        }
    }


    /// <summary>
    /// PrerecordedPackets provides a static method that returns one of a
    /// number of pre-recorded packets, provided for testing purposes.
    /// </summary>
    public class PrerecordedPackets
    {
        static string[] m_packets;

        static PrerecordedPackets()
        {
            m_packets = new string[]
            {
                "0205DB00AA555A0000000101{\"rxpk\":[{ \"tmst\":2166390139,\"chan\":0,\"rfch\":1,\"freq\":868.100000,\"stat\":1,\"modu\":\"LORA\",\"datr\":\"SF7BW125\",\"codr\":\"4/5\",\"lsnr\":9.5,\"rssi\":-24,\"size\":18,\"data\":\"QEa5KADAQwAIwahYNa9zWAn1\"}]}",
                "0205DB00AA555A0000000101{\"rxpk\":[{\"tmst\":2185426075,\"chan\":1,\"rfch\":1,\"freq\":868.300000,\"stat\":1,\"modu\":\"LORA\",\"datr\":\"SF7BW125\",\"codr\":\"4/5\",\"lsnr\":9.5,\"rssi\":-21,\"size\":20,\"data\":\"QEa5KADARAAIK4BzkmhMRZOrOOk=\"}]}",
                "0205DB00AA555A0000000101{\"rxpk\":[{\"tmst\":2195955763,\"chan\":2,\"rfch\":1,\"freq\":868.500000,\"stat\":1,\"modu\":\"LORA\",\"datr\":\"SF7BW125\",\"codr\":\"4/5\",\"lsnr\":7.5,\"rssi\":-22,\"size\":20,\"data\":\"QEa5KADARQAIlxv5SG8raslJeKk=\"}]}"
            };
        }

        /// <summary>
        /// Returns the pre-recorded packet specified by parameter n.
        /// 
        /// Packets 0, 1, and 2 form a sequence of packets from broadcast
        /// from the same device.
        /// 
        ///    DevAddr: 0028B946 
        ///    NwkSKey: 2B7E151628AED2A6ABF7158809CF4F3C
        ///    AppSKey: 2B7E151628AED2A6ABF7158809CF4F3C
        ///    FRMPayload: 
        ///      Packet 0: 323A313030  (decrypted)
        ///      Packet 1: 3138353A313030 (decrypted)
        ///      Packet 2: 3139323A313030 (decrypted)
        ///    FCnt:
        ///      Packet 0: 67
        ///      Packet 1: 68
        ///      Packet 2: 69
        ///
        /// Packets can be decoded with the following links:
        ///    https://lorawan-packet-decoder-0ta6puiniaut.runkit.sh/?data=QEa5KADAQwAIwahYNa9zWAn1&nwkskey=2B7E151628AED2A6ABF7158809CF4F3C&appskey=2B7E151628AED2A6ABF7158809CF4F3C
        ///    https://lorawan-packet-decoder-0ta6puiniaut.runkit.sh/?data=QEa5KADARAAIK4BzkmhMRZOrOOk%3D&nwkskey=2B7E151628AED2A6ABF7158809CF4F3C&appskey=2B7E151628AED2A6ABF7158809CF4F3C
        ///    https://lorawan-packet-decoder-0ta6puiniaut.runkit.sh/?data=QEa5KADARQAIlxv5SG8raslJeKk%3D&nwkskey=2B7E151628AED2A6ABF7158809CF4F3C&appskey=2B7E151628AED2A6ABF7158809CF4F3C
        ///
        /// </summary>
        /// <param name="n">Specifies the recorded packet to return. Current legal values are 0, 1, and 2.</param>
        /// <returns>IPacket representing the recorded packet.</returns>
        public static IPacket GetPacket(int n)
        {
            if (n < 0 || n >= m_packets.Length)
            {
                throw new System.ArgumentException("Packet number out of range.");
            }
            return new RecordedPacket(m_packets[n]);
        }

        public static int GetPacketCount()
        {
            return m_packets.Length;
        }
    }
}
