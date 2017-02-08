// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;

using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class AttributeTests
    {
        /// <summary>
        /// Test RequiredRuntimeAttribute
        /// </summary>
        [Fact]
        public void RequiredRuntimeAttribute()
        {

            RequiredRuntimeAttribute attribute =
                typeof(X).GetTypeInfo().GetCustomAttribute<RequiredRuntimeAttribute>();

            Assert.Equal("v5", attribute.RuntimeVersion);
        }

        [Fact]
        public void OutputAttribute()
        {
            OutputAttribute attribute =
                typeof(X).GetMember("TestValue2", BindingFlags.NonPublic | BindingFlags.Static)[0].GetCustomAttribute<OutputAttribute>();
            Assert.NotNull(attribute);
        }

        [Fact]
        public void RequiredAttribute()
        {
            RequiredAttribute attribute =
                typeof(X).GetMember("TestValue", BindingFlags.NonPublic | BindingFlags.Static)[0].GetCustomAttribute<RequiredAttribute>();
            Assert.NotNull(attribute);
        }
    }

    /// <summary>
    /// Sample class with RequiredRuntimeAttribute on it
    /// </summary>
    [RequiredRuntime("v5")]
    internal static class X
    {
        [Required]
        internal static bool TestValue
        {
            get
            {
                return true;
            }
        }

        [Output]
        internal static bool TestValue2
        {
            get
            {
                return true;
            }
        }
    }
}






