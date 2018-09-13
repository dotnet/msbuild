// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{ 
    public class FindReferenceAssembliesForReferences : TaskExtension
    {
        [Required]
        public ITaskItem[] References { get; set; }

        public bool CompileUsingReferenceAssemblies { get; set; }

        [Output]
        public ITaskItem[] ReferencesWithImplementationAssemblies { get; set; }

        [Output]
        public ITaskItem[] ReferencesWithReferenceAssemblies { get; set; }

        public override bool Execute()
        {
            ReferencesWithImplementationAssemblies = new ITaskItem[References.Length];
            ReferencesWithReferenceAssemblies = new ITaskItem[References.Length];
            string referenceAssemblyMetadataName = "ReferenceAssembly";

            for (int i = 0; i < References.Length; i++)
            {
                ITaskItem reference = References[i];
                var referenceWithImplementationAssembly = new TaskItem(reference);
                var referenceWithReferenceAssembly = new TaskItem(reference);
                string referenceAssembly = reference.GetMetadata(referenceAssemblyMetadataName);

                if (String.IsNullOrEmpty(referenceAssembly))
                {
                    referenceWithImplementationAssembly.SetMetadata(referenceAssemblyMetadataName, reference.GetMetadata("FullPath"));
                }

                if (CompileUsingReferenceAssemblies)
                {
                    referenceWithReferenceAssembly.ItemSpec = referenceAssembly;

                    if (reference.ItemSpec != referenceAssembly)
                    {
                        referenceWithReferenceAssembly.SetMetadata("OriginalPath", reference.ItemSpec);
                    }
                }

                ReferencesWithImplementationAssemblies[i] = referenceWithImplementationAssembly;
                ReferencesWithReferenceAssemblies[i] = referenceWithReferenceAssembly;
            }

            return true;
        }
    }
}
