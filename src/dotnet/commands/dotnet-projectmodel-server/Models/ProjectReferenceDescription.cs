// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Server.Models
{
    internal class ProjectReferenceDescription
    {
        private ProjectReferenceDescription() { }

        public FrameworkData Framework { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string WrappedProjectPath { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as ProjectReferenceDescription;
            return other != null &&
                   string.Equals(Name, other.Name) &&
                   string.Equals(Path, other.Path) &&
                   string.Equals(WrappedProjectPath, other.WrappedProjectPath);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        
        public static ProjectReferenceDescription Create(ProjectDescription description)
        {
            var targetFrameworkInformation = description.TargetFrameworkInfo;

            string wrappedProjectPath = null;
            if (!string.IsNullOrEmpty(targetFrameworkInformation?.WrappedProject) &&
                description.Project != null)
            {
                wrappedProjectPath = System.IO.Path.Combine(
                    description.Project.ProjectDirectory,
                    targetFrameworkInformation.WrappedProject);

                wrappedProjectPath = System.IO.Path.GetFullPath(wrappedProjectPath);
            }

            return new ProjectReferenceDescription
            {
                Framework = description.Framework.ToPayload(),
                Name = description.Identity.Name,
                Path = description.Path,
                WrappedProjectPath = wrappedProjectPath,
            };
        }
    }
}
