// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;

    public sealed class EventArgs<T> : EventArgs
    {
        public T Value { get; }

        public EventArgs(T value) => Value = value;
    }
}
