// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
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

        #endregion
    }
}
