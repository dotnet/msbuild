// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;


namespace Microsoft.Build.UnitTests
{
    internal class MockTaskBase
    {
        private bool myBoolParam = false;
        private bool[] myBoolArrayParam = null;
        private int myIntParam = 0;
        private int[] myIntArrayParam = null;
        private string myStringParam = null;
        private string[] myStringArrayParam = null;
        private ITaskItem myITaskItemParam = null;
        private ITaskItem[] myITaskItemArrayParam = null;

        private bool myRequiredBoolParam = false;
        private bool[] myRequiredBoolArrayParam = null;
        private int myRequiredIntParam = 0;
        private int[] myRequiredIntArrayParam = null;
        private string myRequiredStringParam = null;
        private string[] myRequiredStringArrayParam = null;
        private ITaskItem myRequiredITaskItemParam = null;
        private ITaskItem[] myRequiredITaskItemArrayParam = null;

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
        internal bool        myRequiredBoolParamWasSet             = false;
        internal bool        myRequiredBoolArrayParamWasSet        = false;
        internal bool        myRequiredIntParamWasSet              = false;
        internal bool        myRequiredIntArrayParamWasSet         = false;
        internal bool        myRequiredStringParamWasSet           = false;
        internal bool        myRequiredStringArrayParamWasSet      = false;
        internal bool        myRequiredITaskItemParamWasSet        = false;
        internal bool        myRequiredITaskItemArrayParamWasSet   = false;
#pragma warning restore 0414

        /// <summary>
        /// Single bool parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        public bool MyBoolParam
        {
            get { return this.myBoolParam; }
            set { this.myBoolParam = value; this.myBoolParamWasSet = true; }
        }

        /// <summary>
        /// bool[] parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        public bool[] MyBoolArrayParam
        {
            get { return this.myBoolArrayParam; }
            set { this.myBoolArrayParam = value; this.myBoolArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single int parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        public int MyIntParam
        {
            get { return this.myIntParam; }
            set { this.myIntParam = value; this.myIntParamWasSet = true; }
        }

        /// <summary>
        /// int[] parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        public int[] MyIntArrayParam
        {
            get { return this.myIntArrayParam; }
            set { this.myIntArrayParam = value; this.myIntArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single string parameter
        /// </summary>
        /// <owner>RGoel</owner>
        public string MyStringParam
        {
            get { return this.myStringParam; }
            set { this.myStringParam = value; this.myStringParamWasSet = true; }
        }

        /// <summary>
        /// A string array parameter.
        /// </summary>
        /// <owner>JomoF</owner>
        public string[] MyStringArrayParam
        {
            get { return this.myStringArrayParam; }
            set { this.myStringArrayParam = value; this.myStringArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single ITaskItem parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        public ITaskItem MyITaskItemParam
        {
            get { return this.myITaskItemParam; }
            set { this.myITaskItemParam = value; this.myITaskItemParamWasSet = true; }
        }

        /// <summary>
        /// ITaskItem[] parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        public ITaskItem[] MyITaskItemArrayParam
        {
            get { return this.myITaskItemArrayParam; }
            set { this.myITaskItemArrayParam = value; this.myITaskItemArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single bool parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        [Required]
        public bool MyRequiredBoolParam
        {
            get { return this.myRequiredBoolParam; }
            set { this.myRequiredBoolParam = value; this.myRequiredBoolParamWasSet = true; }
        }

        /// <summary>
        /// bool[] parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        [Required]
        public bool[] MyRequiredBoolArrayParam
        {
            get { return this.myRequiredBoolArrayParam; }
            set { this.myRequiredBoolArrayParam = value; this.myRequiredBoolArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single int parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        [Required]
        public int MyRequiredIntParam
        {
            get { return this.myRequiredIntParam; }
            set { this.myRequiredIntParam = value; this.myRequiredIntParamWasSet = true; }
        }

        /// <summary>
        /// int[] parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        [Required]
        public int[] MyRequiredIntArrayParam
        {
            get { return this.myRequiredIntArrayParam; }
            set { this.myRequiredIntArrayParam = value; this.myRequiredIntArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single string parameter
        /// </summary>
        /// <owner>RGoel</owner>
        [Required]
        public string MyRequiredStringParam
        {
            get { return this.myRequiredStringParam; }
            set { this.myRequiredStringParam = value; this.myRequiredStringParamWasSet = true; }
        }

        /// <summary>
        /// A string array parameter.
        /// </summary>
        /// <owner>JomoF</owner>
        [Required]
        public string[] MyRequiredStringArrayParam
        {
            get { return this.myRequiredStringArrayParam; }
            set { this.myRequiredStringArrayParam = value; this.myRequiredStringArrayParamWasSet = true; }
        }

        /// <summary>
        /// Single ITaskItem parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        [Required]
        public ITaskItem MyRequiredITaskItemParam
        {
            get { return this.myRequiredITaskItemParam; }
            set { this.myRequiredITaskItemParam = value; this.myRequiredITaskItemParamWasSet = true; }
        }

        /// <summary>
        /// ITaskItem[] parameter.
        /// </summary>
        /// <owner>RGoel</owner>
        [Required]
        public ITaskItem[] MyRequiredITaskItemArrayParam
        {
            get { return this.myRequiredITaskItemArrayParam; }
            set { this.myRequiredITaskItemArrayParam = value; this.myRequiredITaskItemArrayParamWasSet = true; }
        }

        /// <summary>
        /// ArrayList output parameter.  (This is not supported by MSBuild.)
        /// </summary>
        /// <owner>RGoel</owner>
        [Output]
        public ArrayList MyArrayListOutputParam
        {
            get { return null; }
        }

        /// <summary>
        /// Null ITaskItem[] output parameter. 
        /// </summary>
        /// <owner>danmose</owner>
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
        /// <owner>danmose</owner>
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
        /// <owner>danmose</owner>
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
        /// <owner>danmose</owner>
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
        /// <owner>danmose</owner>
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
        /// <owner>danmose</owner>
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
        /// <owner>danmose</owner>
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
        /// <owner>danmose</owner>
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
        /// <owner>danmose</owner>
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
                return new TaskItem("foo");
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
                return new TaskItem[] { new TaskItem("foo") };
            }
        }
    }
    
    /// <summary>
    /// A simple mock task for use with Unit Testing.
    /// </summary>
    /// <owner>JomoF</owner>
    sealed internal class MockTask : MockTaskBase,ITask
    {
        private IBuildEngine e = null;

        /// <summary>
        /// Task constructor.
        /// </summary>
        /// <param name="e"></param>
        /// <owner>JomoF</owner>
        public MockTask(IBuildEngine e)
        {
            this.e = e;
        }
        /// <summary>
        /// Access the engine.
        /// </summary>
        /// <owner>JomoF</owner>
        public IBuildEngine BuildEngine
        {
            get {return this.e;}
            set {this.e = value;}
        }        

        /// <summary>
        /// Access the host object.
        /// </summary>
        /// <owner>RGoel</owner>
        public ITaskHost HostObject
        {
            get {return null;}
            set {}
        }        

        /// <summary>
        /// Main Execute method of the task does nothing.
        /// </summary>
        /// <returns>true if successful</returns>
        /// <owner>JomoF</owner>
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
