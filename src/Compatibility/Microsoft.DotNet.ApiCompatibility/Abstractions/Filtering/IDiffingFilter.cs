using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public interface IDiffingFilter
    {
        bool Include(ISymbol symbol);
    }
}
