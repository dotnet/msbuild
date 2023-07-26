// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class AddFacadesToReferences : TaskBase
    {
        [Required]
        public ITaskItem[] References { get; set; }

        [Required]
        public ITaskItem[] Facades { get; set; }

        [Output]
        public ITaskItem[] UpdatedReferences { get; set; }

        protected override void ExecuteCore()
        {
            Dictionary<string, ITaskItem> facadeDict = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var facade in Facades)
            {
                string filename = facade.GetMetadata("FileName");
                TaskItem facadeWithMetadata = new TaskItem(filename);
                facadeWithMetadata.SetMetadata(MetadataKeys.HintPath, facade.ItemSpec);
                facadeWithMetadata.SetMetadata(MetadataKeys.Private, "false");
                facadeDict[filename] = facadeWithMetadata;
            }

            List<ITaskItem> updatedReferences = new List<ITaskItem>();

            foreach (var reference in References)
            {
                string filename = reference.ItemSpec;
                if (!facadeDict.ContainsKey(filename))
                {
                    updatedReferences.Add(reference);
                }
                else
                {
                    if (!reference.GetMetadata(MetadataKeys.IsImplicitlyDefined).Equals("True", StringComparison.OrdinalIgnoreCase) &&
                        reference.GetMetadata(MetadataKeys.NuGetSourceType) == "")
                    {
                        //  Reference is not implicitly defined or coming from a NuGet package, so preserve its metadata
                        //  on the facade reference that will replace it
                        var newFacade = new TaskItem(facadeDict[filename]);
                        reference.CopyMetadataTo(newFacade);
                        facadeDict[filename] = newFacade;
                    }
                }
            }

            foreach (var facade in Facades)
            {
                string filename = facade.GetMetadata("FileName");
                updatedReferences.Add(facadeDict[filename]);
            }

            UpdatedReferences = updatedReferences.ToArray();
        }
    }
}
