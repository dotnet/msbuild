// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public abstract class Rule
    {
        public virtual void Run(AssemblyMapper mapper, List<CompatDifference> differences)
        {
        }
        public virtual void Run(TypeMapper mapper, List<CompatDifference> differences)
        {
        }
        public virtual void Run(MemberMapper mapper, List<CompatDifference> differences)
        {
        }
    }
}
