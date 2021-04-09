using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

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
