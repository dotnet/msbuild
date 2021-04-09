// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class MemberMapper : ElementMapper<ISymbol>
    {
        public MemberMapper(DiffingSettings settings) : base(settings) { }
    }
}
