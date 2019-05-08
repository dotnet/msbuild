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
        public string TypeName { get; }
        public string OriginatingFile { get; }
        public string FileName { get; }

        public FileStreamResource(string name, string typeName, string fileName, string originatingFile)
        {
            Name = name;
            TypeName = typeName;
            FileName = fileName;
            OriginatingFile = originatingFile;
        }

        public void AddTo(IResourceWriter writer)
        {
            if (writer is PreserializedResourceWriter preserializedResourceWriter)
            {
                FileStream fileStream = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);

                preserializedResourceWriter.AddActivatorResource(Name, TypeName, fileStream, closeAfterWrite: true);
            }
            else
            {
                ErrorUtilities.ThrowInternalError($"{nameof(FileStreamResource)} was asked to serialize to a {writer.GetType().ToString()}");
            }
        }
    }
}
