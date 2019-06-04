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
    internal class BinaryFormatterByteArrayResource : IResource
    {
        public string Name { get; }
        public string TypeName { get; }
        public string OriginatingFile { get; }
        public byte[] Bytes { get; }

        public BinaryFormatterByteArrayResource(string name, string typeName, byte[] bytes, string originatingFile)
        {
            Name = name;
            TypeName = typeName;
            Bytes = bytes;
            OriginatingFile = originatingFile;
        }

        public void AddTo(IResourceWriter writer)
        {
            if (writer is PreserializedResourceWriter preserializedResourceWriter)
            {
                // TODO: use no-typename-needed method from https://github.com/dotnet/corefx/pull/38012
                // preserializedResourceWriter.AddBinaryFormattedResource(Name, Bytes);

                preserializedResourceWriter.AddBinaryFormattedResource(Name, TypeName, Bytes);
            }
            else
            {
                ErrorUtilities.ThrowInternalError($"{nameof(BinaryFormatterByteArrayResource)} was asked to serialize to a {writer.GetType().ToString()}");
            }
        }
    }
}
