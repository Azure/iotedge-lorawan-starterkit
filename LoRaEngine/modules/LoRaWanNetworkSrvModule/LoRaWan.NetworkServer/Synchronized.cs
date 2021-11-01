// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System.Collections.Generic;

    /// <summary>
    /// Provides exclusive thread-synchronized access to a value.
    /// </summary>
    public sealed class Synchronized<T>
    {
        private readonly object mutex;
        private T value;

        public Synchronized(T value) : this(new object(), value) { }

        public Synchronized(object mutex, T value) =>
            (this.mutex, this.value) = (mutex, value);

        /// <summary>
        /// Reads the value without synchronized access.
        /// </summary>
        public T ReadDirty() => this.value;

        /// <summary>
        /// Reads the value after acquiring a mutual lock.
        /// </summary>
        public T Read()
        {
            lock (this.mutex)
                return this.value;
        }

        /// <summary>
        /// Writes the value after acquiring a mutual lock.
        /// </summary>
        /// <returns><c>true</c> if the value updated was different.</returns>
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
