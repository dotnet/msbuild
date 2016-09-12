// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel.Resolution;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Server.Models
{
    public static class NuGetFrameworkExtensions
    {
        public static FrameworkData ToPayload(this NuGetFramework framework)
        {
            return new FrameworkData
            {
                ShortName = framework.GetShortFolderName(),
                FrameworkName = framework.DotNetFrameworkName,
                FriendlyName = FrameworkReferenceResolver.Default.GetFriendlyFrameworkName(framework),
                RedistListPath = FrameworkReferenceResolver.Default.GetFrameworkRedistListPath(framework)
            };
        }
    }
}
