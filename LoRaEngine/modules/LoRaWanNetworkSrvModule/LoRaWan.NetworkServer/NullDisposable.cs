// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;

    /// <summary>
    /// Helper for a reusable <see cref="IDisposable"/>
    /// </summary>
    internal class NullDisposable : IDisposable
    {
        static NullDisposable instance = new NullDisposable();

        internal static IDisposable Instance => instance;

        private NullDisposable()
        {
        }

        void IDisposable.Dispose()
        {
            // do nothing
        }
    }
}