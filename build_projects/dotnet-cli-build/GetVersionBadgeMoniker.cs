// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Build
{
    public class GetVersionBadgeMoniker : Task
    {
        [Output]
        public string VersionBadgeMoniker { get; set; }

        public override bool Execute()
        {
            VersionBadgeMoniker = Monikers.GetBadgeMoniker();

            return true;
        }
    }
}
