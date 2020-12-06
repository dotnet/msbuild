// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Reflection;
using Microsoft.Build.Utilities;
using Microsoft.Build.Construction;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the assembly task factory
    /// </summary>
    public class AssemblyTaskFactory_Tests
    {
        /// <summary>
        ///  A well instantiated task factory
        /// </summary>
        private AssemblyTaskFactory _taskFactory;

        /// <summary>
        /// The load info about a task to wrap in the assembly task factory
        /// </summary>
        private AssemblyLoadInfo _loadInfo;

        /// <summary>
        /// The loaded type from the initialized task factory.
        /// </summary>
        private LoadedType _loadedType;

        /// <summary>
        /// Initialize a task factory
        /// </summary>
        public AssemblyTaskFactory_Tests()
        {
            SetupTaskFactory(null, false);
        }

        #region AssemblyTaskFactory
        #region ExpectExceptions
        /// <summary>
        /// Make sure we get an invalid project file exception when a null load info is passed to the factory
        /// </summary>
        [Fact]
        public void NullLoadInfo()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
                taskFactory.InitializeFactory(null, "TaskToTestFactories", new Dictionary<string, TaskPropertyInfo>(), string.Empty, null, false, null, ElementLocation.Create("NONE"), String.Empty);
            }
           );
        }
        /// <summary>
        /// Make sure we get an invalid project file exception when a null task name is passed to the factory
        /// </summary>
        [Fact]
        public void NullTaskName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
                taskFactory.InitializeFactory(_loadInfo, null, new Dictionary<string, TaskPropertyInfo>(), string.Empty, null, false, null, ElementLocation.Create("NONE"), String.Empty);
            }
           );
        }
        /// <summary>
        /// Make sure we get an invalid project file exception when an empty task name is passed to the factory
        /// </summary>
        [Fact]
        public void EmptyTaskName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
                taskFactory.InitializeFactory(_loadInfo, String.Empty, new Dictionary<string, TaskPropertyInfo>(), string.Empty, null, false, null, ElementLocation.Create("NONE"), String.Empty);
            }
           );
        }
        /// <summary>
        /// Make sure we get an invalid project file exception when the task is not in the info
        /// </summary>
        [Fact]
        public void GoodTaskNameButNotInInfo()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
                taskFactory.InitializeFactory(_loadInfo, "RandomTask", new Dictionary<string, TaskPropertyInfo>(), string.Empty, null, false, null, ElementLocation.Create("NONE"), String.Empty);
            }
           );
        }
        /// <summary>
        /// Make sure we get an internal error when we call the initialize factory on the public method.
        /// This is done because we cannot properly initialize the task factory using the public interface and keep 
        /// backwards compatibility with orcas and whidbey.
        /// </summary>
        [Fact]
        public void CallPublicInitializeFactory()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
                taskFactory.Initialize(String.Empty, new Dictionary<string, TaskPropertyInfo>(), String.Empty, null);
            }
           );
        }
        /// <summary>
        /// Make sure we get an internal error when we call the ITaskFactory2 version of initialize factory.
        /// This is done because we cannot properly initialize the task factory using the public interface and keep 
        /// backwards compatibility with orcas and whidbey.
        /// </summary>
        [Fact]
        public void CallPublicInitializeFactory2()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
                taskFactory.Initialize(String.Empty, null, new Dictionary<string, TaskPropertyInfo>(), String.Empty, null);
            }
           );
        }
        #endregion

        /// <summary>
        /// Verify that we can ask the factory if a given task is in the factory and get the correct result back
        /// </summary>
        [Fact]
        public void CreatableByTaskFactoryGoodName()
        {
            Assert.True(_taskFactory.TaskNameCreatableByFactory("TaskToTestFactories", null, String.Empty, null, ElementLocation.Create(".", 1, 1)));
        }

        /// <summary>
        /// Expect a false answer when we ask for a task which is not in the factory.
        /// </summary>
        [Fact]
        public void CreatableByTaskFactoryNotInAssembly()
        {
            Assert.False(_taskFactory.TaskNameCreatableByFactory("NotInAssembly", null, String.Empty, null, ElementLocation.Create(".", 1, 1)));
        }

        /// <summary>
        /// Expect a false answer when we ask for a task which is not in the factory.
        /// </summary>
        [Fact]
        public void CreatableByTaskFactoryNotInAssemblyEmptyTaskName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                Assert.False(_taskFactory.TaskNameCreatableByFactory(String.Empty, null, String.Empty, null, ElementLocation.Create(".", 1, 1)));
            }
           );
        }
        /// <summary>
        /// Expect a false answer when we ask for a task which is not in the factory.
        /// </summary>
        [Fact]
        public void CreatableByTaskFactoryNullTaskName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                Assert.False(_taskFactory.TaskNameCreatableByFactory(null, null, String.Empty, null, ElementLocation.Create(".", 1, 1)));
            }
           );
        }
        /// <summary>
        /// Make sure that when an explicitly matching identity is specified (e.g. the identity is non-empty), 
        /// it still counts as correct.  
        /// </summary>
        [Fact]
        public void CreatableByTaskFactoryMatchingIdentity()
        {
            IDictionary<string, string> factoryIdentityParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            factoryIdentityParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.currentRuntime);
            factoryIdentityParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture);

            SetupTaskFactory(factoryIdentityParameters, false /* don't want task host */);

            IDictionary<string, string> taskIdentityParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            taskIdentityParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr4);
            taskIdentityParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

            Assert.True(_taskFactory.TaskNameCreatableByFactory("TaskToTestFactories", taskIdentityParameters, String.Empty, null, ElementLocation.Create(".", 1, 1)));
        }

        /// <summary>
        /// Verify that if the task identity parameters don't match the factory identity, TaskNameCreatableByFactory 
        /// returns false.
        /// </summary>
        [Fact]
        public void CreatableByTaskFactoryMismatchedIdentity()
        {
            IDictionary<string, string> factoryIdentityParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            factoryIdentityParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr2);
            factoryIdentityParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture);

            SetupTaskFactory(factoryIdentityParameters, false /* don't want task host */);

            IDictionary<string, string> taskIdentityParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            taskIdentityParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr4);
            taskIdentityParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture);

            Assert.False(_taskFactory.TaskNameCreatableByFactory("TaskToTestFactories", taskIdentityParameters, String.Empty, null, ElementLocation.Create(".", 1, 1)));
        }

        /// <summary>
        /// Make sure the number of properties retrieved from the task factory are the same number retrieved from the type directly.
        /// </summary>
        [Fact]
        public void VerifyGetTaskParameters()
        {
            TaskPropertyInfo[] propertyInfos = _taskFactory.GetTaskParameters();
            LoadedType comparisonType = new LoadedType(typeof(TaskToTestFactories), _loadInfo);
            PropertyInfo[] comparisonInfo = comparisonType.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            Assert.Equal(comparisonInfo.Length, propertyInfos.Length);

            bool foundExpectedParameter = false;
            bool foundNotExpectedParameter = false;

            for (int i = 0; i < propertyInfos.Length; i++)
            {
                if (propertyInfos[i].Name.Equals("ExpectedParameter", StringComparison.OrdinalIgnoreCase))
                {
                    foundExpectedParameter = true;
                }

                if (propertyInfos[i].Name.Equals("NotExpectedParameter", StringComparison.OrdinalIgnoreCase))
                {
                    foundNotExpectedParameter = true;
                }
            }

            Assert.True(foundExpectedParameter);
            Assert.False(foundNotExpectedParameter);
        }

        /// <summary>
        /// Verify a good task can be created.
        /// </summary>
        [Fact]
        public void VerifyGoodTaskInstantiation()
        {
            ITask createdTask = null;
            try
            {
                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.False(createdTask is TaskHostTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that does not use the task host can be created when passed "don't care" 
        /// for the task invocation task host parameters.
        /// </summary>
        [Fact]
        public void VerifyMatchingTaskParametersDontLaunchTaskHost1()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.False(createdTask is TaskHostTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that does not use the task host can be created when passed task host 
        /// parameters that explicitly match the current process. 
        /// </summary>
        [Fact]
        public void VerifyMatchingTaskParametersDontLaunchTaskHost2()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr4);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.GetCurrentMSBuildArchitecture());

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.False(createdTask is TaskHostTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that does not use the task host can be created when passed "don't care" 
        /// for the task invocation task host parameters.
        /// </summary>
        [Fact]
        public void VerifyMatchingUsingTaskParametersDontLaunchTaskHost1()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                SetupTaskFactory(taskParameters, false /* don't want task host */);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.False(createdTask is TaskHostTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that does not use the task host can be created when passed task host 
        /// parameters that explicitly match the current process. 
        /// </summary>
        [Fact]
        public void VerifyMatchingUsingTaskParametersDontLaunchTaskHost2()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.GetCurrentMSBuildArchitecture());

                SetupTaskFactory(taskParameters, false /* don't want task host */);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.False(createdTask is TaskHostTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that uses the task host can be created when passed task host 
        /// parameters that explicitly do not match the current process. 
        /// </summary>
        [Fact]
        public void VerifyMatchingParametersDontLaunchTaskHost()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> factoryParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                factoryParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr4);

                SetupTaskFactory(factoryParameters, false /* don't want task host */);

                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.False(createdTask is TaskHostTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that uses the task host can be created when passed task host 
        /// parameters that explicitly do not match the current process. 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void VerifyNonmatchingUsingTaskParametersLaunchTaskHost()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr2);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                SetupTaskFactory(taskParameters, false /* don't want task host */);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.IsType<TaskHostTask>(createdTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that uses the task host can be created when passed task host 
        /// parameters that explicitly do not match the current process. 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void VerifyNonmatchingTaskParametersLaunchTaskHost()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr2);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.IsType<TaskHostTask>(createdTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that uses the task host can be created when passed task host 
        /// parameters that explicitly do not match the current process. 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void VerifyNonmatchingParametersLaunchTaskHost()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> factoryParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                factoryParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr2);

                SetupTaskFactory(factoryParameters, false /* don't want task host */);

                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.IsType<TaskHostTask>(createdTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that uses the task host can be created when the task factory is 
        /// explicitly instructed to launch the task host. 
        /// </summary>
        [Fact]
        public void VerifyExplicitlyLaunchTaskHost()
        {
            ITask createdTask = null;
            try
            {
                SetupTaskFactory(null, true /* want task host */);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.IsType<TaskHostTask>(createdTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that uses the task host can be created when the task factory is 
        /// explicitly instructed to launch the task host. 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void VerifyExplicitlyLaunchTaskHostEvenIfParametersMatch1()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                SetupTaskFactory(taskParameters, true /* want task host */);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.IsType<TaskHostTask>(createdTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that uses the task host can be created when the task factory is 
        /// explicitly instructed to launch the task host. 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void VerifyExplicitlyLaunchTaskHostEvenIfParametersMatch2()
        {
            ITask createdTask = null;
            try
            {
                SetupTaskFactory(null, true /* want task host */);

                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.IsType<TaskHostTask>(createdTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Verify a good task that uses the task host can be created when the task factory is 
        /// explicitly instructed to launch the task host. 
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void VerifySameFactoryCanGenerateDifferentTaskInstances()
        {
            ITask createdTask = null;
            IDictionary<string, string> factoryParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            factoryParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
            factoryParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

            SetupTaskFactory(factoryParameters, explicitlyLaunchTaskHost: false);

            try
            {
                // #1: don't launch task host
                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.IsNotType<TaskHostTask>(createdTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }

            try
            {
                // #2: launch task host
                var taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr2);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters,
#if FEATURE_APPDOMAIN
                    new AppDomainSetup(),
#endif
                    false);
                Assert.NotNull(createdTask);
                Assert.IsType<TaskHostTask>(createdTask);
            }
            finally
            {
                if (createdTask != null)
                {
                    _taskFactory.CleanupTask(createdTask);
                }
            }
        }

        /// <summary>
        /// Abstract out the creation of the new AssemblyTaskFactory with default task, and 
        /// with some basic validation.
        /// </summary>
        private void SetupTaskFactory(IDictionary<string, string> factoryParameters, bool explicitlyLaunchTaskHost)
        {
            _taskFactory = new AssemblyTaskFactory();
#if FEATURE_ASSEMBLY_LOCATION
            _loadInfo = AssemblyLoadInfo.Create(null, Assembly.GetAssembly(typeof(TaskToTestFactories)).Location);
#else
            _loadInfo = AssemblyLoadInfo.Create(typeof(TaskToTestFactories).GetTypeInfo().Assembly.FullName, null);
#endif
            _loadedType = _taskFactory.InitializeFactory(_loadInfo, "TaskToTestFactories", new Dictionary<string, TaskPropertyInfo>(), string.Empty, factoryParameters, explicitlyLaunchTaskHost, null, ElementLocation.Create("NONE"), String.Empty);
            Assert.True(_loadedType.Assembly.Equals(_loadInfo)); // "Expected the AssemblyLoadInfo to be equal"
        }

        #endregion

        #region InternalClasses
        /// <summary>
        ///  Create a task which can be used to test the factories
        /// </summary>
        public class TaskToTestFactories
#if FEATURE_APPDOMAIN
            : AppDomainIsolatedTask
#else
            : Task
#endif
        {
            /// <summary>
            /// Give a parameter which can be considered expected
            /// </summary>
            public string ExpectedParameter
            {
                get;
                set;
            }

            /// <summary>
            /// Expect not to find this parameter as it is internal
            /// </summary>
            internal string NotExpected
            {
                get;
                set;
            }

            /// <summary>
            /// Execute the test
            /// </summary>
            public override bool Execute()
            {
                return true;
            }
        }
        #endregion
    }
}
