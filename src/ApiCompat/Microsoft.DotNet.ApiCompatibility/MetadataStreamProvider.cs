// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Provider to retrieve the stream of a <see cref="MetadataInformation"/>.
    /// </summary>
    public class MetadataStreamProvider : IMetadataStreamProvider
    {
        private readonly HashSet<string> _knownArchiveExtensions = new(new string[] { ".zip", ".nupkg" });

        /// <inheritdoc />
        public Stream GetStream(MetadataInformation metadata)
        {
            string fileExtension = Path.GetExtension(metadata.FullPath);
            bool isArchive = _knownArchiveExtensions.Contains(fileExtension);

            // If the assembly isn't part of an archive, read from the full path directly.
            if (!isArchive)
            {
                return File.OpenRead(metadata.FullPath);
            }

            // If the assembly is part of an archive, AssemblyId is set to the relative path inside it.
            MemoryStream ms = new();
            using (FileStream stream = File.OpenRead(metadata.FullPath))
            {
                var zipFile = new ZipArchive(stream);
                zipFile.GetEntry(metadata.AssemblyId)?.Open().CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
            }

            return ms;
        }
    }
}
