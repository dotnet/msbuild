// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Tools.List.PackageReferences
{
    internal class ListPackageReferencesCommand : CommandBase
    {
        //The file or directory passed down by the command
        private readonly string _fileOrDirectory;

        public ListPackageReferencesCommand(
            ParseResult parseResult) : base(parseResult)
        {
            _fileOrDirectory = PathUtility.GetAbsolutePath(PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()),
                parseResult.GetValueForArgument(ListCommandParser.SlnOrProjectArgument));
        }

        public override int Execute()
        {
            return NuGetCommand.Run(TransformArgs());
        }

        internal static void EnforceOptionRules(ParseResult parseResult)
        {
            var mutexOptionCount = 0;
            mutexOptionCount += parseResult.HasOption(ListPackageReferencesCommandParser.DepreciatedOption) ? 1 : 0;
            mutexOptionCount += parseResult.HasOption(ListPackageReferencesCommandParser.OutdatedOption) ? 1 : 0;
            mutexOptionCount += parseResult.HasOption(ListPackageReferencesCommandParser.VulnerableOption) ? 1 : 0;
            if (mutexOptionCount > 1)
            {
                throw new GracefulException(LocalizableStrings.OptionsCannotBeCombined);
            }
        }

        private string[] TransformArgs()
        {
            var args = new List<string>
            {
                "package",
                "list",
            };

            args.Add(GetProjectOrSolution());

            args.AddRange(_parseResult.OptionValuesToBeForwarded(ListPackageReferencesCommandParser.GetCommand()));

            EnforceOptionRules(_parseResult);

            return args.ToArray();
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

            if (Directory.Exists(resultPath))
            {
                var possibleSolutionPath = Directory.GetFiles(resultPath, "*.sln", SearchOption.TopDirectoryOnly);

                //If more than a single sln file is found, an error is thrown since we can't determine which one to choose.
                if (possibleSolutionPath.Count() > 1)
                {
                    throw new GracefulException(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, resultPath);
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
                        throw new GracefulException(LocalizableStrings.NoProjectsOrSolutions, resultPath);
                    }
                    //A single project found, use it
                    else if (possibleProjectPath.Count() == 1)
                    {
                        return possibleProjectPath[0];
                    }
                    //More than one project found. Not sure which one to choose
                    else
                    {
                        throw new GracefulException(CommonLocalizableStrings.MoreThanOneProjectInDirectory, resultPath);
                    }
                }
            }

            if (!File.Exists(resultPath))
            {
                throw new GracefulException(LocalizableStrings.FileNotFound, resultPath);
            }

            return resultPath;
        }
    }
}
