// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Resources.Extensions;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal class FileStreamResource : IResource
    {
        public string Name { get; }
        public string TypeAssemblyQualifiedName { get; }
        public string OriginatingFile { get; }
        public string FileName { get; }

        public string TypeFullName => NameUtilities.FullNameFromAssemblyQualifiedName(TypeAssemblyQualifiedName);

        /// <summary>
        /// Construct a new linked resource.
        /// </summary>
        /// <param name="name">The resource's name</param>
        /// <param name="assemblyQualifiedTypeName">The assembly-qualified type name of the resource (at runtime).</param>
        /// <param name="fileName">The absolute path of the file to be embedded as a resource.</param>
        /// <param name="originatingFile">The absolute path of the file that defined the ResXFileRef to this resource.</param>
        public FileStreamResource(string name, string assemblyQualifiedTypeName, string fileName, string originatingFile)
        {
            Name = name;
            TypeAssemblyQualifiedName = assemblyQualifiedTypeName;
            FileName = fileName;
            OriginatingFile = originatingFile;
        }

        public void AddTo(IResourceWriter writer)
        {
            if (writer is PreserializedResourceWriter preserializedResourceWriter)
            {
                FileStream fileStream = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);

                preserializedResourceWriter.AddActivatorResource(Name, fileStream, TypeAssemblyQualifiedName, closeAfterWrite: true);
            }
            else
            {
                throw new PreserializedResourceWriterRequiredException();
            }
        }
    }
}
