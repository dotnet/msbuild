// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface that a task factory Instance should implement if it wants to be able to
    /// use new UsingTask parameters such as Runtime and Architecture. 
    /// </summary>
    public interface ITaskFactory2 : ITaskFactory
    {
        /// <summary>
        /// Initializes this factory for instantiating tasks with a particular inline task block and a set of UsingTask parameters.  MSBuild
        /// provides an implementation of this interface, TaskHostFactory, that uses "Runtime", with values "CLR2", "CLR4", "CurrentRuntime", 
        /// and "*" (Any); and "Architecture", with values "x86", "x64", "CurrentArchitecture", and "*" (Any).  An implementer of ITaskFactory2 
        /// can choose to use these pre-defined Runtime and Architecture values, or can specify new values for these parameters.  
        /// </summary>
        /// <param name="taskName">Name of the task.</param>
        /// <param name="factoryIdentityParameters">Special parameters that the task factory can use to modify how it executes tasks, 
        /// such as Runtime and Architecture.  The key is the name of the parameter and the value is the parameter's value. This 
        /// is the set of parameters that was set on the UsingTask using e.g. the UsingTask Runtime and Architecture parameters.</param>
        /// <param name="parameterGroup">The parameter group.</param>
        /// <param name="taskBody">The task body.</param>
        /// <param name="taskFactoryLoggingHost">The task factory logging host.</param>
        /// <returns>A value indicating whether initialization was successful.</returns>
        /// <remarks>
        /// <para>MSBuild engine will call this to initialize the factory. This should initialize the factory enough so that the 
        /// factory can be asked whether or not task names can be created by the factory.  If a task factory implements ITaskFactory2, 
        /// this Initialize method will be called in place of ITaskFactory.Initialize.</para>
        /// <para>
        /// The taskFactoryLoggingHost will log messages in the context of the target where the task is first used.
        /// </para>
        /// </remarks>
        bool Initialize(string taskName, IDictionary<string, string> factoryIdentityParameters, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost);

        /// <summary>
        /// Create an instance of the task to be used, with an optional set of "special" parameters set on the individual task invocation using 
        /// the MSBuildRuntime and MSBuildArchitecture default task parameters.  MSBuild provides an implementation of this interface, 
        /// TaskHostFactory, that uses "MSBuildRuntime", with values "CLR2", "CLR4", "CurrentRuntime", and "*" (Any); and "MSBuildArchitecture", 
        /// with values "x86", "x64", "CurrentArchitecture", and "*" (Any).  An implementer of ITaskFactory2 can choose to use these pre-defined 
        /// MSBuildRuntime and MSBuildArchitecture values, or can specify new values for these parameters.  
        /// </summary>
        /// <param name="taskFactoryLoggingHost">
        /// The task factory logging host will log messages in the context of the task.
        /// </param>
        /// <param name="taskIdentityParameters">
        /// Special parameters that the task factory can use to modify how it executes tasks, such as Runtime and Architecture.  
        /// The key is the name of the parameter and the value is the parameter's value.  This is the set of parameters that was 
        /// set to the task invocation itself, via e.g. the special MSBuildRuntime and MSBuildArchitecture parameters.  
        /// </param>
        /// <remarks>
        /// If a task factory implements ITaskFactory2, MSBuild will call this method instead of ITaskFactory.CreateTask.  
        /// </remarks>
        /// <returns>
        /// The generated task, or <c>null</c> if the task failed to be created.
        /// </returns>
        ITask CreateTask(IBuildEngine taskFactoryLoggingHost, IDictionary<string, string> taskIdentityParameters);
    }
}