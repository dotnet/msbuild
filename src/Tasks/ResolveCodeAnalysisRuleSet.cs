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
        /// <summary>
        /// The desired code analysis rule set file.
        /// </summary>
        private string _codeAnalysisRuleSet;

        /// <summary>
        /// The location of the project currently being built.
        /// </summary>
        private string _projectDirectory;

        /// <summary>
        /// The set of additional directories to search for code analysis rule set files.
        /// </summary>
        private string[] _codeAnalysisRuleSetDirectories;

        /// <summary>
        /// The location of the resolved rule set file. May be null if the file
        /// does not exist on disk.
        /// </summary>
        private string _resolvedCodeAnalysisRuleSet;

        #region Properties

        /// <summary>
        /// The desired code analysis rule set file. May be a simple name, relative
        /// path, or full path.
        /// </summary>
        public string CodeAnalysisRuleSet
        {
            get { return _codeAnalysisRuleSet; }
            set { _codeAnalysisRuleSet = value; }
        }

        /// <summary>
        /// The set of additional directories to search for code analysis rule set files.
        /// </summary>
        public string[] CodeAnalysisRuleSetDirectories
        {
            get { return _codeAnalysisRuleSetDirectories; }
            set { _codeAnalysisRuleSetDirectories = value; }
        }

        /// <summary>
        /// The location of the project currently being built.
        /// </summary>
        public string MSBuildProjectDirectory
        {
            get { return _projectDirectory; }
            set { _projectDirectory = value; }
        }

        /// <summary>
        /// The location of the resolved rule set file. May be null if the file
        /// does not exist on disk.
        /// </summary>
        [Output]
        public string ResolvedCodeAnalysisRuleSet
        {
            get { return _resolvedCodeAnalysisRuleSet; }
        }

        /// <summary>
        /// Runs the task.
        /// </summary>
        /// <returns>True if the task succeeds without errors; false otherwise.</returns>
        public override bool Execute()
        {
            _resolvedCodeAnalysisRuleSet = GetResolvedRuleSetPath();

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
            if (string.IsNullOrEmpty(_codeAnalysisRuleSet))
            {
                return null;
            }

            if (_codeAnalysisRuleSet == Path.GetFileName(_codeAnalysisRuleSet))
            {
                // This is a simple file name.
                // Check if the file exists in the MSBuild project directory.
                if (!string.IsNullOrEmpty(_projectDirectory))
                {
                    string fullName = Path.Combine(_projectDirectory, _codeAnalysisRuleSet);
                    if (File.Exists(fullName))
                    {
                        return _codeAnalysisRuleSet;
                    }
                }

                // Try the rule set directories if we have some.
                if (_codeAnalysisRuleSetDirectories != null)
                {
                    foreach (string directory in _codeAnalysisRuleSetDirectories)
                    {
                        string fullName = Path.Combine(directory, _codeAnalysisRuleSet);
                        if (File.Exists(fullName))
                        {
                            return fullName;
                        }
                    }
                }
            }
            else if (!Path.IsPathRooted(_codeAnalysisRuleSet))
            {
                // This is a path relative to the project.
                if (!string.IsNullOrEmpty(_projectDirectory))
                {
                    string fullName = Path.Combine(_projectDirectory, _codeAnalysisRuleSet);
                    if (File.Exists(fullName))
                    {
                        return _codeAnalysisRuleSet;
                    }
                }
            }
            else if (File.Exists(_codeAnalysisRuleSet))
            {
                // This is a full path.
                return _codeAnalysisRuleSet;
            }

            // We can't resolve the rule set to any existing file.
            Log.LogWarningWithCodeFromResources("Compiler.UnableToFindRuleSet", _codeAnalysisRuleSet);
            return null;
        }

        #endregion
    }
}