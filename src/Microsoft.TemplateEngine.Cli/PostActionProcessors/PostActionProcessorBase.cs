// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal abstract class PostActionProcessor2Base
    {
        protected internal NewCommandCallbacks? Callbacks { get; set; }

        /// <summary>
        /// Gets absolute normalized path for a target matching <paramref name="sourcePathGlob"/>.
        /// </summary>
        protected static IReadOnlyList<string> GetTargetForSource(ICreationEffects2 creationEffects, string sourcePathGlob, string outputBasePath)
        {
            Glob g = Glob.Parse(sourcePathGlob);
            List<string> results = new List<string>();

            if (creationEffects.FileChanges != null)
            {
                foreach (IFileChange2 change in creationEffects.FileChanges)
                {
                    if (g.IsMatch(change.SourceRelativePath))
                    {
                        results.Add(NormalizePath(outputBasePath, change.TargetRelativePath));
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Gets normalized absolute path of <paramref name="basePath"/> + <paramref name="relativePath"/>.
        /// </summary>
        protected static string NormalizePath(string basePath, string relativePath)
        {
            return Path.GetFullPath(Path.Combine(basePath, relativePath));
        }
    }
}
