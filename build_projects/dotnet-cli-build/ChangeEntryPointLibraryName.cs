// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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
            PublishMutationUtilties.ChangeEntryPointLibraryName(DepsFile, NewName);

            return true;
        }
    }
}
