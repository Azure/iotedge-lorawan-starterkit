// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// Defines a contract for properties that have tracking enabled
    /// </summary>
    public interface IChangeTrackingProperty
    {
        bool IsDirty();

        string PropertyName { get; }

        object Value { get; }

        void AcceptChanges();

        void Rollback();
    }
}
