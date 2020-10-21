// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Resources;
using System.Resources.Extensions;

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal class TypeConverterByteArrayResource : IResource
    {
        public string Name { get; }
        public string TypeAssemblyQualifiedName { get; }
        public string OriginatingFile { get; }
        public byte[] Bytes { get; }

        public string TypeFullName => NameUtilities.FullNameFromAssemblyQualifiedName(TypeAssemblyQualifiedName);

        public TypeConverterByteArrayResource(string name, string assemblyQualifiedTypeName, byte[] bytes, string originatingFile)
        {
            Name = name;
            TypeAssemblyQualifiedName = assemblyQualifiedTypeName;
            Bytes = bytes;
            OriginatingFile = originatingFile;
        }

        public void AddTo(IResourceWriter writer)
        {
            if (writer is PreserializedResourceWriter preserializedResourceWriter)
            {
                preserializedResourceWriter.AddTypeConverterResource(Name, Bytes, TypeAssemblyQualifiedName);
            }
            else
            {
                throw new PreserializedResourceWriterRequiredException();
            }
        }
    }
}
