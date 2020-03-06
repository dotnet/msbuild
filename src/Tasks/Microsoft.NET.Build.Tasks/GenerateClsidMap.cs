// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.NET.HostModel.ComHost;

namespace Microsoft.NET.Build.Tasks
{
    public class GenerateClsidMap : TaskBase
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
                    using (PEReader peReader = new PEReader(assemblyStream))
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
