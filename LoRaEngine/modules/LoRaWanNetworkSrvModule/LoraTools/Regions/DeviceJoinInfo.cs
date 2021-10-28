namespace LoRaTools.Regions
{
    public class DeviceJoinInfo
    {
        public DeviceJoinInfo(int? reportedCN470JoinChannel = null, int? desiredCN470JoinChannel = null)
        {
            ReportedCN470JoinChannel = reportedCN470JoinChannel;
            DesiredCN470JoinChannel = desiredCN470JoinChannel;
        }

        /// <summary>
        /// Gets the join channel for the device based on reported properties.
        /// Relevant only for region CN470.
        /// </summary>
        public int? ReportedCN470JoinChannel { get; }

        /// <summary>
        /// Gets the join channel for the device based on desired properties.
        /// Relevant only for region CN470.
        /// </summary>
        public int? DesiredCN470JoinChannel { get; }
    }
}
