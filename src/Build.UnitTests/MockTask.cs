// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

using Microsoft.Build.Framework;

using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    internal class MockTaskBase
    {
        private bool _myBoolParam = false;
        private bool[] _myBoolArrayParam = null;
        private int _myIntParam = 0;
        private int[] _myIntArrayParam = null;
        private string _myStringParam = null;
        private string[] _myStringArrayParam = null;
        private ITaskItem _myITaskItemParam = null;
        private ITaskItem[] _myITaskItemArrayParam = null;

        private bool _myRequiredBoolParam = false;
        private bool[] _myRequiredBoolArrayParam = null;
        private int _myRequiredIntParam = 0;
        private int[] _myRequiredIntArrayParam = null;
        private string _myRequiredStringParam = null;
        private string[] _myRequiredStringArrayParam = null;
        private ITaskItem _myRequiredITaskItemParam = null;
        private ITaskItem[] _myRequiredITaskItemArrayParam = null;

        internal bool myBoolParamWasSet = false;
        internal bool myBoolArrayParamWasSet = false;
        internal bool myIntParamWasSet = false;
        internal bool myIntArrayParamWasSet = false;
        internal bool myStringParamWasSet = false;
        internal bool myStringArrayParamWasSet = false;
        internal bool myITaskItemParamWasSet = false;
        internal bool myITaskItemArrayParamWasSet = false;

        // disable csharp compiler warning #0414: field assigned unused value
#pragma warning disable 0414
        internal bool myRequiredBoolParamWasSet = false;
        internal bool myRequiredBoolArrayParamWasSet = false;
        internal bool myRequiredIntParamWasSet = false;
        internal bool myRequiredIntArrayParamWasSet = false;
        internal bool myRequiredStringParamWasSet = false;
        internal bool myRequiredStringArrayParamWasSet = false;
        internal bool myRequiredITaskItemParamWasSet = false;
        internal bool myRequiredITaskItemArrayParamWasSet = false;
#pragma warning restore 0414

        /// <summary>
        /// Single bool parameter.
        /// </summary>
        public bool MyBoolParam
        {
            get => _myBoolParam;
            set { _myBoolParam = value; this.myBoolParamWasSet = true; }
        }

        /// <summary>
        /// bool[] parameter.
        /// </summary>
        public bool[] MyBoolArrayParam
        {
            get => _myBoolArrayParam;
            set { _myBoolArrayParam = value; this.myBoolArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single int parameter.
        /// </summary>
        public int MyIntParam
        {
            get => _myIntParam;
            set { _myIntParam = value; this.myIntParamWasSet = true; }
        }

        /// <summary>
        /// int[] parameter.
        /// </summary>
        public int[] MyIntArrayParam
        {
            get => _myIntArrayParam;
            set { _myIntArrayParam = value; this.myIntArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single string parameter
        /// </summary>
        public string MyStringParam
        {
            get => _myStringParam;
            set { _myStringParam = value; this.myStringParamWasSet = true; }
        }

        /// <summary>
        /// A string array parameter.
        /// </summary>
        public string[] MyStringArrayParam
        {
            get => _myStringArrayParam;
            set { _myStringArrayParam = value; this.myStringArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single ITaskItem parameter.
        /// </summary>
        public ITaskItem MyITaskItemParam
        {
            get => _myITaskItemParam;
            set { _myITaskItemParam = value; this.myITaskItemParamWasSet = true; }
        }

        /// <summary>
        /// ITaskItem[] parameter.
        /// </summary>
        public ITaskItem[] MyITaskItemArrayParam
        {
            get => _myITaskItemArrayParam;
            set { _myITaskItemArrayParam = value; this.myITaskItemArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single bool parameter.
        /// </summary>
        [Required]
        public bool MyRequiredBoolParam
        {
            get => _myRequiredBoolParam;
            set { _myRequiredBoolParam = value; this.myRequiredBoolParamWasSet = true; }
        }

        /// <summary>
        /// bool[] parameter.
        /// </summary>
        [Required]
        public bool[] MyRequiredBoolArrayParam
        {
            get => _myRequiredBoolArrayParam;
            set { _myRequiredBoolArrayParam = value; this.myRequiredBoolArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single int parameter.
        /// </summary>
        [Required]
        public int MyRequiredIntParam
        {
            get => _myRequiredIntParam;
            set { _myRequiredIntParam = value; this.myRequiredIntParamWasSet = true; }
        }

        /// <summary>
        /// int[] parameter.
        /// </summary>
        [Required]
        public int[] MyRequiredIntArrayParam
        {
            get => _myRequiredIntArrayParam;
            set { _myRequiredIntArrayParam = value; this.myRequiredIntArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single string parameter
        /// </summary>
        [Required]
        public string MyRequiredStringParam
        {
            get => _myRequiredStringParam;
            set { _myRequiredStringParam = value; this.myRequiredStringParamWasSet = true; }
        }

        /// <summary>
        /// A string array parameter.
        /// </summary>
        [Required]
        public string[] MyRequiredStringArrayParam
        {
            get => _myRequiredStringArrayParam;
            set { _myRequiredStringArrayParam = value; this.myRequiredStringArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single ITaskItem parameter.
        /// </summary>
        [Required]
        public ITaskItem MyRequiredITaskItemParam
        {
            get => _myRequiredITaskItemParam;
            set { _myRequiredITaskItemParam = value; this.myRequiredITaskItemParamWasSet = true; }
        }

        /// <summary>
        /// ITaskItem[] parameter.
        /// </summary>
        [Required]
        public ITaskItem[] MyRequiredITaskItemArrayParam
        {
            get => _myRequiredITaskItemArrayParam;
            set { _myRequiredITaskItemArrayParam = value; this.myRequiredITaskItemArrayParamWasSet = true; }
        }

        /// <summary>
        /// ArrayList output parameter.  (This is not supported by MSBuild.)
        /// </summary>
        [Output]
        public ArrayList MyArrayListOutputParam => null;

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
        public string EmptyStringOutputParameter => String.Empty;

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
        public string StringOutputParameter => "foo";

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
        public int IntOutputParameter => 1;

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
        public MyTaskItem MyTaskItemOutputParameter => new MyTaskItem();

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
        public TaskItem TaskItemOutputParameter => new TaskItem("foo", String.Empty);

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
        private IBuildEngine _e = null;

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
            get => _e;
            set => _e = value;
        }

        /// <summary>
        /// Access the host object.
        /// </summary>
        public ITaskHost HostObject
        {
            get => null;
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
            get => "foo";
            set
            {
                // do nothing
            }
        }

        public ICollection MetadataNames => new ArrayList();

        public int MetadataCount => 1;

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
