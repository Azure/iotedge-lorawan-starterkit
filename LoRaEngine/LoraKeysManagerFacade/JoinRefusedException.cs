// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;

    public class JoinRefusedException : Exception
    {
        public JoinRefusedException(string message)
            : base(message)
        {
        }
    }
}
