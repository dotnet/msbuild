// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Security;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using System.Globalization;

using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is used to track tasks used by a project. Tasks are declared in project files with the &lt;UsingTask&gt; tag.
    /// Task and assembly names must be specified per .NET guidelines, however, the names do not need to be fully qualified if
    /// they provide enough information to locate the tasks they refer to. Assemblies can also be referred to using file paths --
    /// this is useful when it is not possible/desirable to place task assemblies in the GAC, or in the same directory as MSBuild.
    /// </summary>
    /// <remarks>
    /// 1) specifying a task assembly using BOTH its assembly name (strong or weak) AND its file path is not allowed
    /// 2) when specifying the assembly name, the file extension (usually ".dll") must NOT be specified
    /// 3) when specifying the assembly file, the file extension MUST be specified
    /// </remarks>
    /// <example>
    /// &lt;UsingTask TaskName="Microsoft.Build.Tasks.Csc"                     ==> look for the "Csc" task in the
    ///            AssemblyName="Microsoft.Build.Tasks"/&gt;                       weakly-named "Microsoft.Build.Tasks" assembly
    /// 
    /// &lt;UsingTask TaskName="t1"                                            ==> look for the "t1" task in the
    ///            AssemblyName="mytasks, Culture=en, Version=1.0.0.0"/&gt;        strongly-named "mytasks" assembly
    ///
    /// &lt;UsingTask TaskName="foo"                                           ==> look for the "foo" task in the
    ///            AssemblyFile="$(MyDownloadedTasks)\utiltasks.dll"/&gt;          "utiltasks" assembly file
    ///
    /// &lt;UsingTask TaskName="UtilTasks.Bar"                                 ==> invalid task declaration
    ///            AssemblyName="utiltasks.dll"
    ///            AssemblyFile="$(MyDownloadedTasks)\"/&gt;
    /// </example>
    /// <owner>SumedhK</owner>
    internal sealed class TaskRegistry : ITaskRegistry
    {
        #region Constructors
        /// <summary>
        /// Default constructor does no work because the tables are initialized lazily when a task is registered
        /// </summary>
        internal TaskRegistry()
        {
            registeredTasks = null;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the collection of task declarations created by parsing the &lt;UsingTask&gt; XML.
        /// Used for unit tests only.
        /// </summary>
        internal Hashtable AllTaskDeclarations
        {
            get
            {
                return registeredTasks;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Removes all entries from the registry.
        /// </summary>
        public void Clear()
        {
            // The hashtables are lazily allocated if they are needed
            if (registeredTasks != null)
            {
                cachedTaskClassesWithExactMatch.Clear();
                cachedTaskClassesWithFuzzyMatch.Clear();
                registeredTasks.Clear();
            }
        }

        /// <summary>
        /// Given a task name, this method retrieves the task class. If the task has been requested before, it will be found in
        /// the class cache; otherwise, &lt;UsingTask&gt; declarations will be used to search the appropriate assemblies.
        /// </summary>
        /// <param name="taskName"></param>
        /// <param name="taskProjectFile"></param>
        /// <param name="taskNode"></param>
        /// <param name="exactMatchRequired"></param>
        /// <param name="loggingServices"></param>
        /// <param name="buildEventContext"></param>
        /// <param name="taskClass"></param>
        /// <returns>true, if task is found</returns>
        public bool GetRegisteredTask
        (
            string taskName,
            string taskProjectFile,
            XmlNode taskNode,
            bool exactMatchRequired,
            EngineLoggingServices loggingServices,
            BuildEventContext buildEventContext,
            out LoadedType taskClass
        )
        {
            taskClass = null;

            // If there are no using tags in the project don't bother caching or looking for tasks
            if (registeredTasks == null)
            {
                return false;
            }

            Hashtable cachedTaskClasses = exactMatchRequired ? this.cachedTaskClassesWithExactMatch : this.cachedTaskClassesWithFuzzyMatch;
            
            if (cachedTaskClasses.Contains(taskName))
            {
                // Caller has asked us before for this same task name, and for the same value of "bool exactMatchRequired".
                // Return whatever the previous result was, even if it was null.   Why would the result be different than
                // it was before?  NOTE:  Hash tables CAN have "null" as their value, and this still returns "true" for Contains(...).
                taskClass = (LoadedType) cachedTaskClasses[taskName];
            }
            else
            {
                Hashtable registeredTasksFound;

                // look for the given task name in the registry; if not found, gather all registered task names that partially
                // match the given name
                if (FindRegisteredTasks(taskName, exactMatchRequired, out registeredTasksFound))
                {
                    foreach (DictionaryEntry registeredTaskFound in registeredTasksFound)
                    {
                        string mostSpecificTaskName = (string)registeredTaskFound.Key;

                        // if the given task name is longer than the registered task name
                        if (taskName.Length > ((string)registeredTaskFound.Key).Length)
                        {
                            // we will use the longer name to help disambiguate between multiple matches
                            mostSpecificTaskName = taskName;
                        }

                        if (GetTaskFromAssembly(mostSpecificTaskName, (ArrayList)registeredTaskFound.Value, taskProjectFile, taskNode, loggingServices, buildEventContext, out taskClass))
                        {
                            // Whilst we are within the processing of the task, we haven't actually started executing it, so
                            // our using task message needs to be in the context of the target. However any errors should be reported
                            // at the point where the task appears in the project.
                            BuildEventContext usingTaskContext = new BuildEventContext(buildEventContext.NodeId, buildEventContext.TargetId, buildEventContext.ProjectContextId, BuildEventContext.InvalidTaskId);
                            loggingServices.LogComment(usingTaskContext, "TaskFound", taskName, taskClass.Assembly.ToString());
                            break;
                        }
                    }
                }

                // Cache the result, even if it is null.  We should never again do the work we just did, for this task name.
                cachedTaskClasses[taskName] = taskClass;
            }

            return (taskClass != null);
        }

        /// <summary>
        /// Searches all task declarations for the given task name. If no exact match is found, looks for partial matches.
        /// </summary>
        /// <remarks>
        /// It is possible to get multiple partial matches for a task name that is not fully qualified.
        /// NOTE: this method is marked internal for unit testing purposes only.
        /// </remarks>
        /// <param name="taskName"></param>
        /// <param name="exactMatchRequired"></param>
        /// <param name="registeredTasksFound"></param>
        /// <returns>true, if given task name matches one or more task declarations</returns>
        internal bool FindRegisteredTasks(string taskName, bool exactMatchRequired, out Hashtable registeredTasksFound)
        {
            registeredTasksFound = new Hashtable(StringComparer.OrdinalIgnoreCase);
            ArrayList taskAssemblies = registeredTasks != null ? (ArrayList)registeredTasks[taskName] : null;

            // if we find an exact match
            if (taskAssemblies != null)
            {
                // we're done
                registeredTasksFound[taskName] = taskAssemblies;
            }
            else if (!exactMatchRequired)
            {
                // look through all task declarations for partial matches
                foreach (DictionaryEntry registeredTask in registeredTasks)
                {
                    if (TypeLoader.IsPartialTypeNameMatch(taskName, (string)registeredTask.Key))
                    {
                        registeredTasksFound[registeredTask.Key] = registeredTask.Value;
                    }
                }
            }

            return (registeredTasksFound.Count > 0);
        }

        /// <summary>
        /// Given a task name and a list of assemblies, this helper method checks if the task exists in any of the assemblies.
        /// </summary>
        /// <remarks>
        /// If the task name is fully qualified, then a match (if any) is unambiguous; otherwise, if there are multiple tasks with
        /// the same name in different namespaces/assemblies, the first task found will be returned.
        /// </remarks>
        /// <param name="taskName"></param>
        /// <param name="taskAssemblies"></param>
        /// <param name="taskProjectFile"></param>
        /// <param name="taskNode"></param>
        /// <param name="loggingServices"></param>
        /// <param name="buildEventContext"></param>
        /// <param name="taskClass"></param>
        /// <returns>true, if task is successfully loaded</returns>
        private bool GetTaskFromAssembly
        (
            string taskName,
            ArrayList taskAssemblies,
            string taskProjectFile,
            XmlNode taskNode,
            EngineLoggingServices loggingServices,
            BuildEventContext buildEventContext,
            out LoadedType taskClass
        )
        {
            taskClass = null;

            foreach (AssemblyLoadInfo assembly in taskAssemblies)
            {
                try
                {
                    taskClass = typeLoader.Load(taskName, assembly);
                }
                catch (TargetInvocationException e)
                {
                    // Exception thrown by the called code itself
                    // Log the stack, so the task vendor can fix their code
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskNode, "TaskLoadFailure", taskName, assembly.ToString(), Environment.NewLine + e.InnerException.ToString());
                }
                catch (ReflectionTypeLoadException e)
                {
                    // ReflectionTypeLoadException.LoaderExceptions may contain nulls
                    foreach (Exception exception in e.LoaderExceptions)
                    {
                        if (exception != null)
                        {
                            loggingServices.LogError(buildEventContext, new BuildEventFileInfo(taskProjectFile), "TaskLoadFailure", taskName, assembly.ToString(), exception.Message);
                        }
                    }

                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskNode, "TaskLoadFailure", taskName, assembly.ToString(), e.Message);
                }
                catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
                {
                    if (ExceptionHandling.NotExpectedReflectionException(e))
                        throw;

                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskNode, "TaskLoadFailure", taskName, assembly.ToString(), e.Message);
                }
                
                if (taskClass != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads the given &lt;UsingTask&gt; tag and saves the task information specified in it.
        /// </summary>
        /// <param name="usingTask"></param>
        /// <param name="expander"></param>
        /// <param name="loggingServices"></param>
        /// <param name="buildEventContext"></param>
        public void RegisterTask(UsingTask usingTask, Expander expander, EngineLoggingServices loggingServices, BuildEventContext buildEventContext)
        {
            if (
                // if the <UsingTask> tag doesn't have a condition on it
                    (usingTask.Condition == null)
                    ||
                // or if the condition holds
                    Utilities.EvaluateCondition(usingTask.Condition, usingTask.ConditionAttribute, expander,
                        null, ParserOptions.AllowProperties | ParserOptions.AllowItemLists, loggingServices, buildEventContext)
                )
            {
                // Lazily allocate the hashtables if they are needed
                if (registeredTasks == null)
                {
                    cachedTaskClassesWithExactMatch = new Hashtable(StringComparer.OrdinalIgnoreCase);
                    cachedTaskClassesWithFuzzyMatch = new Hashtable(StringComparer.OrdinalIgnoreCase);
                    registeredTasks = new Hashtable(StringComparer.OrdinalIgnoreCase);
                }

                string assemblyName = null;
                string assemblyFile = null;

                if (usingTask.AssemblyName != null)
                {
                    // expand out all embedded properties and items in the assembly name
                    assemblyName = expander.ExpandAllIntoString(usingTask.AssemblyName, usingTask.AssemblyNameAttribute);

                    ProjectErrorUtilities.VerifyThrowInvalidProject(assemblyName.Length > 0,
                        usingTask.AssemblyNameAttribute, "InvalidEvaluatedAttributeValue", assemblyName, usingTask.AssemblyName, XMakeAttributes.assemblyName, XMakeElements.usingTask);
                }
                else
                {
                    // expand out all embedded properties and items in the assembly file/path
                    assemblyFile = expander.ExpandAllIntoString(usingTask.AssemblyFile, usingTask.AssemblyFileAttribute);

                    ProjectErrorUtilities.VerifyThrowInvalidProject(assemblyFile.Length > 0,
                        usingTask.AssemblyFileAttribute, "InvalidEvaluatedAttributeValue", assemblyFile, usingTask.AssemblyFile, XMakeAttributes.assemblyFile, XMakeElements.usingTask);

                    // figure out the directory of the project in which this <UsingTask> node was defined
                    string projectFile = XmlUtilities.GetXmlNodeFile(usingTask.TaskNameAttribute.OwnerElement, String.Empty);
                    string projectDir = (projectFile.Length > 0)
                        ? Path.GetDirectoryName(projectFile)
                        : String.Empty;

                    // ensure the assembly file/path is relative to the project in which this <UsingTask> node was defined -- we
                    // don't want paths from imported projects being interpreted relative to the main project file
                    try
                    {
                        assemblyFile = Path.Combine(projectDir, assemblyFile);
                    }
                    catch (ArgumentException ex)
                    {
                        // Invalid chars in AssemblyFile path
                        ProjectErrorUtilities.VerifyThrowInvalidProject(false, usingTask.AssemblyFileAttribute,
                            "InvalidAttributeValueWithException", assemblyFile,
                            XMakeAttributes.assemblyFile, XMakeElements.usingTask, ex.Message);
                    }

                }

                AssemblyLoadInfo taskAssembly = new AssemblyLoadInfo(assemblyName, assemblyFile);

                // expand out all embedded properties and items
                string taskName = expander.ExpandAllIntoString(usingTask.TaskName, usingTask.TaskNameAttribute);

                ProjectErrorUtilities.VerifyThrowInvalidProject(taskName.Length > 0,
                    usingTask.TaskNameAttribute, "InvalidEvaluatedAttributeValue", taskName, usingTask.TaskName, XMakeAttributes.taskName, XMakeElements.usingTask);

                // since more than one task can have the same name, we want to keep track of all assemblies that are declared to
                // contain tasks with a given name...
                ArrayList taskAssemblies = (ArrayList)registeredTasks[taskName];

                if (taskAssemblies == null)
                {
                    taskAssemblies = new ArrayList();
                    registeredTasks[taskName] = taskAssemblies;
                }

                taskAssemblies.Add(taskAssembly);
            }
        }

        /// <summary>
        /// Checks if the given type is a task class.
        /// </summary>
        /// <remarks>This method is used as a TypeFilter delegate.</remarks>
        /// <owner>SumedhK</owner>
        /// <param name="type"></param>
        /// <param name="unused"></param>
        /// <returns>true, if specified type is a task</returns>
        private static bool IsTaskClass(Type type, object unused)
        {
            return (type.IsClass &&
                !type.IsAbstract &&
                (type.GetInterface("ITask") != null));
        }
        #endregion

        #region Data
        // cache of tasks that have been verified to exist in their respective assemblies
        private Hashtable cachedTaskClassesWithExactMatch;
        private Hashtable cachedTaskClassesWithFuzzyMatch;

        /// <summary>
        /// Cache of task declarations i.e. the &lt;UsingTask&gt; tags fed to this registry.
        /// </summary>
        private Hashtable registeredTasks;

        // used for finding tasks when reflecting through assemblies
        private readonly TypeLoader typeLoader = new TypeLoader(new TypeFilter(IsTaskClass));
        #endregion
    }
}
