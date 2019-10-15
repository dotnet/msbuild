// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Tool.Common
{
    internal static class ToolManifestFinderExtensions
    {
        public static (FilePath? filePath, string warningMessage) ExplicitManifestOrFindManifestContainPackageId(
            this IToolManifestFinder toolManifestFinder,
            string explicitManifestFile,
            PackageId packageId)
        {
            if (!string.IsNullOrWhiteSpace(explicitManifestFile))
            {
                return (new FilePath(explicitManifestFile), null);
            }

            IReadOnlyList<FilePath> manifestFilesContainPackageId;
            try
            {
                manifestFilesContainPackageId
                 = toolManifestFinder.FindByPackageId(packageId);
            }
            catch (ToolManifestCannotBeFoundException e)
            {
                throw new GracefulException(new[]
                    {
                        e.Message,
                        LocalizableStrings.NoManifestGuide
                    },
                    verboseMessages: new[] { e.VerboseMessage },
                    isUserError: false);
            }

            if (manifestFilesContainPackageId.Any())
            {
                string warning = null;
                if (manifestFilesContainPackageId.Count > 1)
                {
                    warning =
                        string.Format(
                            LocalizableStrings.SamePackageIdInOtherManifestFile,
                            string.Join(
                                Environment.NewLine,
                                manifestFilesContainPackageId.Skip(1).Select(m => $"\t{m}")));
                }

                return (manifestFilesContainPackageId.First(), warning);
            }

            return (null, null);
        }
    }
}
