// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class ValidateExecutableReferences : TaskBase
    {
        public bool SelfContained { get; set; }

        public bool SelfContainedIsGlobalProperty { get; set; }

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

                if (SelfContainedIsGlobalProperty && project.GetBooleanMetadata("AcceptsRuntimeIdentifier") == true)
                {
                    //  If AcceptsRuntimeIdentifier is true for the project, and SelfContained was set as a global property,
                    //  then the SelfContained value will flow across the project reference when we go to build it, despite the
                    //  fact that we ignored it when doing the GetTargetFrameworks negotiation.
                    referencedProjectIsSelfContained = SelfContained;
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
