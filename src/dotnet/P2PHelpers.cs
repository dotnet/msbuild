// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools
{
    internal static class P2PHelpers
    {
        public static void EnsureAllReferencesExist(List<string> references)
        {
            var notExisting = new List<string>();
            foreach (var r in references)
            {
                if (!File.Exists(r))
                {
                    notExisting.Add(r);
                }
            }

            if (notExisting.Count > 0)
            {
                throw new GracefulException(
                    string.Join(
                        Environment.NewLine,
                        notExisting.Select((r) => string.Format(LocalizableStrings.ReferenceDoesNotExist, r))));
            }
        }

        public static void ConvertPathsToRelative(string root, ref List<string> references)
        {
            root = PathUtility.EnsureTrailingSlash(Path.GetFullPath(root));
            references = references.Select((r) => PathUtility.GetRelativePath(root, Path.GetFullPath(r))).ToList();
        }

        public static string NormalizeSlashesForMsbuild(string path)
        {
            return path.Replace('/', '\\');
        }

        public static int AddProjectToProjectReference(ProjectRootElement root, string framework, IEnumerable<string> refs)
        {
            int numberOfAddedReferences = 0;
            const string ProjectItemElementType = "ProjectReference";

            ProjectItemGroupElement itemGroup = root.FindUniformOrCreateItemGroupWithCondition(ProjectItemElementType, framework);
            foreach (var @ref in refs.Select((r) => NormalizeSlashesForMsbuild(r)))
            {
                if (root.HasExistingItemWithCondition(framework, @ref))
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.ProjectAlreadyHasAreference, @ref));
                    continue;
                }

                numberOfAddedReferences++;
                itemGroup.AppendChild(root.CreateItemElement(ProjectItemElementType, @ref));

                Reporter.Output.WriteLine(string.Format(LocalizableStrings.ReferenceAddedToTheProject, @ref));
            }

            return numberOfAddedReferences;
        }

        public static int RemoveProjectToProjectReference(ProjectRootElement root, string framework, IEnumerable<string> refs)
        {
            throw new NotImplementedException();
        }
    }
}
