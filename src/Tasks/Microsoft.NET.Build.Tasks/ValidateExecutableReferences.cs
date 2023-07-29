// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class ValidateExecutableReferences : TaskBase
    {
        public bool SelfContained { get; set; }

        public bool IsExecutable { get; set; }

        public ITaskItem[] ReferencedProjects { get; set; } = Array.Empty<ITaskItem>();

        public bool UseAttributeForTargetFrameworkInfoPropertyNames { get; set; }

        protected override void ExecuteCore()
        {
            if (!IsExecutable)
            {
                //  If current project is not executable, then we don't need to check its references
                return;
            }

            foreach (ITaskItem project in ReferencedProjects)
            {
                string nearestTargetFramework = project.GetMetadata("NearestTargetFramework");

                if (string.IsNullOrEmpty(nearestTargetFramework) || !project.HasMetadataValue("AdditionalPropertiesFromProject"))
                {
                    //  Referenced project doesn't have the right metadata.  This may be because it's a different project type (C++, for example)
                    //  In this case just skip the checks
                    continue;
                }

                var additionalPropertiesXml = XElement.Parse(project.GetMetadata("AdditionalPropertiesFromProject"));
                XElement targetFrameworkElement = UseAttributeForTargetFrameworkInfoPropertyNames ?
                    additionalPropertiesXml.Elements().Where(el => el.HasAttributes && el.FirstAttribute.Value.Equals(nearestTargetFramework)).Single() :
                    additionalPropertiesXml.Element(nearestTargetFramework);
                Dictionary<string, string> projectAdditionalProperties = new(StringComparer.OrdinalIgnoreCase);
                foreach (XElement propertyElement in targetFrameworkElement.Elements())
                {
                    projectAdditionalProperties[propertyElement.Name.LocalName] = propertyElement.Value;
                }

                bool shouldBeValidatedAsExecutableReference = MSBuildUtilities.ConvertStringToBool(projectAdditionalProperties["ShouldBeValidatedAsExecutableReference"], true);
                bool referencedProjectIsExecutable = MSBuildUtilities.ConvertStringToBool(projectAdditionalProperties["_IsExecutable"]);
                bool referencedProjectIsSelfContained = MSBuildUtilities.ConvertStringToBool(projectAdditionalProperties["SelfContained"]);
                bool referencedProjectHadSelfContainedSpecified = MSBuildUtilities.ConvertStringToBool(projectAdditionalProperties["_SelfContainedWasSpecified"]);

                // We can only access TargetFrameworks and NearestTargetFramework to find the referenced project "TargetFramework".
                // We rely on the nearest one because it will pick the lowest 'most-compatible' tfm for the referencer and referencee projects.
                // Since 'younger' TFMs are the ones that would error and are generally also what gets picked as 'most-copmaptible,' we can use it.
                FrameworkName? referencedProjectTargetFramework = null;
                var targetFrameworkMonikerIndex = Array.FindIndex(project.GetMetadata("TargetFrameworks").Split(';'), targetFramework => targetFramework == nearestTargetFramework);

                // Since TargetFrameworks can have aliases that aren't the real TFM, we need to uncover the potential alias to the raw TargetFramework in the TFMs by using its index
                var targetFrameworkMonikers = project.GetMetadata("TargetFrameworkMonikers").Split(';');
                if (targetFrameworkMonikerIndex != -1 && targetFrameworkMonikerIndex < targetFrameworkMonikers.Length)
                {
                    // we never expect this if statement to not go through, but if there is a bug with targetframeworks we need to be careful not to fail here.
                    referencedProjectTargetFramework = new FrameworkName(targetFrameworkMonikers[targetFrameworkMonikerIndex]);
                }

                var globalProperties = BuildEngine6.GetGlobalProperties();

                bool selfContainedIsGlobalProperty = globalProperties.ContainsKey("SelfContained");
                bool runtimeIdentifierIsGlobalProperty = globalProperties.ContainsKey("RuntimeIdentifier");

                bool projectIsRidAgnostic = true;
                if (projectAdditionalProperties.TryGetValue("IsRidAgnostic", out string isRidAgnostic) &&
                    bool.TryParse(isRidAgnostic, out bool isRidAgnosticParseResult))
                {
                    projectIsRidAgnostic = isRidAgnosticParseResult;
                }

                if (!projectIsRidAgnostic)
                {
                    //  If the project is NOT RID agnostic, and SelfContained was set as a global property,
                    //  then SelfContained will flow across the project reference when we go to build it,
                    //  despite the fact that we ignored it when doing the GetTargetFrameworks negotiation
                    if (selfContainedIsGlobalProperty && SelfContained)
                    {
                        referencedProjectIsSelfContained = true;
                    }

                    //  We need to check if referenced project will become SelfContained because of its RuntimeIdentifier. This only happens on TargetFrameworks less than net8.0.
                    bool runtimeIdentifierInfersSelfContained = referencedProjectTargetFramework != null && referencedProjectTargetFramework.Identifier == ".NETCoreApp" && referencedProjectTargetFramework.Version.Major < 8;

                    //  If the project is NOT RID agnostic, then a global RuntimeIdentifier will flow to it.
                    //  If the project didn't explicitly specify a value for SelfContained, then this will
                    //  set SelfContained to true
                    if (runtimeIdentifierInfersSelfContained && runtimeIdentifierIsGlobalProperty && !referencedProjectHadSelfContainedSpecified)
                    {
                        referencedProjectIsSelfContained = true;
                    }
                }

                if (referencedProjectIsExecutable && shouldBeValidatedAsExecutableReference)
                {
                    if (SelfContained && !referencedProjectIsSelfContained)
                    {
                        Log.LogError(Strings.SelfContainedExeCannotReferenceNonSelfContained, project.ItemSpec);
                    }
                    else if (!SelfContained && referencedProjectIsSelfContained)
                    {
                        Log.LogError(Strings.NonSelfContainedExeCannotReferenceSelfContained, project.ItemSpec);
                    }
                }
            }
        }
    }
}
