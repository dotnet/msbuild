// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    // For a long MSBuildProjectFile let's shorten this to 17 chars - using the first 8 chars of the filename and a filename hash.
    public sealed class ShortenAndHashProjectName : Task
    {
        [Required]
        public string ProjectName { get; set; }

        [Output]
        public string ShortProjectName { get; set; }

        public override bool Execute()
        {
            if (ProjectName.Length <= 17)
            {
                ShortProjectName = ProjectName;
                return true;
            }

            // if the last char of string is a surrogate, cutting it in half would confuse encoder
            int length = char.IsHighSurrogate(ProjectName[7]) ? 9 : 8;
            string truncatedProjectName = ProjectName.Substring(0, length);
            string originalProjectNameHash = StableStringHash(ProjectName);
            ShortProjectName = $"{truncatedProjectName}.{originalProjectNameHash}".ToString("X8");
            return true;
    }
}