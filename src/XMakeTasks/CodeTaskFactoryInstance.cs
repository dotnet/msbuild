//-----------------------------------------------------------------------
// <copyright file="CodeTaskFactoryInstance.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>A code task factory  instance which is instantiated for each batch</summary>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Xml;
using System.IO;

using Microsoft.Build.Framework;
using System.CodeDom;
using Microsoft.Build.Utilities;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A task factory instance which actually create an ITaskInstance, this factory contains the data that needs to be refreshed for each task invocation.
    /// </summary>
    public class CodeTaskFactoryInstance : ITaskFactory
    {
        /// <summary>
        /// Compiled assembly to use when instantiating the Itask
        /// </summary>
        private Assembly compiledAssembly;

        /// <summary>
        /// The instantiated Itask instance
        /// </summary>
        private ITask taskInstance;

        /// <summary>
        /// Name of the task
        /// </summary>
         private string nameOfTask;

        /// <summary>
        /// Create a new CodeTaskFactoryInstance using the compiled assembly and the task name
        /// </summary>
        public CodeTaskFactoryInstance(Assembly compiledAssembly, string taskName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskName, "taskName");
            ErrorUtilities.VerifyThrowArgumentNull(compiledAssembly, "compiledAssembly");

            nameOfTask = taskName;
            this.compiledAssembly = compiledAssembly;
        }

        /// <summary>
        /// Create an instance of the ITask
        /// </summary>
        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost)
        {
            // The assembly will have been compiled during class factory initialization, create an instance of it
            if (this.compiledAssembly != null)
            {
                // In order to use the resource strings from the tasks assembly we need to register the resources with the task logging helper.
                TaskLoggingHelper log = new TaskLoggingHelper(taskFactoryLoggingHost, nameOfTask);
                log.TaskResources = AssemblyResources.PrimaryResources;
                log.HelpKeywordPrefix = "MSBuild.";

                Type[] exportedTypes = this.compiledAssembly.GetExportedTypes();

                Type fullNameMatch = null;
                Type partialNameMatch = null;

                foreach (Type exportedType in exportedTypes)
                {
                    string exportedTypeName = exportedType.FullName;
                    if (exportedTypeName.Equals(nameOfTask, StringComparison.OrdinalIgnoreCase))
                    {
                        fullNameMatch = exportedType;
                        break;
                    } 
                    else if (partialNameMatch == null && exportedTypeName.EndsWith(nameOfTask, StringComparison.OrdinalIgnoreCase))
                    {
                        partialNameMatch = exportedType;
                    }
                }

                if (fullNameMatch == null && partialNameMatch == null)
                {
                    log.LogErrorWithCodeFromResources("CodeTaskFactory.CouldNotFindTaskInAssembly", nameOfTask);
                    return null;
                }

                this.taskInstance = this.compiledAssembly.CreateInstance(fullNameMatch != null ? fullNameMatch.FullName : partialNameMatch.FullName, true) as ITask;

                if (this.taskInstance == null)
                {
                    log.LogErrorWithCodeFromResources("CodeTaskFactory.NeedsITaskInterface", nameOfTask);
                    return null;
                }

                return this.taskInstance;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Clean up any state created when the task was instantiated
        /// </summary>
        public void CleanupTask()
        {
            compiledAssembly = null;
            taskInstance = null;
        }

        /// <summary>
        /// Given a property info and a value set the parametervalue on the ITaskInstance
        /// </summary>
        public bool SetTaskParameterValue(PropertyInfo parameter, object parameterValue)
        {
            bool success = false;

            PropertyInfo propInfo = this.taskInstance.GetType().GetProperty
                (
                parameter.Name,
                BindingFlags.ExactBinding | BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public
                );

            propInfo.SetValue(taskInstance, parameterValue, null);
            success = true;

            return success;
        }

        /// <summary>
        /// Given a property info get the value of the task parameter
        /// </summary>
        public object GetTaskParameterValue(PropertyInfo parameter)
        {
            // We need to work with the real propertyInfo object so that we can use reflection to collect the value
            // so use our factory property info's name to get the real one, then use it.
            PropertyInfo propInfo = this.taskInstance.GetType().GetProperty
                (
                parameter.Name,
                BindingFlags.ExactBinding | BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public
                );
            return propInfo.GetValue(taskInstance, null);
        }
    }
}

