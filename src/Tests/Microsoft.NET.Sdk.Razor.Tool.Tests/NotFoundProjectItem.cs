// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.AspNetCore.Razor.Language;
using System.IO;

namespace Microsoft.NET.Sdk.Razor.Tool.Tests
{
    /// <summary>
    /// A <see cref="RazorProjectItem"/> that does not exist.
    /// </summary>
    internal class NotFoundProjectItem : RazorProjectItem
    {
        /// <summary>
        /// Initializes a new instance of <see cref="NotFoundProjectItem"/>.
        /// </summary>
        /// <param name="basePath">The base path.</param>
        /// <param name="path">The path.</param>
        /// <param name="fileKind">The file kind</param>
        public NotFoundProjectItem(string basePath, string path, string fileKind)
        {
            BasePath = basePath;
            FilePath = path;
            FileKind = fileKind ?? FileKinds.GetFileKindFromFilePath(path);
        }

        /// <inheritdoc />
        public override string BasePath { get; }

        /// <inheritdoc />
        public override string FilePath { get; }

        /// <inheritdoc />
        public override string FileKind { get; }

        /// <inheritdoc />
        public override bool Exists => false;

        /// <inheritdoc />
        public override string PhysicalPath => throw new NotSupportedException();

        /// <inheritdoc />
        public override Stream Read() => throw new NotSupportedException();
    }
}
