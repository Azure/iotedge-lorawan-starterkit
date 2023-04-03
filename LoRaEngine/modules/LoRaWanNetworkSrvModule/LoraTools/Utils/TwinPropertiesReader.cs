// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.Utils
{
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Extensions.Logging;

    public sealed class TwinPropertiesReader
    {
        private readonly ITwinProperties twinCollection;
        private readonly ILogger logger;

        public TwinPropertiesReader(ITwinProperties twinCollection, ILogger logger)
        {
            this.twinCollection = twinCollection;
            this.logger = logger;
        }

        public T? SafeRead<T>(string property, T? defaultValue = default)
            => this.twinCollection.SafeRead(property, defaultValue, this.logger);

        public bool TryRead<T>(string property, [NotNullWhen(true)] out T? value)
            => this.twinCollection.TryRead(property, this.logger, out value);
    }
}
