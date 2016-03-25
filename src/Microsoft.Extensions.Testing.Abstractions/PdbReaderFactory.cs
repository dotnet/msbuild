// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public class PdbReaderFactory : IPdbReaderFactory
    {
        public IPdbReader Create(string pdbPath)
        {
            var pdbStream = new FileStream(pdbPath, FileMode.Open, FileAccess.Read);

            if (IsPortable(pdbStream))
            {
                return new PortablePdbReader(pdbStream);
            }
            else
            {
                return new FullPdbReader(pdbStream);
            }
        }

        private static bool IsPortable(Stream pdbStream)
        {
            return pdbStream.ReadByte() == 'B' &&
                pdbStream.ReadByte() == 'S' &&
                pdbStream.ReadByte() == 'J' &&
                pdbStream.ReadByte() == 'B';
        }
    }
}
