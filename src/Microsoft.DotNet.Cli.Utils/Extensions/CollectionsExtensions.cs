using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class CollectionsExtensions
    {
        public static IEnumerable<T> OrEmptyIfNull<T>(this IEnumerable<T> enumerable)
        {
            return enumerable == null 
                ? Enumerable.Empty<T>()
                : enumerable;
        }
    }
}
