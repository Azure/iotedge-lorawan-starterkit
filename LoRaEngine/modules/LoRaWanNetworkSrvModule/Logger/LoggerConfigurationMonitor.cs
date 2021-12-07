// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Logger
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    internal sealed class LoggerConfigurationMonitor : IDisposable
    {
        private readonly IDisposable onChangeToken;

        public LoRaLoggerConfiguration Configuration { get; private set; }
        public IExternalScopeProvider? ScopeProvider { get; private set; }

        public LoggerConfigurationMonitor(IOptionsMonitor<LoRaLoggerConfiguration> optionsMonitor)
        {
            if (optionsMonitor is null) throw new ArgumentNullException(nameof(optionsMonitor));

            this.onChangeToken = optionsMonitor.OnChange(UpdateConfiguration);
            UpdateConfiguration(optionsMonitor.CurrentValue);
        }

        public void Dispose()
        {
            this.onChangeToken.Dispose();
        }

        [MemberNotNull(nameof(Configuration))]
        private void UpdateConfiguration(LoRaLoggerConfiguration configuration)
        {
            Configuration = configuration;
            ScopeProvider = Configuration.UseScopes ? new LoggerExternalScopeProvider() : null;
        }
    }
}
