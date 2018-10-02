// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.CodeDom.Compiler;

using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// This class provides methods for creating custom tasks for unit tests.
    /// </summary>
    internal static class CustomTaskHelper
    {
#if FEATURE_CODEDOM
        /// <summary>
        /// Create a task assembly given the specified task code.
        /// </summary>
        /// <param name="taskContents">The text of the C# code.</param>
        /// <returns>The name of the assembly.</returns>
        public static string GetAssemblyForTask(string taskContents)
        {
            string referenceAssembliesPath = ToolLocationHelper.GetPathToBuildTools(ToolLocationHelper.CurrentToolsVersion);

            string[] referenceAssemblies = new string[] { "System.dll", Path.Combine(referenceAssembliesPath, "Microsoft.Build.Framework.dll"), Path.Combine(referenceAssembliesPath, "Microsoft.Build.Utilities.Core.dll"), Path.Combine(referenceAssembliesPath, "Microsoft.Build.Tasks.Core.dll") };
            return GetAssemblyForTask(taskContents, referenceAssemblies);
        }

        /// <summary>
        /// Create a task assembly given the specified task code.
        /// </summary>
        /// <param name="taskContents">The text of the C# code.</param>
        /// <param name="referenceAssembliesForTask">The reference assemblies to pass to the task</param>
        /// <returns>The name of the assembly.</returns>
        public static string GetAssemblyForTask(string taskContents, string[] referenceAssembliesForTask)
        {
            CompilerParameters compilerParameters = new CompilerParameters(referenceAssembliesForTask);
            compilerParameters.GenerateInMemory = false;
            compilerParameters.TreatWarningsAsErrors = false;

            CodeDomProvider codegenerator = CodeDomProvider.CreateProvider("cs");
            CompilerResults results = codegenerator.CompileAssemblyFromSource(compilerParameters, taskContents);
            try
            {
                Assembly taskAssembly = results.CompiledAssembly;
                if (taskAssembly == null)
                {
                    StringBuilder builder = new StringBuilder();
                    foreach (CompilerError error in results.Errors)
                    {
                        if (!error.IsWarning)
                        {
                            builder.AppendLine(error.ToString());
                        }
                    }

                    throw new ArgumentException(builder.ToString());
                }

                return taskAssembly.Location;
            }
            catch (FileNotFoundException)
            {
                // This occurs if there is a failure to compile the assembly.  We just pass through because we will take care of the failure below.
            }

            return null;
        }
#endif
    }
}
