// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel.Server.Models
{
    internal class ProjectReferenceDescription
    {
        private ProjectReferenceDescription() { }

        public FrameworkData Framework { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string MSBuildProjectPath { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as ProjectReferenceDescription;
            return other != null &&
                   string.Equals(Name, other.Name) &&
                   string.Equals(Path, other.Path) &&
                   string.Equals(MSBuildProjectPath, other.MSBuildProjectPath);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Create a ProjectReferenceDescription from given LibraryDescription. If the library doesn't 
        /// represent a project reference returns null.
        /// </summary>
        public static ProjectReferenceDescription Create(LibraryDescription library)
        {
            if (library is ProjectDescription)
            {
                return new ProjectReferenceDescription
                {
                    Framework = library.Framework.ToPayload(),
                    Name = library.Identity.Name,
                    Path = library.Path
                };
            }
            else if (library is MSBuildProjectDescription)
            {
                return new ProjectReferenceDescription
                {
                    Framework = library.Framework.ToPayload(),
                    Name = library.Identity.Name,
                    Path = library.Path,
                    MSBuildProjectPath = ((MSBuildProjectDescription)library).ProjectLibrary.MSBuildProject
                };
            }
            else
            {
                return null;
            }
        }
    }
}
