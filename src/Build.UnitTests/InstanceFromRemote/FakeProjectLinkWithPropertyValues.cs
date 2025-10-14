// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Engine.UnitTests.InstanceFromRemote
{
    /// <summary>
    /// This is a fake implementation of ProjectLink that can return property values for testing.
    /// </summary>
    internal sealed class FakeProjectLinkWithPropertyValues : FakeProjectLink
    {
        private readonly Dictionary<string, string> _propertyValues;

        public FakeProjectLinkWithPropertyValues(string path, Dictionary<string, string> propertyValues)
            : base(path)
        {
            _propertyValues = propertyValues ?? throw new ArgumentNullException(nameof(propertyValues));
        }

        public override string GetPropertyValue(string name)
        {
            if (_propertyValues.TryGetValue(name, out string? value))
            {
                return value;
            }

            return string.Empty;
        }
    }
}
