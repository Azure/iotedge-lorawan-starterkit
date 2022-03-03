// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Extensions.Logging;
    using Moq;

    public sealed record LoggerLogInvocation(LogLevel LogLevel, EventId EventId, Exception? Exception, string Message);

    public static class LoggerMockExtensions
    {
        private static readonly MethodInfo? LogMethodInfo = typeof(ILogger).GetMethod(nameof(ILogger.Log));

        public static IEnumerable<LoggerLogInvocation> GetLogInvocations(this Mock<ILogger> mock)
        {
            ArgumentNullException.ThrowIfNull(mock);
            return mock.Invocations.GetLoggerLogInvocations();
        }

        public static IEnumerable<LoggerLogInvocation> GetLogInvocations<T>(this Mock<ILogger<T>> mock)
        {
            ArgumentNullException.ThrowIfNull(mock);
            return mock.Invocations.GetLoggerLogInvocations();
        }

        public static IEnumerable<LoggerLogInvocation> GetLoggerLogInvocations(this IInvocationList invocations)
        {
            ArgumentNullException.ThrowIfNull(invocations);

            return from i in invocations
                   where i.Method.IsGenericMethod && i.Method.GetGenericMethodDefinition() == LogMethodInfo
                   select i.Arguments into args
                   select new LoggerLogInvocation((LogLevel)args[0],
                                                  (EventId)args[1],
                                                  (Exception?)args[3],
                                                  (string)((Delegate)args[4]).DynamicInvoke(args[2], args[3])!);
        }
    }
}
