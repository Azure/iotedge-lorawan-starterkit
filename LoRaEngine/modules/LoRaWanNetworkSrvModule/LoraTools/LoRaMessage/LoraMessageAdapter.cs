using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.LoRaMessage
{
    public enum LoRaMessageAdapterEnum
    {
        JoinRequest,
        JoinAccept,
        Data
    }

    /// <summary>
    /// The Message adapter class is a class enabling to get the internals from a Join accept/request or data payload.
    /// </summary>
    public class LoRaMessageAdapter
    {
        public LoRaMessageAdapterEnum LoRaMessageAdapterEnum { get; set; }
        #region JoinAccept
        /// <summary>
        /// Server Nonce aka JoinNonce
        /// </summary>
        public Memory<byte> AppNonce { get; set; }

        /// <summary>
        /// Device home network aka Home_NetId
        /// </summary>
        public Memory<byte> NetID { get; set; }

        /// <summary>
        /// DLSettings
        /// </summary>
        public Memory<byte> DlSettings { get; set; }

        /// <summary>
        /// RxDelay
        /// </summary>
        public Memory<byte> RxDelay { get; set; }

        /// <summary>
        /// CFList / Optional
        /// </summary>
        public Memory<byte> CfList { get; set; }

        /// <summary>
        /// Frame Counter
        /// </summary>
        public Memory<byte> Fcnt { get; set; }

        #endregion

        #region joinrequest
        /// <summary>
        /// aka JoinEUI
        /// </summary>
        public Memory<byte> AppEUI { get; set; }
        public Memory<byte> DevEUI { get; set; }
        public Memory<byte> DevNonce { get; set; }
        #endregion

        #region Message
        /// <summary>
        /// Frame control octet
        /// </summary>
        public Memory<byte> Fctrl { get; set; }
        /// <summary>
        /// Optional frame
        /// </summary>
        public Memory<byte> Fopts { get; set; }
        /// <summary>
        /// Port field
        /// </summary>
        public Memory<byte> Fport { get; set; }
        /// <summary>
        /// MAC Frame Payload Encryption 
        /// </summary>
        public Memory<byte> Frmpayload { get; set; }
        /// <summary>
        /// get message direction
        /// </summary>
        public int Direction { get; set; }

        //fcnt also here
        #endregion

        public LoRaMessageAdapter(LoRaPayload loRaGenericPayload)
        {
           if(loRaGenericPayload.GetType() == typeof(LoRaPayloadJoinAccept))
            {
                LoRaMessageAdapterEnum = LoRaMessageAdapterEnum.JoinAccept;
                AppNonce = ((LoRaPayloadJoinAccept)loRaGenericPayload).AppNonce;
                CfList = ((LoRaPayloadJoinAccept)loRaGenericPayload).CfList;
                DlSettings = ((LoRaPayloadJoinAccept)loRaGenericPayload).DlSettings;
                Fcnt = ((LoRaPayloadJoinAccept)loRaGenericPayload).Fcnt;
                NetID = ((LoRaPayloadJoinAccept)loRaGenericPayload).NetID;
                RxDelay = ((LoRaPayloadJoinAccept)loRaGenericPayload).RxDelay;
            }
            else if (loRaGenericPayload.GetType() == typeof(LoRaPayloadJoinRequest))
            {
                LoRaMessageAdapterEnum = LoRaMessageAdapterEnum.JoinRequest;
                AppEUI = ((LoRaPayloadJoinRequest)loRaGenericPayload).AppEUI;
                DevEUI = ((LoRaPayloadJoinRequest)loRaGenericPayload).DevEUI;
                DevNonce = ((LoRaPayloadJoinRequest)loRaGenericPayload).DevNonce;
            }
            else if (loRaGenericPayload.GetType() == typeof(LoRaPayloadData))
            {
                LoRaMessageAdapterEnum = LoRaMessageAdapterEnum.Data;
                Direction = ((LoRaPayloadData)loRaGenericPayload).Direction;
                Fcnt = ((LoRaPayloadData)loRaGenericPayload).Fcnt;
                Fctrl = ((LoRaPayloadData)loRaGenericPayload).Fctrl;
                Fopts = ((LoRaPayloadData)loRaGenericPayload).Fopts;
                Fport = ((LoRaPayloadData)loRaGenericPayload).Fport;
                Frmpayload = ((LoRaPayloadData)loRaGenericPayload).Frmpayload;

            }
            
        }
    }
}
