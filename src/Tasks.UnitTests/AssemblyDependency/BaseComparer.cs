using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Tasks.UnitTests.AssemblyDependency
{
    internal abstract class BaseComparer<T> : EqualityComparer<T>
    {
        protected bool CollectionEquals<TIn>(IEnumerable<TIn> c1, IEnumerable<TIn> c2, IEqualityComparer<TIn> equalityComparer)
        {
            if (c1 == null)
            {
                return c2 == null;
            }

            return c1.SequenceEqual(c2, equalityComparer);
        }

        public override int GetHashCode(T obj)
        {
            throw new NotSupportedException();
        }
    }
}
