// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public interface IPdbReaderFactory
    {
        /// <summary>
        /// Creates <see cref="IPdbReader"/> for given file.
        /// </summary>
        /// <param name="pdbPath">
        /// Path to the .pdb file or a PE file that refers to the .pdb file in its Debug Directory Table.
        /// </param>
        /// <exception cref="IOException">File <paramref name="pdbPath"/> does not exist or can't be read.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="pdbPath"/> is null.</exception>
        IPdbReader Create(string pdbPath);
    }
}
