// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Sln.Internal;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
