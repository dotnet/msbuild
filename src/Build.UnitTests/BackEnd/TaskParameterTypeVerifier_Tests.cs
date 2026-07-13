// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Shouldly;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for TaskParameterTypeVerifier class
    /// </summary>
    [TestClass]
    public class TaskParameterTypeVerifier_Tests
    {
        #region IsValidScalarInputParameter Tests

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_String_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(string)));
        }

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_ITaskItem_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(ITaskItem)));
        }

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_Bool_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(bool)));
        }

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_Int_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(int)));
        }

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_Double_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(double)));
        }

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_DateTime_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(DateTime)));
        }

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_AbsolutePath_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(AbsolutePath)));
        }

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_Object_ReturnsFalse()
        {
            Assert.IsFalse(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(object)));
        }

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_StringArray_ReturnsFalse()
        {
            Assert.IsFalse(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(string[])));
        }

        #endregion

        #region IsValidVectorInputParameter Tests

        [MSBuildTestMethod]
        public void IsValidVectorInputParameter_StringArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(string[])));
        }

        [MSBuildTestMethod]
        public void IsValidVectorInputParameter_ITaskItemArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(ITaskItem[])));
        }

        [MSBuildTestMethod]
        public void IsValidVectorInputParameter_BoolArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(bool[])));
        }

        [MSBuildTestMethod]
        public void IsValidVectorInputParameter_IntArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(int[])));
        }

        [MSBuildTestMethod]
        public void IsValidVectorInputParameter_DoubleArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(double[])));
        }

        [MSBuildTestMethod]
        public void IsValidVectorInputParameter_DateTimeArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(DateTime[])));
        }

        [MSBuildTestMethod]
        public void IsValidVectorInputParameter_AbsolutePathArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(AbsolutePath[])));
        }

        [MSBuildTestMethod]
        public void IsValidVectorInputParameter_String_ReturnsFalse()
        {
            Assert.IsFalse(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(string)));
        }

        [MSBuildTestMethod]
        public void IsValidVectorInputParameter_ObjectArray_ReturnsFalse()
        {
            Assert.IsFalse(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(object[])));
        }

        #endregion

        #region IsValueTypeOutputParameter Tests

        [MSBuildTestMethod]
        public void IsValueTypeOutputParameter_String_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(string)));
        }

        [MSBuildTestMethod]
        public void IsValueTypeOutputParameter_Bool_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(bool)));
        }

        [MSBuildTestMethod]
        public void IsValueTypeOutputParameter_Int_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(int)));
        }

        [MSBuildTestMethod]
        public void IsValueTypeOutputParameter_AbsolutePath_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(AbsolutePath)));
        }

        [MSBuildTestMethod]
        public void IsValueTypeOutputParameter_StringArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(string[])));
        }

        [MSBuildTestMethod]
        public void IsValueTypeOutputParameter_BoolArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(bool[])));
        }

        [MSBuildTestMethod]
        public void IsValueTypeOutputParameter_IntArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(int[])));
        }

        [MSBuildTestMethod]
        public void IsValueTypeOutputParameter_AbsolutePathArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(AbsolutePath[])));
        }

        [MSBuildTestMethod]
        public void IsValueTypeOutputParameter_ITaskItem_ReturnsFalse()
        {
            Assert.IsFalse(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(ITaskItem)));
        }

        [MSBuildTestMethod]
        public void IsValueTypeOutputParameter_Object_ReturnsFalse()
        {
            Assert.IsFalse(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(object)));
        }

        #endregion

        #region IsValidInputParameter Tests

        [MSBuildTestMethod]
        public void IsValidInputParameter_String_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidInputParameter(typeof(string)));
        }

        [MSBuildTestMethod]
        public void IsValidInputParameter_ITaskItem_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidInputParameter(typeof(ITaskItem)));
        }

        [MSBuildTestMethod]
        public void IsValidInputParameter_Bool_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidInputParameter(typeof(bool)));
        }

        [MSBuildTestMethod]
        public void IsValidInputParameter_AbsolutePath_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidInputParameter(typeof(AbsolutePath)));
        }

        [MSBuildTestMethod]
        public void IsValidInputParameter_StringArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidInputParameter(typeof(string[])));
        }

        [MSBuildTestMethod]
        public void IsValidInputParameter_ITaskItemArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidInputParameter(typeof(ITaskItem[])));
        }

        [MSBuildTestMethod]
        public void IsValidInputParameter_BoolArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidInputParameter(typeof(bool[])));
        }

        [MSBuildTestMethod]
        public void IsValidInputParameter_AbsolutePathArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidInputParameter(typeof(AbsolutePath[])));
        }

        [MSBuildTestMethod]
        public void IsValidInputParameter_Object_ReturnsFalse()
        {
            Assert.IsFalse(TaskParameterTypeVerifier.IsValidInputParameter(typeof(object)));
        }

        [MSBuildTestMethod]
        public void IsValidInputParameter_ObjectArray_ReturnsFalse()
        {
            Assert.IsFalse(TaskParameterTypeVerifier.IsValidInputParameter(typeof(object[])));
        }

        #endregion

        #region IsValidOutputParameter Tests

        [MSBuildTestMethod]
        public void IsValidOutputParameter_String_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(string)));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_ITaskItem_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(ITaskItem)));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_Bool_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(bool)));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_AbsolutePath_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(AbsolutePath)));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_StringArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(string[])));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_ITaskItemArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(ITaskItem[])));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_BoolArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(bool[])));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_AbsolutePathArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(AbsolutePath[])));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_Object_ReturnsFalse()
        {
            Assert.IsFalse(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(object)));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_ObjectArray_ReturnsFalse()
        {
            Assert.IsFalse(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(object[])));
        }

        // ─── ITaskItem<T> and ITaskItem<T>[] coverage (path-like types) ────────

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_ITaskItemOfAbsolutePath_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(ITaskItem<AbsolutePath>)));
        }

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_ITaskItemOfFileInfo_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(ITaskItem<FileInfo>)));
        }

        [MSBuildTestMethod]
        public void IsValidScalarInputParameter_ITaskItemOfDirectoryInfo_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(ITaskItem<DirectoryInfo>)));
        }

        [MSBuildTestMethod]
        public void IsValidVectorInputParameter_ITaskItemOfAbsolutePathArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(ITaskItem<AbsolutePath>[])));
        }

        [MSBuildTestMethod]
        public void IsAssignableToITaskItem_ITaskItemOfAbsolutePath_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsAssignableToITaskItem(typeof(ITaskItem<AbsolutePath>)));
        }

        [MSBuildTestMethod]
        public void IsAssignableToITaskItem_ITaskItemOfAbsolutePathArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsAssignableToITaskItem(typeof(ITaskItem<AbsolutePath>[])));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_ITaskItemOfAbsolutePath_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(ITaskItem<AbsolutePath>)));
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_ITaskItemOfAbsolutePathArray_ReturnsTrue()
        {
            Assert.IsTrue(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(ITaskItem<AbsolutePath>[])));
        }

        [MSBuildTestMethod]
        public void IsAssignableToITaskItem_TaskItemOfAbsolutePathArray_ReturnsTrue()
        {
            typeof(ITaskItem[]).IsAssignableFrom(typeof(TaskItem<AbsolutePath>[])).ShouldBeFalse();
            TaskParameterTypeVerifier.IsAssignableToITaskItem(typeof(TaskItem<AbsolutePath>[])).ShouldBeTrue();
        }

        [MSBuildTestMethod]
        public void IsValidOutputParameter_TaskItemOfAbsolutePathArray_ReturnsTrue()
        {
            TaskParameterTypeVerifier.IsValidOutputParameter(typeof(TaskItem<AbsolutePath>[])).ShouldBeTrue();
        }

        #endregion
    }
}
