// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for TaskParameterTypeVerifier class
    /// </summary>
    public class TaskParameterTypeVerifier_Tests
    {
        #region IsValidScalarInputParameter Tests

        [Fact]
        public void IsValidScalarInputParameter_String_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(string)));
        }

        [Fact]
        public void IsValidScalarInputParameter_ITaskItem_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(ITaskItem)));
        }

        [Fact]
        public void IsValidScalarInputParameter_Bool_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(bool)));
        }

        [Fact]
        public void IsValidScalarInputParameter_Int_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(int)));
        }

        [Fact]
        public void IsValidScalarInputParameter_Double_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(double)));
        }

        [Fact]
        public void IsValidScalarInputParameter_DateTime_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(DateTime)));
        }

        [Fact]
        public void IsValidScalarInputParameter_AbsolutePath_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(AbsolutePath)));
        }

        [Fact]
        public void IsValidScalarInputParameter_Object_ReturnsFalse()
        {
            Assert.False(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(object)));
        }

        [Fact]
        public void IsValidScalarInputParameter_StringArray_ReturnsFalse()
        {
            Assert.False(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(string[])));
        }

        #endregion

        #region IsValidVectorInputParameter Tests

        [Fact]
        public void IsValidVectorInputParameter_StringArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(string[])));
        }

        [Fact]
        public void IsValidVectorInputParameter_ITaskItemArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(ITaskItem[])));
        }

        [Fact]
        public void IsValidVectorInputParameter_BoolArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(bool[])));
        }

        [Fact]
        public void IsValidVectorInputParameter_IntArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(int[])));
        }

        [Fact]
        public void IsValidVectorInputParameter_DoubleArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(double[])));
        }

        [Fact]
        public void IsValidVectorInputParameter_DateTimeArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(DateTime[])));
        }

        [Fact]
        public void IsValidVectorInputParameter_AbsolutePathArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(AbsolutePath[])));
        }

        [Fact]
        public void IsValidVectorInputParameter_String_ReturnsFalse()
        {
            Assert.False(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(string)));
        }

        [Fact]
        public void IsValidVectorInputParameter_ObjectArray_ReturnsFalse()
        {
            Assert.False(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(object[])));
        }

        #endregion

        #region IsValueTypeOutputParameter Tests

        [Fact]
        public void IsValueTypeOutputParameter_String_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(string)));
        }

        [Fact]
        public void IsValueTypeOutputParameter_Bool_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(bool)));
        }

        [Fact]
        public void IsValueTypeOutputParameter_Int_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(int)));
        }

        [Fact]
        public void IsValueTypeOutputParameter_AbsolutePath_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(AbsolutePath)));
        }

        [Fact]
        public void IsValueTypeOutputParameter_StringArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(string[])));
        }

        [Fact]
        public void IsValueTypeOutputParameter_BoolArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(bool[])));
        }

        [Fact]
        public void IsValueTypeOutputParameter_IntArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(int[])));
        }

        [Fact]
        public void IsValueTypeOutputParameter_AbsolutePathArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(AbsolutePath[])));
        }

        [Fact]
        public void IsValueTypeOutputParameter_ITaskItem_ReturnsFalse()
        {
            Assert.False(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(ITaskItem)));
        }

        [Fact]
        public void IsValueTypeOutputParameter_Object_ReturnsFalse()
        {
            Assert.False(TaskParameterTypeVerifier.IsValueTypeOutputParameter(typeof(object)));
        }

        #endregion

        #region IsValidInputParameter Tests

        [Fact]
        public void IsValidInputParameter_String_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidInputParameter(typeof(string)));
        }

        [Fact]
        public void IsValidInputParameter_ITaskItem_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidInputParameter(typeof(ITaskItem)));
        }

        [Fact]
        public void IsValidInputParameter_Bool_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidInputParameter(typeof(bool)));
        }

        [Fact]
        public void IsValidInputParameter_AbsolutePath_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidInputParameter(typeof(AbsolutePath)));
        }

        [Fact]
        public void IsValidInputParameter_StringArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidInputParameter(typeof(string[])));
        }

        [Fact]
        public void IsValidInputParameter_ITaskItemArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidInputParameter(typeof(ITaskItem[])));
        }

        [Fact]
        public void IsValidInputParameter_BoolArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidInputParameter(typeof(bool[])));
        }

        [Fact]
        public void IsValidInputParameter_AbsolutePathArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidInputParameter(typeof(AbsolutePath[])));
        }

        [Fact]
        public void IsValidInputParameter_Object_ReturnsFalse()
        {
            Assert.False(TaskParameterTypeVerifier.IsValidInputParameter(typeof(object)));
        }

        [Fact]
        public void IsValidInputParameter_ObjectArray_ReturnsFalse()
        {
            Assert.False(TaskParameterTypeVerifier.IsValidInputParameter(typeof(object[])));
        }

        #endregion

        #region IsValidOutputParameter Tests

        [Fact]
        public void IsValidOutputParameter_String_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(string)));
        }

        [Fact]
        public void IsValidOutputParameter_ITaskItem_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(ITaskItem)));
        }

        [Fact]
        public void IsValidOutputParameter_Bool_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(bool)));
        }

        [Fact]
        public void IsValidOutputParameter_AbsolutePath_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(AbsolutePath)));
        }

        [Fact]
        public void IsValidOutputParameter_StringArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(string[])));
        }

        [Fact]
        public void IsValidOutputParameter_ITaskItemArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(ITaskItem[])));
        }

        [Fact]
        public void IsValidOutputParameter_BoolArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(bool[])));
        }

        [Fact]
        public void IsValidOutputParameter_AbsolutePathArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(AbsolutePath[])));
        }

        [Fact]
        public void IsValidOutputParameter_Object_ReturnsFalse()
        {
            Assert.False(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(object)));
        }

        [Fact]
        public void IsValidOutputParameter_ObjectArray_ReturnsFalse()
        {
            Assert.False(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(object[])));
        }

        // ─── ITaskItem<T> and ITaskItem<T>[] coverage (path-like types) ────────

        [Fact]
        public void IsValidScalarInputParameter_ITaskItemOfAbsolutePath_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(ITaskItem<AbsolutePath>)));
        }

        [Fact]
        public void IsValidScalarInputParameter_ITaskItemOfFileInfo_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(ITaskItem<FileInfo>)));
        }

        [Fact]
        public void IsValidScalarInputParameter_ITaskItemOfDirectoryInfo_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(ITaskItem<DirectoryInfo>)));
        }

        [Fact]
        public void IsValidVectorInputParameter_ITaskItemOfAbsolutePathArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(ITaskItem<AbsolutePath>[])));
        }

        [Fact]
        public void IsAssignableToITaskItem_ITaskItemOfAbsolutePath_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsAssignableToITaskItem(typeof(ITaskItem<AbsolutePath>)));
        }

        [Fact]
        public void IsAssignableToITaskItem_ITaskItemOfAbsolutePathArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsAssignableToITaskItem(typeof(ITaskItem<AbsolutePath>[])));
        }

        [Fact]
        public void IsValidOutputParameter_ITaskItemOfAbsolutePath_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(ITaskItem<AbsolutePath>)));
        }

        [Fact]
        public void IsValidOutputParameter_ITaskItemOfAbsolutePathArray_ReturnsTrue()
        {
            Assert.True(TaskParameterTypeVerifier.IsValidOutputParameter(typeof(ITaskItem<AbsolutePath>[])));
        }

        [Fact]
        public void IsAssignableToITaskItem_TaskItemOfAbsolutePathArray_ReturnsTrue()
        {
            typeof(ITaskItem[]).IsAssignableFrom(typeof(TaskItem<AbsolutePath>[])).ShouldBeFalse();
            TaskParameterTypeVerifier.IsAssignableToITaskItem(typeof(TaskItem<AbsolutePath>[])).ShouldBeTrue();
        }

        [Fact]
        public void IsValidOutputParameter_TaskItemOfAbsolutePathArray_ReturnsTrue()
        {
            TaskParameterTypeVerifier.IsValidOutputParameter(typeof(TaskItem<AbsolutePath>[])).ShouldBeTrue();
        }

        // ─── Concrete Microsoft.Build.Framework.TaskItem<T> is rejected as an INPUT (authors must use ITaskItem<T>) ───

        [Fact]
        public void IsValidScalarInputParameter_ConcreteUtilitiesTaskItemOfT_ReturnsFalse()
        {
            // The concrete public TaskItem<T> is a struct, so it would otherwise pass via the value-type branch.
            // The engine can only construct its own ITaskItem<T> implementation, so the concrete type must be rejected
            // as an input (it falls through to the "UnsupportedTaskParameterTypeError" diagnostic).
            TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(TaskItem<FileInfo>)).ShouldBeFalse();
            TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(TaskItem<DirectoryInfo>)).ShouldBeFalse();
            TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(TaskItem<AbsolutePath>)).ShouldBeFalse();
        }

        [Fact]
        public void IsValidVectorInputParameter_ConcreteUtilitiesTaskItemOfTArray_ReturnsFalse()
        {
            TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(TaskItem<FileInfo>[])).ShouldBeFalse();
            TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(TaskItem<AbsolutePath>[])).ShouldBeFalse();
        }

        [Fact]
        public void IsValidInputParameter_InterfaceITaskItemOfT_StillValid()
        {
            // Regression guard: rejecting the concrete type must not affect the supported interface form.
            TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(ITaskItem<FileInfo>)).ShouldBeTrue();
            TaskParameterTypeVerifier.IsValidVectorInputParameter(typeof(ITaskItem<FileInfo>[])).ShouldBeTrue();
        }

        [Fact]
        public void IsValidScalarInputParameter_PlainValueType_StillValid()
        {
            // The concrete-type exclusion must not affect ordinary value-type parameters.
            TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(int)).ShouldBeTrue();
            TaskParameterTypeVerifier.IsValidScalarInputParameter(typeof(bool)).ShouldBeTrue();
        }

        #endregion
    }
}
