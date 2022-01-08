// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

using Microsoft.Build.Framework;

using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    internal class MockTaskBase
    {
        private bool _myBoolParam;
        private bool[] _myBoolArrayParam;
        private int _myIntParam;
        private int[] _myIntArrayParam;
        private string _myStringParam;
        private string[] _myStringArrayParam;
        private ITaskItem _myITaskItemParam;
        private ITaskItem[] _myITaskItemArrayParam;

        private bool _myRequiredBoolParam;
        private bool[] _myRequiredBoolArrayParam;
        private int _myRequiredIntParam;
        private int[] _myRequiredIntArrayParam;
        private string _myRequiredStringParam;
        private string[] _myRequiredStringArrayParam;
        private ITaskItem _myRequiredITaskItemParam;
        private ITaskItem[] _myRequiredITaskItemArrayParam;

        internal bool myBoolParamWasSet;
        internal bool myBoolArrayParamWasSet;
        internal bool myIntParamWasSet;
        internal bool myIntArrayParamWasSet;
        internal bool myStringParamWasSet;
        internal bool myStringArrayParamWasSet;
        internal bool myITaskItemParamWasSet;
        internal bool myITaskItemArrayParamWasSet;

        // disable csharp compiler warning #0414: field assigned unused value
#pragma warning disable 0414
        internal bool myRequiredBoolParamWasSet;
        internal bool myRequiredBoolArrayParamWasSet;
        internal bool myRequiredIntParamWasSet;
        internal bool myRequiredIntArrayParamWasSet;
        internal bool myRequiredStringParamWasSet;
        internal bool myRequiredStringArrayParamWasSet;
        internal bool myRequiredITaskItemParamWasSet;
        internal bool myRequiredITaskItemArrayParamWasSet;
#pragma warning restore 0414

        /// <summary>
        /// Single bool parameter.
        /// </summary>
        public bool MyBoolParam
        {
            get { return _myBoolParam; }
            set { _myBoolParam = value; this.myBoolParamWasSet = true; }
        }

        /// <summary>
        /// bool[] parameter.
        /// </summary>
        public bool[] MyBoolArrayParam
        {
            get { return _myBoolArrayParam; }
            set { _myBoolArrayParam = value; this.myBoolArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single int parameter.
        /// </summary>
        public int MyIntParam
        {
            get { return _myIntParam; }
            set { _myIntParam = value; this.myIntParamWasSet = true; }
        }

        /// <summary>
        /// int[] parameter.
        /// </summary>
        public int[] MyIntArrayParam
        {
            get { return _myIntArrayParam; }
            set { _myIntArrayParam = value; this.myIntArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single string parameter
        /// </summary>
        public string MyStringParam
        {
            get { return _myStringParam; }
            set { _myStringParam = value; this.myStringParamWasSet = true; }
        }

        /// <summary>
        /// A string array parameter.
        /// </summary>
        public string[] MyStringArrayParam
        {
            get { return _myStringArrayParam; }
            set { _myStringArrayParam = value; this.myStringArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single ITaskItem parameter.
        /// </summary>
        public ITaskItem MyITaskItemParam
        {
            get { return _myITaskItemParam; }
            set { _myITaskItemParam = value; this.myITaskItemParamWasSet = true; }
        }

        /// <summary>
        /// ITaskItem[] parameter.
        /// </summary>
        public ITaskItem[] MyITaskItemArrayParam
        {
            get { return _myITaskItemArrayParam; }
            set { _myITaskItemArrayParam = value; this.myITaskItemArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single bool parameter.
        /// </summary>
        [Required]
        public bool MyRequiredBoolParam
        {
            get { return _myRequiredBoolParam; }
            set { _myRequiredBoolParam = value; this.myRequiredBoolParamWasSet = true; }
        }

        /// <summary>
        /// bool[] parameter.
        /// </summary>
        [Required]
        public bool[] MyRequiredBoolArrayParam
        {
            get { return _myRequiredBoolArrayParam; }
            set { _myRequiredBoolArrayParam = value; this.myRequiredBoolArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single int parameter.
        /// </summary>
        [Required]
        public int MyRequiredIntParam
        {
            get { return _myRequiredIntParam; }
            set { _myRequiredIntParam = value; this.myRequiredIntParamWasSet = true; }
        }

        /// <summary>
        /// int[] parameter.
        /// </summary>
        [Required]
        public int[] MyRequiredIntArrayParam
        {
            get { return _myRequiredIntArrayParam; }
            set { _myRequiredIntArrayParam = value; this.myRequiredIntArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single string parameter
        /// </summary>
        [Required]
        public string MyRequiredStringParam
        {
            get { return _myRequiredStringParam; }
            set { _myRequiredStringParam = value; this.myRequiredStringParamWasSet = true; }
        }

        /// <summary>
        /// A string array parameter.
        /// </summary>
        [Required]
        public string[] MyRequiredStringArrayParam
        {
            get { return _myRequiredStringArrayParam; }
            set { _myRequiredStringArrayParam = value; this.myRequiredStringArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single ITaskItem parameter.
        /// </summary>
        [Required]
        public ITaskItem MyRequiredITaskItemParam
        {
            get { return _myRequiredITaskItemParam; }
            set { _myRequiredITaskItemParam = value; this.myRequiredITaskItemParamWasSet = true; }
        }

        /// <summary>
        /// ITaskItem[] parameter.
        /// </summary>
        [Required]
        public ITaskItem[] MyRequiredITaskItemArrayParam
        {
            get { return _myRequiredITaskItemArrayParam; }
            set { _myRequiredITaskItemArrayParam = value; this.myRequiredITaskItemArrayParamWasSet = true; }
        }

        /// <summary>
        /// ArrayList output parameter.  (This is not supported by MSBuild.)
        /// </summary>
        [Output]
        public ArrayList MyArrayListOutputParam
        {
            get { return null; }
        }

        /// <summary>
        /// Null ITaskItem[] output parameter.
        /// </summary>
        [Output]
        public ITaskItem[] NullITaskItemArrayOutputParameter
        {
            get
            {
                ITaskItem[] myNullITaskItemArrayOutputParameter = null;
                return myNullITaskItemArrayOutputParameter;
            }
        }

        /// <summary>
        /// Empty string output parameter.
        /// </summary>
        [Output]
        public string EmptyStringOutputParameter
        {
            get
            {
                return String.Empty;
            }
        }

        /// <summary>
        /// Empty string output parameter.
        /// </summary>
        [Output]
        public string[] EmptyStringInStringArrayOutputParameter
        {
            get
            {
                string[] myArray = new string[] { "" };
                return myArray;
            }
        }

        /// <summary>
        /// ITaskItem output parameter.
        /// </summary>
        [Output]
        public ITaskItem ITaskItemOutputParameter
        {
            get
            {
                ITaskItem myITaskItem = null;
                return myITaskItem;
            }
        }

        /// <summary>
        /// string output parameter.
        /// </summary>
        [Output]
        public string StringOutputParameter
        {
            get
            {
                return "foo";
            }
        }

        /// <summary>
        /// string array output parameter.
        /// </summary>
        [Output]
        public string[] StringArrayOutputParameter
        {
            get
            {
                return new string[] { "foo", "bar" };
            }
        }

        /// <summary>
        /// int output parameter.
        /// </summary>
        [Output]
        public int IntOutputParameter
        {
            get
            {
                return 1;
            }
        }

        /// <summary>
        /// int array output parameter.
        /// </summary>
        [Output]
        public int[] IntArrayOutputParameter
        {
            get
            {
                return new int[] { 1, 2 };
            }
        }

        /// <summary>
        /// object array output parameter.
        /// </summary>
        [Output]
        public object[] ObjectArrayOutputParameter
        {
            get
            {
                return new object[] { new Object() };
            }
        }

        /// <summary>
        /// itaskitem implementation output parameter
        /// </summary>
        [Output]
        public MyTaskItem MyTaskItemOutputParameter
        {
            get
            {
                return new MyTaskItem();
            }
        }

        /// <summary>
        /// itaskitem implementation array output parameter
        /// </summary>
        [Output]
        public MyTaskItem[] MyTaskItemArrayOutputParameter
        {
            get
            {
                return new MyTaskItem[] { new MyTaskItem() };
            }
        }

        /// <summary>
        /// taskitem output parameter
        /// </summary>
        [Output]
        public TaskItem TaskItemOutputParameter
        {
            get
            {
                return new TaskItem("foo", String.Empty);
            }
        }

        /// <summary>
        /// taskitem array output parameter
        /// </summary>
        [Output]
        public TaskItem[] TaskItemArrayOutputParameter
        {
            get
            {
                return new TaskItem[] { new TaskItem("foo", String.Empty) };
            }
        }
    }

    /// <summary>
    /// A simple mock task for use with Unit Testing.
    /// </summary>
    sealed internal class MockTask : MockTaskBase, ITask
    {
        private IBuildEngine _e;

        /// <summary>
        /// Task constructor.
        /// </summary>
        /// <param name="e"></param>
        public MockTask(IBuildEngine e)
        {
            _e = e;
        }
        /// <summary>
        /// Access the engine.
        /// </summary>
        public IBuildEngine BuildEngine
        {
            get { return _e; }
            set { _e = value; }
        }

        /// <summary>
        /// Access the host object.
        /// </summary>
        public ITaskHost HostObject
        {
            get { return null; }
            set { }
        }

        /// <summary>
        /// Main Execute method of the task does nothing.
        /// </summary>
        /// <returns>true if successful</returns>
        public bool Execute()
        {
            return true;
        }
    }

    /// <summary>
    /// Custom implementation of ITaskItem for unit testing
    /// Just TaskItem would work fine, but why not test a custom type as well
    /// </summary>
    internal class MyTaskItem : ITaskItem
    {
        #region ITaskItem Members

        public string ItemSpec
        {
            get
            {
                return "foo";
            }
            set
            {
                // do nothing
            }
        }

        public ICollection MetadataNames
        {
            get
            {
                return new ArrayList();
            }
        }

        public int MetadataCount
        {
            get { return 1; }
        }

        public string GetMetadata(string attributeName)
        {
            return "foo";
        }

        public void SetMetadata(string attributeName, string attributeValue)
        {
            // do nothing
        }

        public void RemoveMetadata(string attributeName)
        {
            // do nothing
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            // do nothing
        }

        public IDictionary CloneCustomMetadata()
        {
            return new Hashtable();
        }

        #endregion
    }
}
