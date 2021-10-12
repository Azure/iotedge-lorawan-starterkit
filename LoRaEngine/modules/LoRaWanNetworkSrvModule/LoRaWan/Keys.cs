// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    /// <summary>
    /// Data encryption key used to "encode" the messages between the end nodes and the Application
    /// Server.
    /// </summary>
    public partial struct AppKey { }

    /// <summary>
    /// Data encryption key (AppSKey) used for encryption and decryption of payload.
    /// </summary>
    public partial struct AppSessionKey { }

    /// <summary>
    /// Data encryption key (NwkSKey) used to "encode" the messages between the end nodes and the
    /// Network Server.
    /// </summary>
    public partial struct NetworkSessionKey { }
}
