// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal static class MigrationNuGetFrameworkExtensions
    {
        public static string GetMSBuildCondition(this NuGetFramework framework)
        {
            return $" '$(TargetFramework)' == '{framework.GetTwoDigitShortFolderName()}' ";
        }
    }
}
