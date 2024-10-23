// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Instance
{
    /// <inheritdoc />
    internal sealed class ImmutableValuedElementCollectionConverter<TCached, T> : ImmutableElementCollectionConverter<TCached, T>, IRetrievableValuedEntryHashSet<T>
        where T : class, IKeyed, IValued
        where TCached : IValued
    {
        public ImmutableValuedElementCollectionConverter(
            IDictionary<string, TCached> projectElements,
            IDictionary<(string, int, int), TCached> constrainedProjectElements,
            Func<TCached, T> convertElement)
            : base(projectElements, constrainedProjectElements, convertElement)
        {
        }

        public bool TryGetEscapedValue(string key, out string escapedValue)
        {
            if (_projectElements.TryGetValue(key, out TCached value) && value != null)
            {
                escapedValue = value.EscapedValue;
                return true;
            }

            escapedValue = null;
            return false;
        }
    }
}
