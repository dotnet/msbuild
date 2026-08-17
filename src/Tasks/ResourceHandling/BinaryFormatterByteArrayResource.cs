// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Resources;
using System.Resources.Extensions;

#nullable disable

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal class BinaryFormatterByteArrayResource : IResource
    {
        public string Name { get; }
        public string OriginatingFile { get; }
        public byte[] Bytes { get; }

        /// <summary>
        /// BinaryFormatter byte arrays contain the type name, but it is not directly accessible from the resx.
        /// </summary>
        public string TypeAssemblyQualifiedName => null;

        /// <summary>
        /// BinaryFormatter byte arrays contain the type name, but it is not directly accessible from the resx.
        /// </summary>
        public string TypeFullName => null;

        public BinaryFormatterByteArrayResource(string name, byte[] bytes, string originatingFile)
        {
            Name = name;
            Bytes = bytes;
            OriginatingFile = originatingFile;
        }

        public void AddTo(IResourceWriter writer)
        {
            if (writer is PreserializedResourceWriter preserializedResourceWriter)
            {
                preserializedResourceWriter.AddBinaryFormattedResource(Name, Bytes);
            }
            else
            {
                throw new PreserializedResourceWriterRequiredException();
            }
        }
    }
}
