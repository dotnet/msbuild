// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Tasks.ResourceHandling
{
    /// <summary>
    /// A <see cref="LiveObjectResource"/> that is backed by a linked file
    /// (originating from a <c>ResXFileRef</c> entry).
    /// </summary>
    internal class LinkedLiveObjectResource : LiveObjectResource, ILinkedFileResource
    {
        public string LinkedFilePath { get; }

        public LinkedLiveObjectResource(string name, object value, string linkedFilePath)
            : base(name, value)
        {
            LinkedFilePath = linkedFilePath;
        }
    }
}
