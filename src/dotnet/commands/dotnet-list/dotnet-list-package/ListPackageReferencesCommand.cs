// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Tools.List.PackageReferences
{
    internal class ListPackageReferencesCommand : CommandBase
    {
        //The file or directory passed down by the command
        private readonly string _fileOrDirectory;
        private AppliedOption _appliedCommand;

        public ListPackageReferencesCommand(
            AppliedOption appliedCommand,
            ParseResult parseResult) : base(parseResult)
        {
            if (appliedCommand == null)
            {
                throw new ArgumentNullException(nameof(appliedCommand));
            }

            // Gets the absolute path of the given path
            _fileOrDirectory = PathUtility.GetAbsolutePath(PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()),
                                                           appliedCommand.Arguments.Single());

            FileAttributes attr = File.GetAttributes(_fileOrDirectory);

            if (attr.HasFlag(FileAttributes.Directory))
            {
                _fileOrDirectory = PathUtility.EnsureTrailingSlash(_fileOrDirectory);
            }

            _appliedCommand = appliedCommand["package"];
        }

        public override int Execute()
        {
            var result = NuGetCommand.Run(TransformArgs());

            return result;
        }

        private string[] TransformArgs()
        {
            var args = new List<string>
            {
                "package",
                "list",
            };

            args.Add(GetProjectOrSolution());

            if (_appliedCommand.HasOption("outdated"))
            {
                args.Add("--outdated");
            }

            if (_appliedCommand.HasOption("include-transitive"))
            {
                args.Add("--include-transitive");
            }

            if (_appliedCommand.HasOption("framework"))
            {
                //Forward framework as multiple flags
                foreach (var framework in _appliedCommand.AppliedOptions["framework"].Arguments)
                {
                    args.Add("--framework");
                    args.Add(framework);
                }
            }

            if (_appliedCommand.HasOption("include-prerelease"))
            {
                CheckForOutdated("--include-prerelease");
                args.Add("--include-prerelease");
            }

            if (_appliedCommand.HasOption("highest-patch"))
            {
                CheckForOutdated("--highest-patch");
                args.Add("--highest-patch");
            }

            if (_appliedCommand.HasOption("highest-minor"))
            {
                CheckForOutdated("--highest-minor");
                args.Add("--highest-minor");
            }

            if (_appliedCommand.HasOption("config"))
            {
                CheckForOutdated("--config");
                args.Add("--config");
                //Config path absolute path
                var configPath = PathUtility.GetAbsolutePath(PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()),
                                        _appliedCommand.AppliedOptions["config"].Arguments.Single());
                args.Add(configPath);
            }

            if (_appliedCommand.HasOption("source"))
            {
                CheckForOutdated("--source");
                //Forward source as multiple flags
                foreach (var source in _appliedCommand.AppliedOptions["source"].Arguments)
                {
                    args.Add("--source");
                    args.Add(source);
                }
            }

            return args.ToArray();
        }

        /// <summary>
        /// A check for the outdated specific options. If --outdated not present,
        /// these options must not be used, so error is thrown
        /// </summary>
        /// <param name="option"></param>
        private void CheckForOutdated(string option)
        {
            if (!_appliedCommand.HasOption("outdated"))
            {
                throw new Exception(string.Format(LocalizableStrings.OutdatedOptionOnly, option));

            }
            
        }

        /// <summary>
        /// Gets a solution file or a project file from a given directory.
        /// If the given path is a file, it just returns it after checking
        /// it exists.
        /// </summary>
        /// <returns>Path to send to the command</returns>
        private string GetProjectOrSolution()
        {
            string resultPath = _fileOrDirectory;
            FileAttributes attr = File.GetAttributes(resultPath);
            
            if (attr.HasFlag(FileAttributes.Directory))
            {
                var possibleSolutionPath = Directory.GetFiles(resultPath, "*.sln", SearchOption.TopDirectoryOnly);

                //If more than a single sln file is found, an error is thrown since we can't determine which one to choose.
                if (possibleSolutionPath.Count() > 1)
                {
                    throw new Exception(LocalizableStrings.MultipleSolutionsFound + Environment.NewLine + string.Join(Environment.NewLine, possibleSolutionPath.ToArray()));
                }
                //If a single solution is found, use it.
                else if (possibleSolutionPath.Count() == 1)
                {
                    return possibleSolutionPath[0];
                }
                //If no solutions are found, look for a project file
                else
                {
                    var possibleProjectPath = Directory.GetFiles(resultPath, "*.*proj", SearchOption.TopDirectoryOnly)
                                              .Where(path => !path.EndsWith(".xproj", StringComparison.OrdinalIgnoreCase))
                                              .ToList();

                    //No projects found throws an error that no sln nor projs were found
                    if (possibleProjectPath.Count() == 0)
                    {
                        throw new Exception(LocalizableStrings.NoProjectsOrSolutions);
                    }
                    //A single project found, use it
                    else if (possibleProjectPath.Count() == 1)
                    {
                        return possibleProjectPath[0];
                    }
                    //More than one project found. Not sure which one to choose
                    else
                    {
                        throw new Exception(LocalizableStrings.MultipleProjectsFound + Environment.NewLine + string.Join(Environment.NewLine, possibleProjectPath.ToArray()));
                    }
                }
            }
            
            //Make sure the file exists
            if (!File.Exists(resultPath))
            {
                throw new FileNotFoundException(LocalizableStrings.FileNotFound, resultPath);
            }
            
            return resultPath;
        }
    }
}
