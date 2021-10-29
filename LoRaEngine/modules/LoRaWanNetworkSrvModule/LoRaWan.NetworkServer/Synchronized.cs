// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System.Collections.Generic;

    public sealed class Synchronized<T>
    {
        private readonly object mutex;
        private T value;

        public Synchronized(T value) : this(new object(), value) { }

        public Synchronized(object mutex, T value) =>
            (this.mutex, this.value) = (mutex, value);

        public T ReadDirty() => this.value;

        public T Read()
        {
            lock (this.mutex)
                return this.value;
        }

        public bool Write(T value)
        {
            lock (this.mutex)
            {
                if (EqualityComparer<T>.Default.Equals(this.value, value))
                    return false;
                this.value = value;
                return true;
            }
        }
    }
}
