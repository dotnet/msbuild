// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP1_0

using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.NET.Build.Tasks
{
    static partial class FileUtilities
    {
        private static Version GetAssemblyVersion(string sourcePath)
        {
            using (var assemblyStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            {
                Version result = null;
                try
                {
                    using (PEReader peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen))
                    {
                        if (peReader.HasMetadata)
                        {
                            MetadataReader reader = peReader.GetMetadataReader();
                            if (reader.IsAssembly)
                            {
                                result = reader.GetAssemblyDefinition().Version;
                            }
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    // not a PE
                }

                return result;
            }
        }
    }
}

#endif