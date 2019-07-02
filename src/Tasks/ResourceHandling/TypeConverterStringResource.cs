// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System.Resources;
using System.Resources.Extensions;

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal class TypeConverterStringResource : IResource
    {
        public string Name { get; }
        public string TypeName { get; }
        public string OriginatingFile { get; }
        public string StringRepresentation { get; }

        public TypeConverterStringResource(string name, string typeName, string stringRepresentation, string originatingFile)
        {
            Name = name;
            TypeName = typeName;
            StringRepresentation = stringRepresentation;
            OriginatingFile = originatingFile;
        }

        public void AddTo(IResourceWriter writer)
        {
            if (writer is PreserializedResourceWriter preserializedResourceWriter)
            {
                preserializedResourceWriter.AddResource(Name, StringRepresentation, TypeName);
            }
            else
            {
                throw new PreserializedResourceWriterRequiredException();
            }
        }
    }
}
