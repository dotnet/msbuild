// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Sln.Internal;

namespace Microsoft.DotNet.Tools.Common
{
    internal static class SlnProjectCollectionExtensions
    {
        public static IEnumerable<SlnProject> GetProjectsByType(
            this SlnProjectCollection projects,
            string typeGuid)
        {
            return projects.Where(p => p.TypeGuid == typeGuid);
        }

        public static IEnumerable<SlnProject> GetProjectsNotOfType(
            this SlnProjectCollection projects,
            string typeGuid)
        {
            return projects.Where(p => p.TypeGuid != typeGuid);
        }
    }
}
