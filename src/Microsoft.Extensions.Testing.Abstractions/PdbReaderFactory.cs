// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public class PdbReaderFactory : IPdbReaderFactory
    {
        /// <summary>
        /// Creates <see cref="IPdbReader"/> for given file.
        /// </summary>
        /// <param name="pdbPath">
        /// Path to the .pdb file or a PE file that refers to the .pdb file in its Debug Directory Table.
        /// </param>
        /// <exception cref="IOException">File <paramref name="pdbPath"/> does not exist or can't be read.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="pdbPath"/> is null.</exception>
        public IPdbReader Create(string pdbPath)
        {
            if (pdbPath == null)
            {
                throw new ArgumentNullException(nameof(pdbPath));
            }

            Stream stream = OpenRead(pdbPath);

            if (IsPE(stream))
            {
                return CreateFromPortableExecutable(stream, pdbPath);
            }

            if (IsPortable(stream))
            {
                return new PortablePdbReader(stream);
            }

            return new FullPdbReader(stream);
        }

        private static bool IsPortable(Stream stream)
        {
            bool result = stream.ReadByte() == 'B' && stream.ReadByte() == 'S' && stream.ReadByte() == 'J' && stream.ReadByte() == 'B';
            stream.Position = 0;
            return result;
        }

        private static bool IsPE(Stream stream)
        {
            bool result = stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
            stream.Position = 0;
            return result;
        }

        private IPdbReader CreateFromPortableExecutable(Stream peStream, string pePath)
        {
            using (var peReader = new PEReader(peStream))
            {
                MetadataReaderProvider pdbProvider;
                string pdbPath;
                if (peReader.TryOpenAssociatedPortablePdb(pePath, TryOpenRead, out pdbProvider, out pdbPath))
                {
                    return new PortablePdbReader(pdbProvider);
                }

                return MissingPdbReader.Instance;
            }
        }

        private static Stream OpenRead(string path)
        {
            try
            {
                return File.OpenRead(path);
            }
            catch (Exception e) when (!(e is IOException))
            {
                throw new IOException(e.Message, e);
            }
        }

        private static Stream TryOpenRead(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return File.OpenRead(path);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (Exception e) when (!(e is IOException))
            {
                throw new IOException(e.Message, e);
            }
        }
    }
}
