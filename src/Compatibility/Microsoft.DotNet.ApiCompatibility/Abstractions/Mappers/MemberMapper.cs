using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class MemberMapper : ElementMapper<ISymbol>
    {
        public MemberMapper(DiffingSettings settings) : base(settings) { }
    }
}
