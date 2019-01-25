// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    public abstract class GenericMACCommand
    {
        /// <summary>
        /// Gets or sets cid number of
        /// </summary>
        public CidEnum Cid { get; set; }

        public int Length { get; set; }

        public override abstract string ToString();

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericMACCommand"/> class.
        /// create
        /// </summary>
        public GenericMACCommand()
        {
        }

        public CidEnum GetMacType()
        {
            return this.Cid;
        }

        public abstract byte[] ToBytes();
    }
}
