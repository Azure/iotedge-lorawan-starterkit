// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using System.Linq.Expressions;

    static class LambdaCompiler<T>
    {
        public static Func<T, T, bool> Binary(Func<ParameterExpression, ParameterExpression, BinaryExpression> f)
        {
            var a = Expression.Parameter(typeof(T));
            var b = Expression.Parameter(typeof(T));
            return Expression.Lambda<Func<T, T, bool>>(f(a, b), a, b).Compile();
        }
    }
}
