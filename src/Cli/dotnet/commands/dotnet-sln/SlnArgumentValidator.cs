// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Sln
{
    internal static class SlnArgumentValidator
    {
        public enum CommandType
        {
            Add,
            Remove
        }
        public static void ParseAndValidateArguments(string _fileOrDirectory, IReadOnlyCollection<string> _arguments, CommandType commandType, bool _inRoot = false, string relativeRoot = null)
        {
            if (_arguments.Count == 0)
            {
                string message = commandType == CommandType.Add ? CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd : CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove;
                throw new GracefulException(message);
            }

            bool hasRelativeRoot = !string.IsNullOrEmpty(relativeRoot);
            
            if (_inRoot && hasRelativeRoot)
            {
                // These two options are mutually exclusive
                throw new GracefulException(LocalizableStrings.SolutionFolderAndInRootMutuallyExclusive);
            }

            var slnFile = _arguments.FirstOrDefault(path => path.EndsWith(".sln"));
            if (slnFile != null)
            {
                string args;
                if (_inRoot)
                {
                    args = $"--{SlnAddParser.InRootOption.Name} ";
                }
                else if (hasRelativeRoot)
                {
                    args = $"--{SlnAddParser.SolutionFolderOption.Name} {string.Join(" ", relativeRoot)} ";
                }
                else
                {
                    args = "";
                }

                var projectArgs = string.Join(" ", _arguments.Where(path => !path.EndsWith(".sln")));
                string command = commandType == CommandType.Add ? "add" : "remove";
                throw new GracefulException(new string[]
                {
                    string.Format(CommonLocalizableStrings.SolutionArgumentMisplaced, slnFile),
                    CommonLocalizableStrings.DidYouMean,
                    $"  dotnet sln {slnFile} {command} {args}{projectArgs}"
                });
            }
        }
    }
}
