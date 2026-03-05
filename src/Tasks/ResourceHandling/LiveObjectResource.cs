// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Resources;

#nullable disable

namespace Microsoft.Build.Tasks.ResourceHandling
{
    /// <summary>
    /// Name value resource pair to go in resources list
    /// </summary>
    internal class LiveObjectResource : IResource
    {
        public LiveObjectResource(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public object Value { get; }

        public string TypeAssemblyQualifiedName => Value.GetType().AssemblyQualifiedName;

        public string TypeFullName => Value.GetType().FullName;

        public void AddTo(IResourceWriter writer)
        {
            writer.AddResource(Name, Value);
        }
    }
}
