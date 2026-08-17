// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Resources;
using System.Resources.Extensions;

#nullable disable

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal class TypeConverterStringResource : IResource
    {
        public string Name { get; }
        public string TypeAssemblyQualifiedName { get; }
        public string OriginatingFile { get; }
        public string StringRepresentation { get; }

        public string TypeFullName => NameUtilities.FullNameFromAssemblyQualifiedName(TypeAssemblyQualifiedName);

        public TypeConverterStringResource(string name, string assemblyQualifiedTypeName, string stringRepresentation, string originatingFile)
        {
            Name = name;
            TypeAssemblyQualifiedName = assemblyQualifiedTypeName;
            StringRepresentation = stringRepresentation;
            OriginatingFile = originatingFile;
        }

        public void AddTo(IResourceWriter writer)
        {
            if (writer is PreserializedResourceWriter preserializedResourceWriter)
            {
                preserializedResourceWriter.AddResource(Name, StringRepresentation, TypeAssemblyQualifiedName);
            }
            else
            {
                throw new PreserializedResourceWriterRequiredException();
            }
        }
    }
}
