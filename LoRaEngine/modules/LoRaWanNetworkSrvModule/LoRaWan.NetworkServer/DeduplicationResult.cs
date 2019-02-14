// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    public class DeduplicationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the message was a duplicate or not.
        /// Depending on the used strategy this will drive the decision.
        /// </summary>
        public bool IsDuplicate { get; set; }

        /// <summary>
        /// Gets or sets the GatewayId of the gateway that processed the message. If it is no
        /// duplicate, this will be set to the Id of the running gateway, otherwise it will
        /// be set ot the gateway that originally processed the message.
        /// </summary>
        public string GatewayId { get; set; }

        /// <summary>
        /// Gets or sets the clientFcntDown. It will only be set if the original request
        /// to the function actually contained a clientFcntDown otherwise it will be null.
        /// This can be used to combine the 2 function calls of deduplication & fcnt down calculation
        /// </summary>
        public int? ClientFCntDown { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether we can process this message.
        /// </summary>
        public bool CanProcess { get; set; }
    }
}
