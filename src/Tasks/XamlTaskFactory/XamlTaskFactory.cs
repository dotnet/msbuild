// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary> The task factory for Xaml data driven tasks. </summary>
//-----------------------------------------------------------------------

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Text;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Xml;
using System.IO;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Xaml;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// The task factory provider for XAML tasks.
    /// </summary>
    public class XamlTaskFactory : ITaskFactory
    {
        /// <summary>
        /// The namespace we put the task in.
        /// </summary>
        private const string XamlTaskNamespace = "XamlTaskNamespace";

        /// <summary>
        /// The compiled task assembly.
        /// </summary>
        private Assembly _taskAssembly;

        /// <summary>
        /// The task type.
        /// </summary>
        private Type _taskType;

        /// <summary>
        /// The name of the task pulled from the XAML.
        /// </summary>
        public string TaskName
        {
            get;
            private set;
        }

        /// <summary>
        /// The namespace of the task pulled from the XAML.
        /// </summary>
        public string TaskNamespace
        {
            get;
            private set;
        }

        /// <summary>
        /// The contents of the UsingTask body.
        /// </summary>
        public string TaskElementContents
        {
            get;
            private set;
        }

        /// <summary>
        /// The name of this factory. This factory name will be used in error messages. For example
        /// Task "Mytask" failed to load from "FactoryName".
        /// </summary>
        public string FactoryName
        {
            get
            {
                return "XamlTaskFactory";
            }
        }

        /// <summary>
        /// The task type object.
        /// </summary>
        public Type TaskType
        {
            get
            {
                if (_taskType == null)
                {
                    _taskType = _taskAssembly.GetType(String.Concat(XamlTaskNamespace, ".", TaskName), true);
                }

                return _taskType;
            }
        }

        /// <summary>
        /// MSBuild engine will call this to initialize the factory. This should initialize the factory enough so that the factory can be asked
        ///  whether or not task names can be created by the factory.
        /// </summary>
        public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> taskParameters, string taskElementContents, IBuildEngine taskFactoryLoggingHost)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskName, "taskName");
            ErrorUtilities.VerifyThrowArgumentNull(taskParameters, "taskParameters");

            TaskLoggingHelper log = new TaskLoggingHelper(taskFactoryLoggingHost, taskName);
            log.TaskResources = AssemblyResources.PrimaryResources;
            log.HelpKeywordPrefix = "MSBuild.";

            if (taskElementContents == null)
            {
                log.LogErrorWithCodeFromResources("Xaml.MissingTaskBody");
                return false;
            }

            TaskElementContents = taskElementContents.Trim();

            // Attempt to load the task
            TaskParser parser = new TaskParser();

            bool parseSuccessful = parser.Parse(TaskElementContents, taskName);

            TaskName = parser.GeneratedTaskName;
            TaskNamespace = parser.Namespace;
            TaskGenerator generator = new TaskGenerator(parser);

            CodeCompileUnit dom = generator.GenerateCode();

            string pathToMSBuildBinaries = ToolLocationHelper.GetPathToBuildTools(ToolLocationHelper.CurrentToolsVersion);

            // create the code generator options    
            // Since we are running msbuild 12.0 these had better load.
            CompilerParameters compilerParameters = new CompilerParameters
                (
                    new string[]
                    {
                        "System.dll",
                        Path.Combine(pathToMSBuildBinaries, "Microsoft.Build.Framework.dll"),
                        Path.Combine(pathToMSBuildBinaries, "Microsoft.Build.Utilities.Core.dll"),
                        Path.Combine(pathToMSBuildBinaries, "Microsoft.Build.Tasks.Core.dll")
                    }

                );

            compilerParameters.GenerateInMemory = true;
            compilerParameters.TreatWarningsAsErrors = false;

            // create the code provider
            CodeDomProvider codegenerator = CodeDomProvider.CreateProvider("cs");
            CompilerResults results;
            bool debugXamlTask = Environment.GetEnvironmentVariable("MSBUILDWRITEXAMLTASK") == "1";
            if (debugXamlTask)
            {
                using (StreamWriter outputWriter = new StreamWriter(taskName + "_XamlTask.cs"))
                {
                    CodeGeneratorOptions options = new CodeGeneratorOptions();
                    options.BlankLinesBetweenMembers = true;
                    options.BracingStyle = "C";

                    codegenerator.GenerateCodeFromCompileUnit(dom, outputWriter, options);
                }

                results = codegenerator.CompileAssemblyFromFile(compilerParameters, taskName + "_XamlTask.cs");
            }
            else
            {
                results = codegenerator.CompileAssemblyFromDom(compilerParameters, new[] { dom });
            }

            try
            {
                _taskAssembly = results.CompiledAssembly;
            }
            catch (FileNotFoundException)
            {
                // This occurs if there is a failure to compile the assembly.  We just pass through because we will take care of the failure below.
            }

            if (_taskAssembly == null)
            {
                StringBuilder errorList = new StringBuilder();
                errorList.AppendLine();
                foreach (CompilerError error in results.Errors)
                {
                    if (error.IsWarning)
                    {
                        continue;
                    }

                    if (debugXamlTask)
                    {
                        errorList.AppendLine(String.Format(Thread.CurrentThread.CurrentUICulture, "({0},{1}) {2}", error.Line, error.Column, error.ErrorText));
                    }
                    else
                    {
                        errorList.AppendLine(error.ErrorText);
                    }
                }

                log.LogErrorWithCodeFromResources("Xaml.TaskCreationFailed", errorList.ToString());
            }

            return !log.HasLoggedErrors;
        }

        /// <summary>
        /// Create an instance of the task to be used.
        /// </summary>
        /// <param name="taskFactoryLoggingHost">The task factory logging host will log messages in the context of the task.</param>
        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost)
        {
            string fullTaskName = String.Concat(TaskNamespace, ".", TaskName);
            return (ITask)_taskAssembly.CreateInstance(fullTaskName);
        }

        /// <summary>
        /// Cleans up any context or state that may have been built up for a given task.
        /// </summary>
        /// <param name="task">The task to clean up.</param>
        /// <remarks>
        /// For many factories, this method is a no-op.  But some factories may have built up
        /// an AppDomain as part of an individual task instance, and this is their opportunity
        /// to shutdown the AppDomain.
        /// </remarks>
        public void CleanupTask(ITask task)
        {
            ErrorUtilities.VerifyThrowArgumentNull(task, "task");
        }

        /// <summary>
        /// Get a list of parameters for the task.
        /// </summary>
        public TaskPropertyInfo[] GetTaskParameters()
        {
            PropertyInfo[] infos = TaskType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var propertyInfos = new TaskPropertyInfo[infos.Length];
            for (int i = 0; i < infos.Length; i++)
            {
                propertyInfos[i] = new TaskPropertyInfo(
                    infos[i].Name,
                    infos[i].PropertyType,
                    infos[i].GetCustomAttributes(typeof(OutputAttribute), false).Length > 0,
                    infos[i].GetCustomAttributes(typeof(RequiredAttribute), false).Length > 0);
            }

            return propertyInfos;
        }
    }
}
