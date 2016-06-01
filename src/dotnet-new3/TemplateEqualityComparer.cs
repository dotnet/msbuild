using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace dotnet_new3
{
    internal class TemplateEqualityComparer : IEqualityComparer<ITemplate>
    {
        public static IEqualityComparer<ITemplate> Default { get; } = new TemplateEqualityComparer();

        public bool Equals(ITemplate x, ITemplate y)
        {
            return ReferenceEquals(x, y) || (x != null && y != null && string.Equals(x.Identity, y.Identity, StringComparison.Ordinal));
        }

        public int GetHashCode(ITemplate obj)
        {
            return obj?.Identity?.GetHashCode() ?? 0;
        }
    }
}