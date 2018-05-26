// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A task to determine the code analysis rule set file.</summary>
//-----------------------------------------------------------------------

using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Determines which file, if any, to be used as the code analysis rule set based
    /// on the supplied code analysis properties.
    /// </summary>
    public sealed class ResolveCodeAnalysisRuleSet : TaskExtension
    {
        #region Properties

        /// <summary>
        /// The desired code analysis rule set file. May be a simple name, relative
        /// path, or full path.
        /// </summary>
        public string CodeAnalysisRuleSet { get; set; }

        /// <summary>
        /// The set of additional directories to search for code analysis rule set files.
        /// </summary>
        public string[] CodeAnalysisRuleSetDirectories { get; set; }

        /// <summary>
        /// The location of the project currently being built.
        /// </summary>
        public string MSBuildProjectDirectory { get; set; }

        /// <summary>
        /// The location of the resolved rule set file. May be null if the file
        /// does not exist on disk.
        /// </summary>
        [Output]
        public string ResolvedCodeAnalysisRuleSet { get; private set; }

        /// <summary>
        /// Runs the task.
        /// </summary>
        /// <returns>True if the task succeeds without errors; false otherwise.</returns>
        public override bool Execute()
        {
            ResolvedCodeAnalysisRuleSet = GetResolvedRuleSetPath();

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Computes the resolved rule set path.
        /// 
        /// There are four cases: null, file name, relative path, and full path.
        ///
        /// If we were given no value for the ruleset, simply return null.
        ///
        /// For full path we return the string as it is.
        ///
        /// A simple file name can refer to either a file in the MSBuild project directory
        /// or a file in the rule set search paths. In the former case we return the string as-is.
        /// In the latter case, we create a full path by prepending the first rule set search path
        /// where the file is found.
        ///
        /// For relative paths we return the string as-is.
        ///
        /// In all cases, we return null if the file does not actual exist.
        /// </summary>
        /// <returns>The full or relative path to the rule set, or null if the file does not exist.</returns>
        private string GetResolvedRuleSetPath()
        {
            if (string.IsNullOrEmpty(CodeAnalysisRuleSet))
            {
                return null;
            }

            if (CodeAnalysisRuleSet == Path.GetFileName(CodeAnalysisRuleSet))
            {
                // This is a simple file name.
                // Check if the file exists in the MSBuild project directory.
                if (!string.IsNullOrEmpty(MSBuildProjectDirectory))
                {
                    string fullName = Path.Combine(MSBuildProjectDirectory, CodeAnalysisRuleSet);
                    if (File.Exists(fullName))
                    {
                        return CodeAnalysisRuleSet;
                    }
                }

                // Try the rule set directories if we have some.
                if (CodeAnalysisRuleSetDirectories != null)
                {
                    foreach (string directory in CodeAnalysisRuleSetDirectories)
                    {
                        string fullName = Path.Combine(directory, CodeAnalysisRuleSet);
                        if (File.Exists(fullName))
                        {
                            return fullName;
                        }
                    }
                }
            }
            else if (!Path.IsPathRooted(CodeAnalysisRuleSet))
            {
                // This is a path relative to the project.
                if (!string.IsNullOrEmpty(MSBuildProjectDirectory))
                {
                    string fullName = Path.Combine(MSBuildProjectDirectory, CodeAnalysisRuleSet);
                    if (File.Exists(fullName))
                    {
                        return CodeAnalysisRuleSet;
                    }
                }
            }
            else if (File.Exists(CodeAnalysisRuleSet))
            {
                // This is a full path.
                return CodeAnalysisRuleSet;
            }

            // We can't resolve the rule set to any existing file.
            Log.LogWarningWithCodeFromResources("Compiler.UnableToFindRuleSet", CodeAnalysisRuleSet);
            return null;
        }

        #endregion
    }
}
