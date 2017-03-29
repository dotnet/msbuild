// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    internal abstract class ConditionalTransform<T, U> : ITransform<T, U>
    {
        private Func<T, bool> _condition;

        public ConditionalTransform(Func<T,bool> condition)
        {
            _condition = condition;
        }

        public U Transform(T source)
        {
            if (_condition == null || _condition(source))
            {
                return ConditionallyTransform(source);
            }

            return default(U);
        }

        public abstract U ConditionallyTransform(T source);

        protected string FormatGlobPatternsForMsbuild(IEnumerable<string> patterns, string projectDirectory)
        {
            if (patterns == null)
            {
                return string.Empty;
            }

            List<string> mutatedPatterns = new List<string>(patterns.Count());

            foreach (var pattern in patterns)
            {
                // Do not use forward slashes
                // https://github.com/Microsoft/msbuild/issues/724
                var mutatedPattern = pattern.Replace('/', '\\');

                // MSBuild cannot copy directories
                mutatedPattern = ReplaceDirectoriesWithGlobs(mutatedPattern, projectDirectory);

                mutatedPatterns.Add(mutatedPattern);
            }

            return string.Join(";", mutatedPatterns);
        }

        private string ReplaceDirectoriesWithGlobs(string pattern, string projectDirectory)
        {
            if (PatternIsDirectory(pattern, projectDirectory))
            {
                return $"{pattern.TrimEnd(new char[] { '\\' })}\\**\\*";
            }
            else
            {
                return pattern;
            }
        }

        private bool PatternIsDirectory(string pattern, string projectDirectory)
        {
            // TODO: what about /some/path/**/somedir?
            // Should this even be migrated?
            var path = pattern;

            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(projectDirectory, path);
            }

            return Directory.Exists(path);
        }

        protected string ConvertTargetPathToMsbuildMetadata(string targetPath)
        {
            var targetIsFile = MappingsTargetPathIsFile(targetPath);

            if (targetIsFile)
            {
                return targetPath;
            }

            return $"{targetPath}%(FileName)%(Extension)";
        }

        private bool MappingsTargetPathIsFile(string targetPath)
        {
            var normalizedTargetPath = PathUtility.GetPathWithDirectorySeparator(targetPath);

            return normalizedTargetPath[normalizedTargetPath.Length - 1] != Path.DirectorySeparatorChar;
        }
    }
}
