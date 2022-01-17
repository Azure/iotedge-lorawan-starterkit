// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    public static class SearchByDevEuiContract
    {
        public static readonly string Eui = new DevEui(0x1b2c3d).ToString();
        public static readonly string PrimaryKey = "ABCDEFGH1234567890";
        public static readonly string Response = JsonUtil.Strictify(@"{ DevEUI: '00000000001B2C3D',
                                                                        PrimaryKey: 'QUJDREVGR0gxMjM0NTY3ODkw' }");
    }
}
