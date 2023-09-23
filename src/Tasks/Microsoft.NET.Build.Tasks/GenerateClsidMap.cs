// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.NET.HostModel.ComHost;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateClsidMap : TaskWithAssemblyResolveHooks
    {
        [Required]
        public string IntermediateAssembly { get; set; }

        [Required]
        public string ClsidMapDestinationPath { get; set; }

        protected override void ExecuteCore()
        {
            using (var assemblyStream = new FileStream(IntermediateAssembly, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            {
                try
                {
                    using (PEReader peReader = new(assemblyStream))
                    {
                        if (peReader.HasMetadata)
                        {
                            MetadataReader reader = peReader.GetMetadataReader();
                            if (!reader.IsAssembly)
                            {
                                Log.LogError(Strings.ClsidMapInvalidAssembly, IntermediateAssembly);
                                return;
                            }
                            ClsidMap.Create(reader, ClsidMapDestinationPath);
                        }
                    }
                }
                catch (MissingGuidException missingGuid)
                {
                    Log.LogError(Strings.ClsidMapExportedTypesRequireExplicitGuid, missingGuid.TypeName);
                }
                catch (ConflictingGuidException conflictingGuid)
                {
                    Log.LogError(Strings.ClsidMapConflictingGuids, conflictingGuid.TypeName1, conflictingGuid.TypeName2, conflictingGuid.Guid.ToString());
                }
                catch (BadImageFormatException)
                {
                    Log.LogError(Strings.ClsidMapInvalidAssembly, IntermediateAssembly);
                    return;
                }
            }
        }
    }
}
