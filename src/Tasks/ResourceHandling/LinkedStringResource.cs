// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Tasks.ResourceHandling
{
    /// <summary>
    /// A <see cref="StringResource"/> that is backed by a linked file
    /// (originating from a <c>ResXFileRef</c> entry).
    /// </summary>
    internal class LinkedStringResource : StringResource, ILinkedFileResource
    {
        public string LinkedFilePath { get; }

        public LinkedStringResource(string name, string value, string filename, string linkedFilePath)
            : base(name, value, filename)
        {
            LinkedFilePath = linkedFilePath;
        }
    }
}
