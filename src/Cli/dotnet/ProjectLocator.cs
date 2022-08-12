// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using Microsoft.VisualBasic.CompilerServices;

namespace Microsoft.DotNet.Cli
{
    abstract class ProjectLocator
    {
        /// <returns>A project instance that will be targeted to publish/pack, etc. null if one does not exist.</returns>
        public abstract ProjectInstance GetTargetedProject(IEnumerable<string> slnOrProjectArgs, string slnProjectPropertytoCheck = "");

        /// <returns>The top-level project (first if multiple exist) in a SLN. Returns null if no top level project. Throws exception if two top level projects disagree
        /// in the configuration property to check.</returns>
        public abstract ProjectInstance GetSlnProject(string potentialSlnPath, string slnProjectConfigPropertytoCheck = "");

        /// <returns>Creates a ProjectInstance if the project is valid, elsewise, fails..</returns>
        protected static ProjectInstance TryGetProjectInstance(string projectPath)
        {
            try
            {
                return new ProjectInstance(projectPath);
            }
            catch (Exception e) // Catch failed file access, or invalid project files that cause errors when read into memory,
            {
                Reporter.Output.WriteLine(e.Message.Yellow());
            }
            return null;
        }

        protected static bool IsValidProjectFilePath(string path)
        {
            return File.Exists(path) && LikeOperator.LikeString(path, "*.*proj", VisualBasic.CompareMethod.Text);
        }

        protected static bool ProjectHasUserCustomizedConfiguration(ProjectInstance project)
        {
            return project.GlobalProperties.ContainsKey("Configuration");
        }


    }
}
