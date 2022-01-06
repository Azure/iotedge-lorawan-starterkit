// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.Utils
{
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public sealed class TwinCollectionReader
    {
        private readonly TwinCollection twinCollection;
        private readonly ILogger logger;

        public TwinCollectionReader(TwinCollection twinCollection, ILogger logger)
        {
            this.twinCollection = twinCollection;
            this.logger = logger;
        }

        public T? SafeRead<T>(string property, T? defaultValue = default)
            => this.twinCollection.SafeRead(property, defaultValue, this.logger);

        public string ReadRequiredString(string property) =>
                this.twinCollection.ReadRequiredString(property, this.logger);

        public bool Contains(string propertyName) =>
            this.twinCollection.Contains(propertyName);

        public bool TryRead<T>(string property, [NotNullWhen(true)] out T? value)
            => this.twinCollection.TryRead(property, this.logger, out value);

        public bool TryParseJson<T>(string property, [NotNullWhen(true)] out T? value)
            => this.twinCollection.TryParseJson(property, this.logger, out value);
    }
}
