// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel.Files;

namespace Microsoft.DotNet.ProjectModel
{
    public class PackOptions
    {
        public string[] Tags { get; set; }

        public string[] Owners { get; set; }

        public string ReleaseNotes { get; set; }

        public string IconUrl { get; set; }

        public string ProjectUrl { get; set; }

        public string LicenseUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public string RepositoryType { get; set; }

        public string RepositoryUrl { get; set; }

        public string Summary { get; set; }

        public IncludeContext PackInclude { get; set; }
    }
}