// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using System.Reflection;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A task used for testing the TaskExecutionHost, which reports what the TaskExecutionHost does to it.
    /// </summary>
    internal class TaskBuilderTestTask : IGeneratedTask
    {
        /// <summary>
        /// The task host.
        /// </summary>
        private ITestTaskHost _testTaskHost;

        /// <summary>
        /// The value to return from Execute
        /// </summary>
        private bool _executeReturnValue;

        /// <summary>
        /// The value for the BoolOutput
        /// </summary>
        private bool _boolOutput;

        /// <summary>
        /// The value for the BoolArrayOutput
        /// </summary>
        private bool[] _boolArrayOutput;

        /// <summary>
        /// The value for the IntOutput
        /// </summary>
        private int _intOutput;

        /// <summary>
        /// The value for the IntArrayOutput
        /// </summary>
        private int[] _intArrayOutput;

        /// <summary>
        /// The value for the StringOutput
        /// </summary>
        private string _stringOutput;

        /// <summary>
        /// The value for the StringArrayOutput
        /// </summary>
        private string[] _stringArrayOutput;

        /// <summary>
        /// The value for the ItemOutput
        /// </summary>
        private ITaskItem _itemOutput;

        /// <summary>
        /// The value for the ItemArrayOutput
        /// </summary>
        private ITaskItem[] _itemArrayOutput;

        /// <summary>
        /// Property determining if Execute() should throw or not.
        /// </summary>
        public bool ThrowOnExecute
        {
            internal get;
            set;
        }

        /// <summary>
        /// A boolean parameter
        /// </summary>
        public bool BoolParam
        {
            set
            {
                _boolOutput = value;
                _testTaskHost.ParameterSet("BoolParam", value);
            }
        }

        /// <summary>
        /// A boolean array parameter
        /// </summary>
        public bool[] BoolArrayParam
        {
            set
            {
                _boolArrayOutput = value;
                _testTaskHost.ParameterSet("BoolArrayParam", value);
            }
        }

        /// <summary>
        /// An integer parameter
        /// </summary>
        public int IntParam
        {
            set
            {
                _intOutput = value;
                _testTaskHost.ParameterSet("IntParam", value);
            }
        }

        /// <summary>
        /// An integer array parameter.
        /// </summary>
        public int[] IntArrayParam
        {
            set
            {
                _intArrayOutput = value;
                _testTaskHost.ParameterSet("IntArrayParam", value);
            }
        }

        /// <summary>
        /// A string parameter.
        /// </summary>
        public string StringParam
        {
            set
            {
                _stringOutput = value;
                _testTaskHost.ParameterSet("StringParam", value);
            }
        }

        /// <summary>
        /// A string array parameter.
        /// </summary>
        public string[] StringArrayParam
        {
            set
            {
                _stringArrayOutput = value;
                _testTaskHost.ParameterSet("StringArrayParam", value);
            }
        }

        /// <summary>
        /// An item parameter.
        /// </summary>
        public ITaskItem ItemParam
        {
            set
            {
                _itemOutput = value;
                _testTaskHost.ParameterSet("ItemParam", value);
            }
        }

        /// <summary>
        /// An item array parameter.
        /// </summary>
        public ITaskItem[] ItemArrayParam
        {
            set
            {
                _itemArrayOutput = value;
                _testTaskHost.ParameterSet("ItemArrayParam", value);
            }
        }

        /// <summary>
        /// The Execute return value parameter.
        /// </summary>
        [Required]
        public bool ExecuteReturnParam
        {
            set
            {
                _executeReturnValue = value;
                _testTaskHost.ParameterSet("ExecuteReturnParam", value);
            }
        }

        /// <summary>
        /// A boolean output.
        /// </summary>
        [Output]
        public bool BoolOutput
        {
            get
            {
                _testTaskHost.OutputRead("BoolOutput", _boolOutput);
                return _boolOutput;
            }
        }

        /// <summary>
        /// A boolean array output
        /// </summary>
        [Output]
        public bool[] BoolArrayOutput
        {
            get
            {
                _testTaskHost.OutputRead("BoolArrayOutput", _boolArrayOutput);
                return _boolArrayOutput;
            }
        }

        /// <summary>
        /// An integer output
        /// </summary>
        [Output]
        public int IntOutput
        {
            get
            {
                _testTaskHost.OutputRead("IntOutput", _intOutput);
                return _intOutput;
            }
        }

        /// <summary>
        /// An integer array output
        /// </summary>
        [Output]
        public int[] IntArrayOutput
        {
            get
            {
                _testTaskHost.OutputRead("IntArrayOutput", _intArrayOutput);
                return _intArrayOutput;
            }
        }

        /// <summary>
        /// A string output
        /// </summary>
        [Output]
        public string StringOutput
        {
            get
            {
                _testTaskHost.OutputRead("StringOutput", _stringOutput);
                return _stringOutput;
            }
        }

        /// <summary>
        /// An empty string output
        /// </summary>
        [Output]
        public string EmptyStringOutput
        {
            get
            {
                _testTaskHost.OutputRead("EmptyStringOutput", null);
                return String.Empty;
            }
        }

        /// <summary>
        /// An empty string array output
        /// </summary>
        [Output]
        public string[] EmptyStringArrayOutput
        {
            get
            {
                _testTaskHost.OutputRead("EmptyStringArrayOutput", null);
                return new string[0];
            }
        }

        /// <summary>
        /// A null string output
        /// </summary>
        [Output]
        public string NullStringOutput
        {
            get
            {
                _testTaskHost.OutputRead("NullStringOutput", null);
                return null;
            }
        }

        /// <summary>
        /// A null ITaskItem output
        /// </summary>
        [Output]
        public ITaskItem NullITaskItemOutput
        {
            get
            {
                _testTaskHost.OutputRead("NullITaskItemOutput", null);
                return null;
            }
        }

        /// <summary>
        /// A null string array output
        /// </summary>
        [Output]
        public string[] NullStringArrayOutput
        {
            get
            {
                _testTaskHost.OutputRead("NullStringArrayOutput", null);
                return null;
            }
        }

        /// <summary>
        /// A null ITaskItem array output
        /// </summary>
        [Output]
        public ITaskItem[] NullITaskItemArrayOutput
        {
            get
            {
                _testTaskHost.OutputRead("NullITaskItemArrayOutput", null);
                return null;
            }
        }

        /// <summary>
        /// A string array output
        /// </summary>
        [Output]
        public string[] StringArrayOutput
        {
            get
            {
                _testTaskHost.OutputRead("StringArrayOutput", _stringArrayOutput);
                return _stringArrayOutput;
            }
        }

        /// <summary>
        /// A task item output
        /// </summary>
        [Output]
        public ITaskItem ItemOutput
        {
            get
            {
                _testTaskHost.OutputRead("ItemOutput", _itemOutput);
                return _itemOutput;
            }
        }

        /// <summary>
        /// A task item array output
        /// </summary>
        [Output]
        public ITaskItem[] ItemArrayOutput
        {
            get
            {
                _testTaskHost.OutputRead("ItemArrayOutput", _itemArrayOutput);
                return _itemArrayOutput;
            }
        }

        /// <summary>
        /// A task item array output that is null
        /// </summary>
        [Output]
        public ITaskItem[] ItemArrayNullOutput
        {
            get
            {
                _testTaskHost.OutputRead("ItemArrayNullOutput", _itemArrayOutput);
                return null;
            }
        }

        /// <summary>
        /// An object output
        /// </summary>
        [Output]
        public object ObjectOutput
        {
            get
            {
                object output = new object();
                _testTaskHost.OutputRead("ObjectOutput", output);
                return output;
            }
        }

        /// <summary>
        /// An object array output
        /// </summary>
        [Output]
        public object[] ObjectArrayOutput
        {
            get
            {
                object[] output = new object[] { new object(), new object() };
                _testTaskHost.OutputRead("ObjectArrayOutput", output);
                return output;
            }
        }

        /// <summary>
        /// An arraylist output
        /// </summary>
        [Output]
        public ArrayList ArrayListOutput
        {
            get
            {
                ArrayList output = new ArrayList();
                _testTaskHost.OutputRead("ArrayListOutput", output);
                return output;
            }
        }

        #region ITask Members

        /// <summary>
        /// The build engine property
        /// </summary>
        public IBuildEngine BuildEngine
        {
            get;
            set;
        }

        /// <summary>
        /// The host object property
        /// </summary>
        public ITaskHost HostObject
        {
            get
            {
                return _testTaskHost;
            }

            set
            {
                _testTaskHost = value as ITestTaskHost;
            }
        }

        /// <summary>
        /// The Execute() method for ITask.
        /// </summary>
        public bool Execute()
        {
            if (ThrowOnExecute)
            {
                throw new IndexOutOfRangeException();
            }

            return _executeReturnValue;
        }

        #endregion

        #region IGeneratedTask Members

        /// <summary>
        /// Gets the property value.
        /// </summary>
        /// <param name="property">The property to get.</param>
        /// <returns>
        /// The value of the property, the value's type will match the type given by <see cref="TaskPropertyInfo.PropertyType"/>.
        /// </returns>
        /// <remarks>
        /// MSBuild calls this method after executing the task to get output parameters.
        /// All exceptions from this method will be caught in the taskExecution host and logged as a fatal task error
        /// </remarks>
        public object GetPropertyValue(TaskPropertyInfo property)
        {
            return GetType().GetProperty(property.Name).GetValue(this, null);
        }

        /// <summary>
        /// Sets a value on a property of this task instance.
        /// </summary>
        /// <param name="property">The property to set.</param>
        /// <param name="value">The value to set. The caller is responsible to type-coerce this value to match the property's <see cref="TaskPropertyInfo.PropertyType"/>.</param>
        /// <remarks>
        /// All exceptions from this method will be caught in the taskExecution host and logged as a fatal task error
        /// </remarks>
        public void SetPropertyValue(TaskPropertyInfo property, object value)
        {
            GetType().GetProperty(property.Name).SetValue(this, value, null);
        }

        #endregion

        /// <summary>
        /// Task factory which wraps a test task, this is used for unit testing
        /// </summary>
        internal class TaskBuilderTestTaskFactory : ITaskFactory
        {
            /// <summary>
            /// Type of the task wrapped by the task factory
            /// </summary>
            private Type _type = typeof(TaskBuilderTestTask);

            /// <summary>
            /// Should the task throw on execution
            /// </summary>
            public bool ThrowOnExecute
            {
                get;
                set;
            }

            /// <summary>
            /// Name of the factory
            /// </summary>
            public string FactoryName
            {
                get { return typeof(TaskBuilderTestTask).ToString(); }
            }

            /// <summary>
            /// Gets the type of task generated.
            /// </summary>
            public Type TaskType
            {
                get { return _type; }
            }

            /// <summary>
            /// There is nothing to initialize
            /// </summary>
            public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> taskParameters, string taskElementContents, IBuildEngine taskLoggingHost)
            {
                return true;
            }

            /// <summary>
            /// Get a list of parameters for the task.
            /// </summary>
            public TaskPropertyInfo[] GetTaskParameters()
            {
                PropertyInfo[] infos = _type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                var propertyInfos = new TaskPropertyInfo[infos.Length];
                for (int i = 0; i < infos.Length; i++)
                {
                    propertyInfos[i] = new TaskPropertyInfo(
                        infos[i].Name,
                        infos[i].PropertyType,
                        infos[i].GetCustomAttributes(typeof(OutputAttribute), false).Count() > 0,
                        infos[i].GetCustomAttributes(typeof(RequiredAttribute), false).Count() > 0);
                }

                return propertyInfos;
            }

            /// <summary>
            /// Create a new instance
            /// </summary>
            public ITask CreateTask(IBuildEngine loggingHost)
            {
                var task = new TaskBuilderTestTask();
                task.ThrowOnExecute = ThrowOnExecute;
                return task;
            }

            /// <summary>
            ///  Cleans up a task that is finished.
            /// </summary>
            public void CleanupTask(ITask task)
            {
            }
        }
    }
}
