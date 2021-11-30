// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class TheoryDataFactory
    {
        public static IEnumerable<object[]> GetEnumMembers(Type type) =>
            from object v in Enum.GetValues(type)
            select new[] { v };
    }
}
