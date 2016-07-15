// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class FixModeFlags : Task
    {
        [Required]
        public string Dir { get; set; }

        public override bool Execute()
        {
            FS.FixModeFlags(Dir);

            return true;
        }
    }
}
