// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//  Use MetadataReader version of GetAssemblyVersion for:
//  - netcoreapp version of Microsoft.NET.Build.Extensions.Tasks
//  - All versions of Microsoft.NET.Build.Tasks

//  We don't use it for the .NET Framework version of Microsoft.NET.Build.Extensions in order to
//  avoid loading the System.Reflection.Metadata assembly in vanilla .NET Framework build scenarios

//  We do use the MetadataReader version for the SDK tasks in order to correctly read the assembly
//  versions of cross-gened assemblies.  See https://github.com/dotnet/sdk/issues/1502
#if NETCOREAPP || !EXTENSIONS

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.NET.Build.Tasks
{
    static partial class FileUtilities
    {
        private static Dictionary<string, (DateTime LastKnownWriteTimeUtc, Version Version)> s_versionCache = new(StringComparer.OrdinalIgnoreCase /* Not strictly correct on *nix. Fix? */);

        private static Version GetAssemblyVersion(string sourcePath)
        {
            DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(sourcePath);

            if (s_versionCache.TryGetValue(sourcePath, out var cacheEntry) 
                && lastWriteTimeUtc == cacheEntry.LastKnownWriteTimeUtc)
            {
                return cacheEntry.Version;
            }

            Version version = GetAssemblyVersionFromFile(sourcePath);

            s_versionCache[sourcePath] = (lastWriteTimeUtc, version);

            // When introducing this cache, we decided that the cached
            // data was small and likely to be basically finite for
            // any given MSBuild process lifetime, so we did not implement
            // a cache lifetime. If you're here because those assumptions
            // don't hold, there's no reason the cache must be static
            // and monotonically growing.

            return version;

            static Version GetAssemblyVersionFromFile(string sourcePath)
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
}

#endif
