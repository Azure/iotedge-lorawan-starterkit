// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IClientCertificateValidatorService
    {
        Task<bool> ValidateAsync(X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors, CancellationToken token);
    }
}
