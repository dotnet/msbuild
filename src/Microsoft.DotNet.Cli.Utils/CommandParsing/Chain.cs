// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.DotNet.Cli.Utils.CommandParsing
{
    internal struct Chain<TLeft, TDown>
    {
        public Chain(TLeft left, TDown down)
            : this()
        {
            Left = left;
            Down = down;
        }

        public readonly TLeft Left;
        public readonly TDown Down;
    }
}