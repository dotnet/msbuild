// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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