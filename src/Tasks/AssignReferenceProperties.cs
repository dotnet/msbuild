using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;

namespace Microsoft.Build.Tasks
{
    public class AssignReferenceProperties : TaskExtension
    {
        private static readonly char[] TargetFrameworkSeparator = {';'};

        public ITaskItem[] AnnotatedProjectReferences { get; set; }

        [Required]
        public string CurrentProjectTargetFramework { get; set; }

        [Output]
        public ITaskItem[] AssignedProjects { get; set; }

        public override bool Execute()
        {
            if (AnnotatedProjectReferences == null)
            {
                return !Log.HasLoggedErrors;
            }

            var assignedProjects = new List<ITaskItem>(AnnotatedProjectReferences.Length);

            NuGetFramework frameworkToMatch = NuGetFramework.Parse(CurrentProjectTargetFramework);

            if (frameworkToMatch.IsUnsupported)
            {
                // TODO Log.LogErrorFromResources();
                return false;
            }


            foreach (var project in AnnotatedProjectReferences)
            {
                assignedProjects.Add(AssignPropertiesForSingleReference(project, frameworkToMatch));
            }

            AssignedProjects = assignedProjects.ToArray();

            return !Log.HasLoggedErrors;
        }

        private ITaskItem AssignPropertiesForSingleReference(ITaskItem project, NuGetFramework currentProjectTargetFramework)
        {
            var itemWithProperties = new TaskItem(project);

            var possibleTargetFrameworks = project.GetMetadata("TargetFrameworks").Split(TargetFrameworkSeparator);

            var possibleNuGetFrameworks = possibleTargetFrameworks.Select(ParseFramework).ToList();

            var nearestNuGetFramework = new FrameworkReducer().GetNearest(currentProjectTargetFramework, possibleNuGetFrameworks);

            if (nearestNuGetFramework != null)
            {
                // Note that there can be more than one spelling of the same target framework (e.g. net45 and net4.5) and 
                // we must return a value that is spelled exactly the same way as the input. To 
                // achieve this, we find the index of the returned framework among the set we passed to nuget and use that
                // to retrive a value at the same position in the input.
                //
                // This is required to guarantee that a project can use whatever spelling appears in $(TargetFrameworks)
                // in a condition that compares against $(TargetFramework).
                //Log.LogError(Strings.NoCompatibleTargetFramework, ProjectFilePath, ReferringTargetFramework, string.Join("; ", possibleNuGetFrameworks));
                int indexOfNearestFramework = possibleNuGetFrameworks.IndexOf(nearestNuGetFramework);
                string nearestTargetFramework = possibleTargetFrameworks[indexOfNearestFramework];

                if (MetadataConversionUtilities.TryConvertItemMetadataToBool(project, "HasSingleTargetFramework",
                        out bool singleTargetFramework) && singleTargetFramework)
                {
                    itemWithProperties.SetMetadata("UndefineProperties", "TargetFramework"); // TODO: append
                }
                else
                {
                    // TODO: append or overwrite?
                    itemWithProperties.SetMetadata("SetTargetFramework", $"TargetFramework={nearestTargetFramework}");
                }

                // TODO: RID stuff

                itemWithProperties.SetMetadata("SkipGetTargetFrameworkProperties", "true");

            }

            return itemWithProperties;
        }

        private NuGetFramework ParseFramework(string name)
        {
            var framework = NuGetFramework.Parse(name);

            if (framework == null)
            {
                //Log.LogError(Strings.InvalidFrameworkName, framework);
            }

            return framework;
        }

    }
}
