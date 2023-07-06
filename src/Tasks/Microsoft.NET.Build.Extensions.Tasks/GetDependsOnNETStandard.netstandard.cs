// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETCOREAPP
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.NET.Build.Tasks
{
    public partial class GetDependsOnNETStandard
    {
        internal static bool GetFileDependsOnNETStandard(string filePath)
        {
            using (var assemblyStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            using (PEReader peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen))
            {
                if (peReader.HasMetadata)
                {
                    MetadataReader reader = peReader.GetMetadataReader();
                    if (reader.IsAssembly)
                    {
                        foreach (var referenceHandle in reader.AssemblyReferences)
                        {
                            AssemblyReference reference = reader.GetAssemblyReference(referenceHandle);

                            if (reader.StringComparer.Equals(reference.Name, NetStandardAssemblyName))
                            {
                                return true;
                            }
                            
                            if (reader.StringComparer.Equals(reference.Name, SystemRuntimeAssemblyName) &&
                                reference.Version >= SystemRuntimeMinVersion)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
#endif
