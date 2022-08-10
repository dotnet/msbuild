// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.VisualBasic.CompilerServices;

namespace Microsoft.DotNet.Cli
{
    abstract class ProjectLocator
    {
        /// <returns>A project instance that will be targeted to publish/pack, etc. null if one does not exist.</returns>
        public static ProjectInstance GetTargetedProject(IEnumerable<string> slnOrProjectArgs)
        {
            string potentialProject = "";

            foreach (string arg in slnOrProjectArgs.Append(Directory.GetCurrentDirectory()))
            {
                if (IsValidProjectFilePath(arg))
                {
                    return TryGetProjectInstance(arg);
                }
                else if (Directory.Exists(arg)) // We should get here if the user did not provide a .proj or a .sln
                {
                    try
                    {
                        return TryGetProjectInstance(MsbuildProject.GetProjectFileFromDirectory(arg).FullName);
                    }
                    catch (GracefulException)
                    {
                        return GetSlnProject(slnOrProjectArgs);
                    } // If nothing can be found: that's caught by MSBuild XMake::ProcessProjectSwitch -- don't change the behavior by failing here. 
                }
            }

            return string.IsNullOrEmpty(potentialProject) ? null : TryGetProjectInstance(potentialProject);
        }

        /// <returns>Creates a ProjectInstance if the project is valid, elsewise, fails..</returns>
        private static ProjectInstance TryGetProjectInstance(string projectPath)
        {
            try
            {
                return new ProjectInstance(projectPath);
            }
            catch (Exception) // Catch failed file access, or invalid project files that cause errors when read into memory,
            {
                Reporter.Output.WriteLine(CommonLocalizableStrings.ProjectDeductionFailure.Yellow() + " " + projectPath.Yellow());
            }
            return null;
        }

        private static bool IsValidProjectFilePath(string path)
        {
            return File.Exists(path) && LikeOperator.LikeString(path, "*.*proj", VisualBasic.CompareMethod.Text);
        }

        /// <returns>Returns null as we don't want this feature for now.</returns>
        static public ProjectInstance GetSlnProject(IEnumerable<string> potentialSlnPaths) => null;
    }
}
