// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Instance
{
    internal class ImmutablePropertyCollectionConverter<TCached, T> : ImmutableElementCollectionConverter<TCached, T>, ICopyOnWritePropertyDictionary<T>
        where T : class, IKeyed, IValued, IEquatable<T>, IImmutable
        where TCached : class, IValued, IEquatable<TCached>
    {
        public ImmutablePropertyCollectionConverter(IDictionary<string, TCached> properties, Func<TCached, T> convertProperty)
            : base(properties, constrainedProjectElements: null, convertProperty)
        {
        }

        public bool Contains(string name) => ContainsKey(name);

        public string? GetEscapedValue(string name)
        {
            if (_projectElements.TryGetValue(name, out TCached? value))
            {
                return value?.EscapedValue;
            }

            return null;
        }

        public ICopyOnWritePropertyDictionary<T> DeepClone() => this;

        public void ImportProperties(IEnumerable<T> other) => throw new NotSupportedException();

        public void Set(T projectProperty) => throw new NotSupportedException();

        public bool Equals(ICopyOnWritePropertyDictionary<T>? other)
        {
            if (other == null || Count != other.Count)
            {
                return false;
            }

            if (other is ImmutablePropertyCollectionConverter<TCached, T> otherImmutableDict)
            {
                // When comparing to another CollectionConverter we compare the TCached values
                // in order to avoid causing the instantiation of each T instance.
                foreach (var propKvp in _projectElements)
                {
                    if (!otherImmutableDict._projectElements.TryGetValue(propKvp.Key, out TCached? otherProperty) ||
                        !EqualityComparer<TCached>.Default.Equals(propKvp.Value, otherProperty))
                    {
                        return false;
                    }
                }
            }
            else
            {
                foreach (T thisProp in Values)
                {
                    if (!other.TryGetValue(thisProp.Key, out T? thatProp) ||
                        !EqualityComparer<T>.Default.Equals(thisProp, thatProp))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
