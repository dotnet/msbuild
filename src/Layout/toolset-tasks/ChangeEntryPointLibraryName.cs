// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public class ChangeEntryPointLibraryName : Task
    {
        [Required]
        public string DepsFile { get; set; }

        [Required]
        public string NewName { get; set; }

        public override bool Execute()
        {
            PublishMutationUtilities.ChangeEntryPointLibraryName(DepsFile, NewName);

            return true;
        }
    }
}
