// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Xunit;
using Microsoft.Build.Engine.UnitTests.TestComparers;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// The test class for the TaskExecutionHost
    /// </summary>
    public class TaskExecutionHost_Tests : ITestTaskHost, IBuildEngine2, IDisposable
    {
        /// <summary>
        /// The set of parameters which have been initialized on the task.
        /// </summary>
        private Dictionary<string, object> _parametersSetOnTask;

        /// <summary>
        /// The set of outputs which were read from the task.
        /// </summary>
        private Dictionary<string, object> _outputsReadFromTask;

        /// <summary>
        /// The task execution host
        /// </summary>
        private ITaskExecutionHost _host;

        /// <summary>
        /// The mock logging service
        /// </summary>
        private ILoggingService _loggingService;

        /// <summary>
        /// The mock logger
        /// </summary>
        private MockLogger _logger;

        /// <summary>
        /// Array containing one item, used for ITaskItem tests.
        /// </summary>
        private ITaskItem[] _oneItem;

        /// <summary>
        /// Array containing two items, used for ITaskItem tests.
        /// </summary>
        private ITaskItem[] _twoItems;

        /// <summary>
        /// The bucket which receives outputs.
        /// </summary>
        private ItemBucket _bucket;

        /// <summary>
        /// Unused.
        /// </summary>
        public bool IsRunningMultipleNodes
        {
            get { return false; }
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public bool ContinueOnError
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public int LineNumberOfTaskNode
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public int ColumnNumberOfTaskNode
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public string ProjectFileOfTaskNode
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Prepares the environment for the test.
        /// </summary>
        public TaskExecutionHost_Tests()
        {
            InitializeHost(false);
        }

        /// <summary>
        /// Cleans up after the test
        /// </summary>
        public void Dispose()
        {
            if (_host != null)
            {
                ((IDisposable)_host).Dispose();
            }

            _host = null;
        }

        /// <summary>
        /// Validate that setting parameters with only the required parameters works.
        /// </summary>
        [Fact]
        public void ValidateNoParameters()
        {
            var parameters = new Dictionary<string, (string, ElementLocation)>(StringComparer.OrdinalIgnoreCase);
            parameters["ExecuteReturnParam"] = ("true", ElementLocation.Create("foo.proj"));

            Assert.True(_host.SetTaskParameters(parameters));
            Assert.Single(_parametersSetOnTask);
            Assert.True(_parametersSetOnTask.ContainsKey("ExecuteReturnParam"));
        }

        /// <summary>
        /// Validate that setting no parameters when a required parameter exists fails and throws an exception.
        /// </summary>
        [Fact]
        public void ValidateNoParameters_MissingRequired()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                var parameters = new Dictionary<string, (string, ElementLocation)>(StringComparer.OrdinalIgnoreCase);
                _host.SetTaskParameters(parameters);
            }
           );
        }
        /// <summary>
        /// Validate that setting a non-existent parameter fails, but does not throw an exception.
        /// </summary>
        [Fact]
        public void ValidateNonExistantParameter()
        {
            var parameters = new Dictionary<string, (string, ElementLocation)>(StringComparer.OrdinalIgnoreCase);
            parameters["NonExistantParam"] = ("foo", ElementLocation.Create("foo.proj"));
            Assert.False(_host.SetTaskParameters(parameters));
        }

        #region Bool Params

        /// <summary>
        /// Validate that setting a bool param works and sets the right value.
        /// </summary>
        [Fact]
        public void TestSetBoolParam()
        {
            ValidateTaskParameter("BoolParam", "true", true);
        }

        /// <summary>
        /// Validate that setting a bool param works and sets the right value.
        /// </summary>
        [Fact]
        public void TestSetBoolParamFalse()
        {
            ValidateTaskParameter("BoolParam", "false", false);
        }

        /// <summary>
        /// Validate that setting a bool param with an empty value does not cause the parameter to get set.
        /// </summary>
        [Fact]
        public void TestSetBoolParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("BoolParam", "");
        }

        /// <summary>
        /// Validate that setting a bool param with a property which evaluates to nothing does not cause the parameter to get set.
        /// </summary>
        [Fact]
        public void TestSetBoolParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("BoolParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting a bool param with an item which evaluates to nothing does not cause the parameter to get set.
        /// </summary>
        [Fact]
        public void TestSetBoolParamEmptyItem()
        {
            ValidateTaskParameterNotSet("BoolParam", "@(NonExistantItem)");
        }

        #endregion

        #region Bool Array Params

        /// <summary>
        /// Validate that setting a bool array with a single true sets the array to one 'true' value.
        /// </summary>
        [Fact]
        public void TestSetBoolArrayParamOneItem()
        {
            ValidateTaskParameterArray("BoolArrayParam", "true", new bool[] { true });
        }

        /// <summary>
        /// Validate that setting a bool array with a list of two values sets them appropriately.
        /// </summary>
        [Fact]
        public void TestSetBoolArrayParamTwoItems()
        {
            ValidateTaskParameterArray("BoolArrayParam", "false;true", new bool[] { false, true });
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetBoolArrayParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("BoolArrayParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetBoolArrayParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("BoolArrayParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetBoolArrayParamEmptyItem()
        {
            ValidateTaskParameterNotSet("BoolArrayParam", "@(NonExistantItem)");
        }

        #endregion

        #region Int Params

        /// <summary>
        /// Validate that setting an int param with a value of 0 causes it to get the correct value.
        /// </summary>
        [Fact]
        public void TestSetIntParamZero()
        {
            ValidateTaskParameter("IntParam", "0", 0);
        }

        /// <summary>
        /// Validate that setting an int param with a value of 1 causes it to get the correct value.
        /// </summary>
        [Fact]
        public void TestSetIntParamOne()
        {
            ValidateTaskParameter("IntParam", "1", 1);
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetIntParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("IntParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetIntParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("IntParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetIntParamEmptyItem()
        {
            ValidateTaskParameterNotSet("IntParam", "@(NonExistantItem)");
        }

        #endregion

        #region Int Array Params

        /// <summary>
        /// Validate that setting an int array with a single value causes it to get a single value.
        /// </summary>
        [Fact]
        public void TestSetIntArrayParamOneItem()
        {
            ValidateTaskParameterArray("IntArrayParam", "0", new int[] { 0 });
        }

        /// <summary>
        /// Validate that setting an int array with a list of values causes it to get the correct values.
        /// </summary>
        [Fact]
        public void TestSetIntArrayParamTwoItems()
        {
            SetTaskParameter("IntArrayParam", "1;0");

            Assert.True(_parametersSetOnTask.ContainsKey("IntArrayParam"));

            Assert.Equal(1, ((int[])_parametersSetOnTask["IntArrayParam"])[0]);
            Assert.Equal(0, ((int[])_parametersSetOnTask["IntArrayParam"])[1]);
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetIntArrayParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("IntArrayParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetIntArrayParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("IntArrayParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetIntArrayParamEmptyItem()
        {
            ValidateTaskParameterNotSet("IntArrayParam", "@(NonExistantItem)");
        }

        #endregion

        #region String Params

        /// <summary>
        /// Test that setting a string param sets the correct value.
        /// </summary>
        [Fact]
        public void TestSetStringParam()
        {
            ValidateTaskParameter("StringParam", "0", "0");
        }

        /// <summary>
        /// Test that setting a string param sets the correct value.
        /// </summary>
        [Fact]
        public void TestSetStringParamOne()
        {
            ValidateTaskParameter("StringParam", "1", "1");
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetStringParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("StringParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetStringParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("StringParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetStringParamEmptyItem()
        {
            ValidateTaskParameterNotSet("StringParam", "@(NonExistantItem)");
        }

        #endregion

        #region String Array Params

        /// <summary>
        /// Validate that setting a string array with a single value sets the correct value.
        /// </summary>
        [Fact]
        public void TestSetStringArrayParam()
        {
            ValidateTaskParameterArray("StringArrayParam", "0", new string[] { "0" });
        }

        /// <summary>
        /// Validate that setting a string array with a list of two values sets the correct values.
        /// </summary>
        [Fact]
        public void TestSetStringArrayParamOne()
        {
            ValidateTaskParameterArray("StringArrayParam", "1;0", new string[] { "1", "0" });
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetStringArrayParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("StringArrayParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetStringArrayParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("StringArrayParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetStringArrayParamEmptyItem()
        {
            ValidateTaskParameterNotSet("StringArrayParam", "@(NonExistantItem)");
        }

        #endregion

        #region ITaskItem Params

        /// <summary>
        /// Validate that setting an item with an item list evaluating to one item sets the value appropriately, including metadata.
        /// </summary>
        [Fact]
        public void TestSetItemParamSingle()
        {
            ValidateTaskParameterItem("ItemParam", "@(ItemListContainingOneItem)", _oneItem[0]);
        }

        /// <summary>
        /// Validate that setting an item with an item list evaluating to two items sets the value appropriately, including metadata.
        /// </summary>
        [Fact]
        public void TestSetItemParamDouble()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ValidateTaskParameterItems("ItemParam", "@(ItemListContainingTwoItems)", _twoItems);
            }
           );
        }
        /// <summary>
        /// Validate that setting an item with a string results in an item with the evaluated include set to the string.
        /// </summary>
        [Fact]
        public void TestSetItemParamString()
        {
            ValidateTaskParameterItem("ItemParam", "MyItemName");
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetItemParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("ItemParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a property which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetItemParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("ItemParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetItemParamEmptyItem()
        {
            ValidateTaskParameterNotSet("ItemParam", "@(NonExistantItem)");
        }

        #endregion

        #region ITaskItem Array Params

        /// <summary>
        /// Validate that setting an item array using an item list containing one item sets a single item.
        /// </summary>
        [Fact]
        public void TestSetItemArrayParamSingle()
        {
            ValidateTaskParameterItems("ItemArrayParam", "@(ItemListContainingOneItem)", _oneItem);
        }

        /// <summary>
        /// Validate that setting an item array using an item list containing two items sets both items.
        /// </summary>
        [Fact]
        public void TestSetItemArrayParamDouble()
        {
            ValidateTaskParameterItems("ItemArrayParam", "@(ItemListContainingTwoItems)", _twoItems);
        }

        /// <summary>
        /// Validate that setting an item array with
        /// </summary>
        [Fact]
        public void TestSetItemArrayParamString()
        {
            ValidateTaskParameterItems("ItemArrayParam", "MyItemName");
        }

        /// <summary>
        /// Validate that setting an item array with a list with multiple values creates multiple items.
        /// </summary>
        [Fact]
        public void TestSetItemArrayParamTwoStrings()
        {
            ValidateTaskParameterItems("ItemArrayParam", "MyItemName;MyOtherItemName", new string[] { "MyItemName", "MyOtherItemName" });
        }

        /// <summary>
        /// Validate that setting the parameter with an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetItemArrayParamEmptyAttribute()
        {
            ValidateTaskParameterNotSet("ItemArrayParam", "");
        }

        /// <summary>
        /// Validate that setting the parameter with a parameter which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetItemArrayParamEmptyProperty()
        {
            ValidateTaskParameterNotSet("ItemArrayParam", "$(NonExistantProperty)");
        }

        /// <summary>
        /// Validate that setting the parameter with an item which evaluates to an empty value does not cause it to be set.
        /// </summary>
        [Fact]
        public void TestSetItemArrayParamEmptyItem()
        {
            ValidateTaskParameterNotSet("ItemArrayParam", "@(NonExistantItem)");
        }

        #endregion

        #region Execute Tests

        /// <summary>
        /// Tests that successful execution returns true.
        /// </summary>
        [Fact]
        public void TestExecuteTrue()
        {
            var parameters = new Dictionary<string, (string, ElementLocation)>(StringComparer.OrdinalIgnoreCase);
            parameters["ExecuteReturnParam"] = ("true", ElementLocation.Create("foo.proj"));

            Assert.True(_host.SetTaskParameters(parameters));

            bool executeValue = _host.Execute();

            Assert.True(executeValue);
        }

        /// <summary>
        /// Tests that unsuccessful execution returns false.
        /// </summary>
        [Fact]
        public void TestExecuteFalse()
        {
            var parameters = new Dictionary<string, (string, ElementLocation)>(StringComparer.OrdinalIgnoreCase);
            parameters["ExecuteReturnParam"] = ("false", ElementLocation.Create("foo.proj"));

            Assert.True(_host.SetTaskParameters(parameters));

            bool executeValue = _host.Execute();

            Assert.False(executeValue);
        }

        /// <summary>
        /// Tests that when Execute throws, the exception bubbles up.
        /// </summary>
        [Fact]
        public void TestExecuteThrow()
        {
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                var parameters = new Dictionary<string, (string, ElementLocation)>(StringComparer.OrdinalIgnoreCase);
                parameters["ExecuteReturnParam"] = ("false", ElementLocation.Create("foo.proj"));

                Dispose();
                InitializeHost(true);

                Assert.True(_host.SetTaskParameters(parameters));

                _host.Execute();
            }
           );
        }
        #endregion

        #region Bool Outputs

        /// <summary>
        /// Validate that boolean output to an item produces the correct evaluated include.
        /// </summary>
        [Fact]
        public void TestOutputBoolToItem()
        {
            SetTaskParameter("BoolParam", "true");
            ValidateOutputItem("BoolOutput", "True");
        }

        /// <summary>
        /// Validate that boolean output to a property produces the correct evaluated value.
        /// </summary>
        [Fact]
        public void TestOutputBoolToProperty()
        {
            SetTaskParameter("BoolParam", "true");
            ValidateOutputProperty("BoolOutput", "True");
        }

        /// <summary>
        /// Validate that boolean array output to an item  array produces the correct evaluated includes.
        /// </summary>
        [Fact]
        public void TestOutputBoolArrayToItems()
        {
            SetTaskParameter("BoolArrayParam", "false;true");
            ValidateOutputItems("BoolArrayOutput", new string[] { "False", "True" });
        }

        /// <summary>
        /// Validate that boolean array output to an item produces the correct semi-colon-delimited evaluated value.
        /// </summary>
        [Fact]
        public void TestOutputBoolArrayToProperty()
        {
            SetTaskParameter("BoolArrayParam", "false;true");
            ValidateOutputProperty("BoolArrayOutput", "False;True");
        }

        #endregion

        #region Int Outputs

        /// <summary>
        /// Validate that an int output to an item produces the correct evaluated include
        /// </summary>
        [Fact]
        public void TestOutputIntToItem()
        {
            SetTaskParameter("IntParam", "42");
            ValidateOutputItem("IntOutput", "42");
        }

        /// <summary>
        /// Validate that an int output to an property produces the correct evaluated value.
        /// </summary>
        [Fact]
        public void TestOutputIntToProperty()
        {
            SetTaskParameter("IntParam", "42");
            ValidateOutputProperty("IntOutput", "42");
        }

        /// <summary>
        /// Validate that an int array output to an item produces the correct evaluated includes.
        /// </summary>
        [Fact]
        public void TestOutputIntArrayToItems()
        {
            SetTaskParameter("IntArrayParam", "42;99");
            ValidateOutputItems("IntArrayOutput", new string[] { "42", "99" });
        }

        /// <summary>
        /// Validate that an int array output to a property produces the correct semi-colon-delimited evaluated value.
        /// </summary>
        [Fact]
        public void TestOutputIntArrayToProperty()
        {
            SetTaskParameter("IntArrayParam", "42;99");
            ValidateOutputProperty("IntArrayOutput", "42;99");
        }

        #endregion

        #region String Outputs

        /// <summary>
        /// Validate that a string output to an item produces the correct evaluated include.
        /// </summary>
        [Fact]
        public void TestOutputStringToItem()
        {
            SetTaskParameter("StringParam", "FOO");
            ValidateOutputItem("StringOutput", "FOO");
        }

        /// <summary>
        /// Validate that a string output to a property produces the correct evaluated value.
        /// </summary>
        [Fact]
        public void TestOutputStringToProperty()
        {
            SetTaskParameter("StringParam", "FOO");
            ValidateOutputProperty("StringOutput", "FOO");
        }

        /// <summary>
        /// Validate that an empty string output overwrites the property value
        /// </summary>
        [Fact]
        public void TestOutputEmptyStringToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("EmptyStringOutput", String.Empty);
        }

        /// <summary>
        /// Validate that an empty string array output overwrites the property value
        /// </summary>
        [Fact]
        public void TestOutputEmptyStringArrayToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("EmptyStringArrayOutput", String.Empty);
        }

        /// <summary>
        /// A string output returning null should not cause any property set.
        /// </summary>
        [Fact]
        public void TestOutputNullStringToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("NullStringOutput", "initialvalue");
        }

        /// <summary>
        /// A string output returning null should not cause any property set.
        /// </summary>
        [Fact]
        public void TestOutputNullITaskItemToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("NullITaskItemOutput", "initialvalue");
        }

        /// <summary>
        /// A string output returning null should not cause any property set.
        /// </summary>
        [Fact]
        public void TestOutputNullStringArrayToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("NullStringArrayOutput", "initialvalue");
        }

        /// <summary>
        /// A string output returning null should not cause any property set.
        /// </summary>
        [Fact]
        public void TestOutputNullITaskItemArrayToProperty()
        {
            _bucket.Lookup.SetProperty(ProjectPropertyInstance.Create("output", "initialvalue"));
            ValidateOutputProperty("NullITaskItemArrayOutput", "initialvalue");
        }

        /// <summary>
        /// Validate that a string array output to an item produces the correct evaluated includes.
        /// </summary>
        [Fact]
        public void TestOutputStringArrayToItems()
        {
            SetTaskParameter("StringArrayParam", "FOO;bar");
            ValidateOutputItems("StringArrayOutput", new string[] { "FOO", "bar" });
        }

        /// <summary>
        /// Validate that a string array output to a property produces the correct semi-colon-delimited evaluated value.
        /// </summary>
        [Fact]
        public void TestOutputStringArrayToProperty()
        {
            SetTaskParameter("StringArrayParam", "FOO;bar");
            ValidateOutputProperty("StringArrayOutput", "FOO;bar");
        }

        #endregion

        #region Item Outputs

        /// <summary>
        /// Validate that an item output to an item replicates the item, with metadata
        /// </summary>
        [Fact]
        public void TestOutputItemToItem()
        {
            SetTaskParameter("ItemParam", "@(ItemListContainingOneItem)");
            ValidateOutputItems("ItemOutput", _oneItem);
        }

        /// <summary>
        /// Validate than an item output to a property produces the correct evaluated value.
        /// </summary>
        [Fact]
        public void TestOutputItemToProperty()
        {
            SetTaskParameter("ItemParam", "@(ItemListContainingOneItem)");
            ValidateOutputProperty("ItemOutput", _oneItem[0].ItemSpec);
        }

        /// <summary>
        /// Validate that an item array output to an item replicates the items, with metadata.
        /// </summary>
        [Fact]
        public void TestOutputItemArrayToItems()
        {
            SetTaskParameter("ItemArrayParam", "@(ItemListContainingTwoItems)");
            ValidateOutputItems("ItemArrayOutput", _twoItems);
        }

        /// <summary>
        /// Validate that an item array output to a property produces the correct semi-colon-delimited evaluated value.
        /// </summary>
        [Fact]
        public void TestOutputItemArrayToProperty()
        {
            SetTaskParameter("ItemArrayParam", "@(ItemListContainingTwoItems)");
            ValidateOutputProperty("ItemArrayOutput", String.Concat(_twoItems[0].ItemSpec, ";", _twoItems[1].ItemSpec));
        }

        #endregion

        #region Other Output Tests

        /// <summary>
        /// Attempts to gather outputs into an item list from an string task parameter that
        /// returns an empty string. This should be a no-op.
        /// </summary>
        [Fact]
        public void TestEmptyStringInStringArrayParameterIntoItemList()
        {
            SetTaskParameter("StringArrayParam", "");
            ValidateOutputItems("StringArrayOutput", new ITaskItem[] { });
        }

        /// <summary>
        /// Attempts to gather outputs into an item list from an string task parameter that
        /// returns an empty string. This should be a no-op.
        /// </summary>
        [Fact]
        public void TestEmptyStringParameterIntoItemList()
        {
            SetTaskParameter("StringParam", "");
            ValidateOutputItems("StringOutput", new ITaskItem[] { });
        }

        /// <summary>
        /// Attempts to gather outputs from a null task parameter of type "ITaskItem[]".  This should succeed.
        /// </summary>
        [Fact]
        public void TestNullITaskItemArrayParameter()
        {
            ValidateOutputItems("ItemArrayNullOutput", new ITaskItem[] { });
        }

        /// <summary>
        /// Attempts to gather outputs from a task parameter of type "ArrayList".  This should fail.
        /// </summary>
        [Fact]
        public void TestArrayListParameter()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ValidateOutputItems("ArrayListOutput", new ITaskItem[] { });
            }
           );
        }
        /// <summary>
        /// Attempts to gather outputs from a non-existent output.  This should fail.
        /// </summary>
        [Fact]
        public void TestNonexistantOutput()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                Assert.False(_host.GatherTaskOutputs("NonExistantOutput", ElementLocation.Create(".", 1, 1), true, "output"));
            }
           );
        }
        /// <summary>
        /// object[] should not be a supported output type.
        /// </summary>
        [Fact]
        public void TestOutputObjectArrayToProperty()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ValidateOutputProperty("ObjectArrayOutput", "");
            }
           );
        }
        #endregion

        #region Other Tests

        /// <summary>
        /// Test that cleanup for task clears out the task instance.
        /// </summary>
        [Fact]
        public void TestCleanupForTask()
        {
            _host.CleanupForBatch();
            Assert.NotNull((_host as TaskExecutionHost)._UNITTESTONLY_TaskFactoryWrapper);
            _host.CleanupForTask();
            Assert.Null((_host as TaskExecutionHost)._UNITTESTONLY_TaskFactoryWrapper);
        }

        /// <summary>
        /// Test that a using task which specifies an invalid assembly produces an exception.
        /// </summary>
        [Fact]
        public void TestTaskResolutionFailureWithUsingTask()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                _loggingService = new MockLoggingService();
                Dispose();
                _host = new TaskExecutionHost();
                TargetLoggingContext tlc = new TargetLoggingContext(_loggingService, new BuildEventContext(1, 1, BuildEventContext.InvalidProjectContextId, 1));

                ProjectInstance project = CreateTestProject();
                _host.InitializeForTask
                    (
                    this,
                    tlc,
                    project,
                    "TaskWithMissingAssembly",
                    ElementLocation.Create("none", 1, 1),
                    this,
                    false,
#if FEATURE_APPDOMAIN
                    null,
#endif
                    false,
                    CancellationToken.None
                    );
                _host.FindTask(null);
                _host.InitializeForBatch(new TaskLoggingContext(_loggingService, tlc.BuildEventContext), _bucket, null);
            }
           );
        }
        /// <summary>
        /// Test that specifying a task with no using task logs an error, but does not throw.
        /// </summary>
        [Fact]
        public void TestTaskResolutionFailureWithNoUsingTask()
        {
            Dispose();
            _host = new TaskExecutionHost();
            TargetLoggingContext tlc = new TargetLoggingContext(_loggingService, new BuildEventContext(1, 1, BuildEventContext.InvalidProjectContextId, 1));

            ProjectInstance project = CreateTestProject();
            _host.InitializeForTask
                (
                this,
                tlc,
                project,
                "TaskWithNoUsingTask",
                ElementLocation.Create("none", 1, 1),
                this,
                false,
#if FEATURE_APPDOMAIN
                null,
#endif
                false,
                CancellationToken.None
                );

            _host.FindTask(null);
            _host.InitializeForBatch(new TaskLoggingContext(_loggingService, tlc.BuildEventContext), _bucket, null);
            _logger.AssertLogContains("MSB4036");
        }

        #endregion

        #region ITestTaskHost Members
        #pragma warning disable xUnit1013

        /// <summary>
        /// Records that a parameter was set on the task.
        /// </summary>
        public void ParameterSet(string parameterName, object valueSet)
        {
            _parametersSetOnTask[parameterName] = valueSet;
        }

        /// <summary>
        /// Records that an output was read from the task.
        /// </summary>
        public void OutputRead(string parameterName, object actualValue)
        {
            _outputsReadFromTask[parameterName] = actualValue;
        }

        #endregion

        #region IBuildEngine2 Members

        /// <summary>
        /// Unused.
        /// </summary>
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IBuildEngine Members

        /// <summary>
        /// Unused.
        /// </summary>
        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unused.
        /// </summary>
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        #pragma warning restore xUnit1013
        #endregion

        #region Validation Routines

        /// <summary>
        /// Is the class a task factory
        /// </summary>
        private static bool IsTaskFactoryClass(Type type, object unused)
        {
            return type.GetTypeInfo().IsClass &&
                !type.GetTypeInfo().IsAbstract &&
#if FEATURE_TYPE_GETINTERFACE
                (type.GetInterface("Microsoft.Build.Framework.ITaskFactory") != null);
#else
                type.GetInterfaces().Any(interfaceType => interfaceType.FullName == "Microsoft.Build.Framework.ITaskFactory");
#endif
        }

        /// <summary>
        /// Initialize the host object
        /// </summary>
        /// <param name="throwOnExecute">Should the task throw when executed</param>
        private void InitializeHost(bool throwOnExecute)
        {
            _loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            _logger = new MockLogger();
            _loggingService.RegisterLogger(_logger);
            _host = new TaskExecutionHost();
            TargetLoggingContext tlc = new TargetLoggingContext(_loggingService, new BuildEventContext(1, 1, BuildEventContext.InvalidProjectContextId, 1));

            // Set up a temporary project and add some items to it.
            ProjectInstance project = CreateTestProject();

            TypeLoader typeLoader = new TypeLoader(IsTaskFactoryClass);
#if !FEATURE_ASSEMBLYLOADCONTEXT
            AssemblyLoadInfo loadInfo = AssemblyLoadInfo.Create(Assembly.GetAssembly(typeof(TaskBuilderTestTask.TaskBuilderTestTaskFactory)).FullName, null);
#else
            AssemblyLoadInfo loadInfo = AssemblyLoadInfo.Create(typeof(TaskBuilderTestTask.TaskBuilderTestTaskFactory).GetTypeInfo().FullName, null);
#endif
            LoadedType loadedType = new LoadedType(typeof(TaskBuilderTestTask.TaskBuilderTestTaskFactory), loadInfo);

            TaskBuilderTestTask.TaskBuilderTestTaskFactory taskFactory = new TaskBuilderTestTask.TaskBuilderTestTaskFactory();
            taskFactory.ThrowOnExecute = throwOnExecute;
            string taskName = "TaskBuilderTestTask";
            (_host as TaskExecutionHost)._UNITTESTONLY_TaskFactoryWrapper = new TaskFactoryWrapper(taskFactory, loadedType, taskName, null);
            _host.InitializeForTask
                (
                this,
                tlc,
                project,
                taskName,
                ElementLocation.Create("none", 1, 1),
                this,
                false,
#if FEATURE_APPDOMAIN
                null,
#endif
                false,
                CancellationToken.None
                );

            ProjectTaskInstance taskInstance = project.Targets["foo"].Tasks.First();
            TaskLoggingContext talc = tlc.LogTaskBatchStarted(".", taskInstance);

            ItemDictionary<ProjectItemInstance> itemsByName = new ItemDictionary<ProjectItemInstance>();

            ProjectItemInstance item = new ProjectItemInstance(project, "ItemListContainingOneItem", "a.cs", ".");
            item.SetMetadata("Culture", "fr-fr");
            itemsByName.Add(item);
            _oneItem = new ITaskItem[] { new TaskItem(item) };

            item = new ProjectItemInstance(project, "ItemListContainingTwoItems", "b.cs", ".");
            ProjectItemInstance item2 = new ProjectItemInstance(project, "ItemListContainingTwoItems", "c.cs", ".");
            item.SetMetadata("HintPath", "c:\\foo");
            item2.SetMetadata("HintPath", "c:\\bar");
            itemsByName.Add(item);
            itemsByName.Add(item2);
            _twoItems = new ITaskItem[] { new TaskItem(item), new TaskItem(item2) };

            _bucket = new ItemBucket(new string[0], new Dictionary<string, string>(), new Lookup(itemsByName, new PropertyDictionary<ProjectPropertyInstance>()), 0);
            _host.FindTask(null);
            _host.InitializeForBatch(talc, _bucket, null);
            _parametersSetOnTask = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _outputsReadFromTask = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateOutputItem(string outputName, string value)
        {
            Assert.True(_host.GatherTaskOutputs(outputName, ElementLocation.Create(".", 1, 1), true, "output"));
            Assert.True(_outputsReadFromTask.ContainsKey(outputName));

            Assert.Single(_bucket.Lookup.GetItems("output"));
            Assert.Equal(value, _bucket.Lookup.GetItems("output").First().EvaluatedInclude);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateOutputItem(string outputName, ITaskItem value)
        {
            Assert.True(_host.GatherTaskOutputs(outputName, ElementLocation.Create(".", 1, 1), true, "output"));
            Assert.True(_outputsReadFromTask.ContainsKey(outputName));

            Assert.Single(_bucket.Lookup.GetItems("output"));
            Assert.Equal(0, TaskItemComparer.Instance.Compare(value, new TaskItem(_bucket.Lookup.GetItems("output").First())));
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateOutputItems(string outputName, string[] values)
        {
            Assert.True(_host.GatherTaskOutputs(outputName, ElementLocation.Create(".", 1, 1), true, "output"));
            Assert.True(_outputsReadFromTask.ContainsKey(outputName));

            Assert.Equal(values.Length, _bucket.Lookup.GetItems("output").Count);
            for (int i = 0; i < values.Length; i++)
            {
                Assert.Equal(values[i], _bucket.Lookup.GetItems("output").ElementAt(i).EvaluatedInclude);
            }
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateOutputItems(string outputName, ITaskItem[] values)
        {
            Assert.True(_host.GatherTaskOutputs(outputName, ElementLocation.Create(".", 1, 1), true, "output"));
            Assert.True(_outputsReadFromTask.ContainsKey(outputName));

            Assert.Equal(values.Length, _bucket.Lookup.GetItems("output").Count);
            for (int i = 0; i < values.Length; i++)
            {
                Assert.Equal(0, TaskItemComparer.Instance.Compare(values[i], new TaskItem(_bucket.Lookup.GetItems("output").ElementAt(i))));
            }
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateOutputProperty(string outputName, string value)
        {
            Assert.True(_host.GatherTaskOutputs(outputName, ElementLocation.Create(".", 1, 1), false, "output"));
            Assert.True(_outputsReadFromTask.ContainsKey(outputName));

            Assert.NotNull(_bucket.Lookup.GetProperty("output"));
            Assert.Equal(value, _bucket.Lookup.GetProperty("output").EvaluatedValue);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameter(string parameterName, string value, object expectedValue)
        {
            SetTaskParameter(parameterName, value);

            Assert.True(_parametersSetOnTask.ContainsKey(parameterName));
            Assert.Equal(expectedValue, _parametersSetOnTask[parameterName]);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterItem(string parameterName, string value)
        {
            SetTaskParameter(parameterName, value);

            Assert.True(_parametersSetOnTask.ContainsKey(parameterName));

            ITaskItem actualItem = _parametersSetOnTask[parameterName] as ITaskItem;
            Assert.Equal(value, actualItem.ItemSpec);
            Assert.Equal(BuiltInMetadata.MetadataCount, actualItem.MetadataCount);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterItem(string parameterName, string value, ITaskItem expectedItem)
        {
            SetTaskParameter(parameterName, value);

            Assert.True(_parametersSetOnTask.ContainsKey(parameterName));

            ITaskItem actualItem = _parametersSetOnTask[parameterName] as ITaskItem;
            Assert.Equal(0, TaskItemComparer.Instance.Compare(expectedItem, actualItem));
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterItems(string parameterName, string value)
        {
            SetTaskParameter(parameterName, value);

            Assert.True(_parametersSetOnTask.ContainsKey(parameterName));

            ITaskItem[] actualItems = _parametersSetOnTask[parameterName] as ITaskItem[];
            Assert.Single(actualItems);
            Assert.Equal(value, actualItems[0].ItemSpec);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterItems(string parameterName, string value, ITaskItem[] expectedItems)
        {
            SetTaskParameter(parameterName, value);

            Assert.True(_parametersSetOnTask.ContainsKey(parameterName));

            ITaskItem[] actualItems = _parametersSetOnTask[parameterName] as ITaskItem[];
            Assert.Equal(expectedItems.Length, actualItems.Length);

            for (int i = 0; i < expectedItems.Length; i++)
            {
                Assert.Equal(0, TaskItemComparer.Instance.Compare(expectedItems[i], actualItems[i]));
            }
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterItems(string parameterName, string value, string[] expectedItems)
        {
            SetTaskParameter(parameterName, value);

            Assert.True(_parametersSetOnTask.ContainsKey(parameterName));

            ITaskItem[] actualItems = _parametersSetOnTask[parameterName] as ITaskItem[];
            Assert.Equal(expectedItems.Length, actualItems.Length);

            for (int i = 0; i < expectedItems.Length; i++)
            {
                Assert.Equal(expectedItems[i], actualItems[i].ItemSpec);
            }
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterArray(string parameterName, string value, object expectedValue)
        {
            SetTaskParameter(parameterName, value);

            Assert.True(_parametersSetOnTask.ContainsKey(parameterName));

            Array expectedArray = expectedValue as Array;
            Array actualArray = _parametersSetOnTask[parameterName] as Array;

            Assert.Equal(expectedArray.Length, actualArray.Length);
            for (int i = 0; i < expectedArray.Length; i++)
            {
                Assert.Equal(expectedArray.GetValue(i), actualArray.GetValue(i));
            }
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void ValidateTaskParameterNotSet(string parameterName, string value)
        {
            SetTaskParameter(parameterName, value);
            Assert.False(_parametersSetOnTask.ContainsKey(parameterName));
        }

        #endregion

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private void SetTaskParameter(string parameterName, string value)
        {
            var parameters = GetStandardParametersDictionary(true);
            parameters[parameterName] = (value, ElementLocation.Create("foo.proj"));
            bool success = _host.SetTaskParameters(parameters);
            Assert.True(success);
        }

        /// <summary>
        /// Helper method for tests
        /// </summary>
        private Dictionary<string, (string, ElementLocation)> GetStandardParametersDictionary(bool returnParam)
        {
            var parameters = new Dictionary<string, (string, ElementLocation)>(StringComparer.OrdinalIgnoreCase);
            parameters["ExecuteReturnParam"] = (returnParam ? "true" : "false", ElementLocation.Create("foo.proj"));
            return parameters;
        }

        /// <summary>
        /// Creates a test project.
        /// </summary>
        /// <returns>The project.</returns>
        private ProjectInstance CreateTestProject()
        {
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <UsingTask TaskName='TaskWithMissingAssembly' AssemblyName='madeup' />
                    <ItemGroup>
                        <Compile Include='b.cs' />
                        <Compile Include='c.cs' />
                    </ItemGroup>

                    <ItemGroup>
                        <Reference Include='System' />
                    </ItemGroup>

                    <Target Name='Empty' />

                    <Target Name='Skip' Inputs='testProject.proj' Outputs='testProject.proj' />

                    <Target Name='Error' >
                        <ErrorTask1 ContinueOnError='True'/>                    
                        <ErrorTask2 ContinueOnError='False'/>  
                        <ErrorTask3 /> 
                        <OnError ExecuteTargets='Foo'/>                  
                        <OnError ExecuteTargets='Bar'/>                  
                    </Target>

                    <Target Name='Foo' Inputs='foo.cpp' Outputs='foo.o'>
                        <FooTask1/>
                    </Target>

                    <Target Name='Bar'>
                        <BarTask1/>
                    </Target>

                    <Target Name='Baz' DependsOnTargets='Bar'>
                        <BazTask1/>
                        <BazTask2/>
                    </Target>

                    <Target Name='Baz2' DependsOnTargets='Bar;Foo'>
                        <Baz2Task1/>
                        <Baz2Task2/>
                        <Baz2Task3/>
                    </Target>

                    <Target Name='DepSkip' DependsOnTargets='Skip'>
                        <DepSkipTask1/>
                        <DepSkipTask2/>
                        <DepSkipTask3/>
                    </Target>

                    <Target Name='DepError' DependsOnTargets='Foo;Skip;Error'>
                        <DepSkipTask1/>
                        <DepSkipTask2/>
                        <DepSkipTask3/>
                    </Target>

                </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(projectFileContents)));
            return project.CreateProjectInstance();
        }
    }
}
