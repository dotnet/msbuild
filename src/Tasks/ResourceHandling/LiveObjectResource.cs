// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Resources;

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
            this.value = value;
        }

        public string Name { get; }
        public object value;

        public void AddTo(IResourceWriter writer)
        {
            writer.AddResource(Name, value);
        }
    }
}
