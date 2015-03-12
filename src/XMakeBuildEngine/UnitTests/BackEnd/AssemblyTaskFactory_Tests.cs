// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for the AssemblyTaskFactory</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using System.Reflection;
using Microsoft.Build.Utilities;
using Microsoft.Build.Construction;

using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
using LoggingService = Microsoft.Build.BackEnd.Logging.LoggingService;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the assembly task factory
    /// </summary>
    [TestClass]
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
        [TestInitialize]
        public void Setup()
        {
            SetupTaskFactory(null, false);
        }

        /// <summary>
        /// Tear down what was created in setup
        /// </summary>
        [TestCleanup]
        public void TearDownAttribute()
        {
            _taskFactory = null;
            _loadInfo = null;
            _loadedType = null;
        }

        #region AssemblyTaskFactory
        #region ExpectExceptions
        /// <summary>
        /// Make sure we get an invalid project file exception when a null load info is passed to the factory
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullLoadInfo()
        {
            AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
            taskFactory.InitializeFactory(null, "TaskToTestFactories", new Dictionary<string, TaskPropertyInfo>(), string.Empty, null, false, null, ElementLocation.Create("NONE"), String.Empty);
        }

        /// <summary>
        /// Make sure we get an invalid project file exception when a null task name is passed to the factory
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void NullTaskName()
        {
            AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
            taskFactory.InitializeFactory(_loadInfo, null, new Dictionary<string, TaskPropertyInfo>(), string.Empty, null, false, null, ElementLocation.Create("NONE"), String.Empty);
        }

        /// <summary>
        /// Make sure we get an invalid project file exception when an empty task name is passed to the factory
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void EmptyTaskName()
        {
            AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
            taskFactory.InitializeFactory(_loadInfo, String.Empty, new Dictionary<string, TaskPropertyInfo>(), string.Empty, null, false, null, ElementLocation.Create("NONE"), String.Empty);
        }

        /// <summary>
        /// Make sure we get an invalid project file exception when the task is not in the info
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void GoodTaskNameButNotInInfo()
        {
            AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
            taskFactory.InitializeFactory(_loadInfo, "RandomTask", new Dictionary<string, TaskPropertyInfo>(), string.Empty, null, false, null, ElementLocation.Create("NONE"), String.Empty);
        }

        /// <summary>
        /// Make sure we get an internal error when we call the initialize factory on the public method.
        /// This is done because we cannot properly initialize the task factory using the public interface and keep 
        /// backwards compatibility with orcas and whidbey.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void CallPublicInitializeFactory()
        {
            AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
            taskFactory.Initialize(String.Empty, new Dictionary<string, TaskPropertyInfo>(), String.Empty, null);
        }

        /// <summary>
        /// Make sure we get an internal error when we call the ITaskFactory2 version of initialize factory.
        /// This is done because we cannot properly initialize the task factory using the public interface and keep 
        /// backwards compatibility with orcas and whidbey.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void CallPublicInitializeFactory2()
        {
            AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
            taskFactory.Initialize(String.Empty, null, new Dictionary<string, TaskPropertyInfo>(), String.Empty, null);
        }

        #endregion

        /// <summary>
        /// Verify that we can ask the factory if a given task is in the factory and get the correct result back
        /// </summary>
        [TestMethod]
        public void CreatableByTaskFactoryGoodName()
        {
            Assert.IsTrue(_taskFactory.TaskNameCreatableByFactory("TaskToTestFactories", null, String.Empty, null, ElementLocation.Create(".", 1, 1)));
        }

        /// <summary>
        /// Expect a false answer when we ask for a task which is not in the factory.
        /// </summary>
        [TestMethod]
        public void CreatableByTaskFactoryNotInAssembly()
        {
            Assert.IsFalse(_taskFactory.TaskNameCreatableByFactory("NotInAssembly", null, String.Empty, null, ElementLocation.Create(".", 1, 1)));
        }

        /// <summary>
        /// Expect a false answer when we ask for a task which is not in the factory.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CreatableByTaskFactoryNotInAssemblyEmptyTaskName()
        {
            Assert.IsFalse(_taskFactory.TaskNameCreatableByFactory(String.Empty, null, String.Empty, null, ElementLocation.Create(".", 1, 1)));
        }

        /// <summary>
        /// Expect a false answer when we ask for a task which is not in the factory.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CreatableByTaskFactoryNullTaskName()
        {
            Assert.IsFalse(_taskFactory.TaskNameCreatableByFactory(null, null, String.Empty, null, ElementLocation.Create(".", 1, 1)));
        }

        /// <summary>
        /// Make sure that when an explicitly matching identity is specified (e.g. the identity is non-empty), 
        /// it still counts as correct.  
        /// </summary>
        [TestMethod]
        public void CreatableByTaskFactoryMatchingIdentity()
        {
            IDictionary<string, string> factoryIdentityParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            factoryIdentityParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.currentRuntime);
            factoryIdentityParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture);

            SetupTaskFactory(factoryIdentityParameters, false /* don't want task host */);

            IDictionary<string, string> taskIdentityParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            taskIdentityParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr4);
            taskIdentityParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

            Assert.IsTrue(_taskFactory.TaskNameCreatableByFactory("TaskToTestFactories", taskIdentityParameters, String.Empty, null, ElementLocation.Create(".", 1, 1)));
        }

        /// <summary>
        /// Verify that if the task identity parameters don't match the factory identity, TaskNameCreatableByFactory 
        /// returns false.
        /// </summary>
        [TestMethod]
        public void CreatableByTaskFactoryMismatchedIdentity()
        {
            IDictionary<string, string> factoryIdentityParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            factoryIdentityParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr2);
            factoryIdentityParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture);

            SetupTaskFactory(factoryIdentityParameters, false /* don't want task host */);

            IDictionary<string, string> taskIdentityParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            taskIdentityParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr4);
            taskIdentityParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture);

            Assert.IsFalse(_taskFactory.TaskNameCreatableByFactory("TaskToTestFactories", taskIdentityParameters, String.Empty, null, ElementLocation.Create(".", 1, 1)));
        }

        /// <summary>
        /// Make sure the number of properties retreived from the task factory are the same number retreived from the type directly.
        /// </summary>
        [TestMethod]
        public void VerifyGetTaskParameters()
        {
            TaskPropertyInfo[] propertyInfos = _taskFactory.GetTaskParameters();
            LoadedType comparisonType = new LoadedType(typeof(TaskToTestFactories), _loadInfo);
            PropertyInfo[] comparisonInfo = comparisonType.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            Assert.AreEqual(comparisonInfo.Length, propertyInfos.Length);

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

            Assert.IsTrue(foundExpectedParameter);
            Assert.IsFalse(foundNotExpectedParameter);
        }

        /// <summary>
        /// Verify a good task can be created.
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void VerifyGoodTaskInstantiation()
        {
            ITask createdTask = null;
            try
            {
                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsNotInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void VerifyMatchingTaskParametersDontLaunchTaskHost1()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsNotInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void VerifyMatchingTaskParametersDontLaunchTaskHost2()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr4);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.GetCurrentMSBuildArchitecture());

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsNotInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void VerifyMatchingUsingTaskParametersDontLaunchTaskHost1()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                SetupTaskFactory(taskParameters, false /* don't want task host */);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsNotInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void VerifyMatchingUsingTaskParametersDontLaunchTaskHost2()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.GetCurrentMSBuildArchitecture());

                SetupTaskFactory(taskParameters, false /* don't want task host */);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsNotInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
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

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsNotInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        public void VerifyNonmatchingUsingTaskParametersLaunchTaskHost()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr2);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                SetupTaskFactory(taskParameters, false /* don't want task host */);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        public void VerifyNonmatchingTaskParametersLaunchTaskHost()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.clr2);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
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

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        public void VerifyExplicitlyLaunchTaskHost()
        {
            ITask createdTask = null;
            try
            {
                SetupTaskFactory(null, true /* want task host */);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        public void VerifyExplicitlyLaunchTaskHostEvenIfParametersMatch1()
        {
            ITask createdTask = null;
            try
            {
                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                SetupTaskFactory(taskParameters, true /* want task host */);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        public void VerifyExplicitlyLaunchTaskHostEvenIfParametersMatch2()
        {
            ITask createdTask = null;
            try
            {
                SetupTaskFactory(null, true /* want task host */);

                IDictionary<string, string> taskParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                taskParameters.Add(XMakeAttributes.runtime, XMakeAttributes.MSBuildRuntimeValues.any);
                taskParameters.Add(XMakeAttributes.architecture, XMakeAttributes.MSBuildArchitectureValues.any);

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsInstanceOfType(createdTask, typeof(TaskHostTask));
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
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
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
                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), null, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsNotInstanceOfType(createdTask, typeof(TaskHostTask));
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

                createdTask = _taskFactory.CreateTaskInstance(ElementLocation.Create("MSBUILD"), null, new MockHost(), taskParameters, new AppDomainSetup(), false);
                Assert.IsNotNull(createdTask);
                Assert.IsInstanceOfType(createdTask, typeof(TaskHostTask));
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
            _loadInfo = AssemblyLoadInfo.Create(null, Assembly.GetAssembly(typeof(TaskToTestFactories)).Location);
            _loadedType = _taskFactory.InitializeFactory(_loadInfo, "TaskToTestFactories", new Dictionary<string, TaskPropertyInfo>(), string.Empty, factoryParameters, explicitlyLaunchTaskHost, null, ElementLocation.Create("NONE"), String.Empty);
            Assert.IsTrue(_loadedType.Assembly.Equals(_loadInfo), "Expected the AssemblyLoadInfo to be equal");
        }

        #endregion

        #region InternalClasses
        /// <summary>
        ///  Create a task which can be used to test the factories
        /// </summary>
        public class TaskToTestFactories : AppDomainIsolatedTask
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
