// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

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
                (RequiredRuntimeAttribute)Attribute.GetCustomAttribute(typeof(X), typeof(RequiredRuntimeAttribute));

            Assert.Equal("v5", attribute.RuntimeVersion);
        }

        [Fact]
        public void OutputAttribute()
        {
            OutputAttribute attribute =
                (OutputAttribute)Attribute.GetCustomAttribute(typeof(X).GetMember("TestValue2", BindingFlags.NonPublic | BindingFlags.Static)[0], typeof(OutputAttribute));
            Assert.NotNull(attribute);
        }

        [Fact]
        public void RequiredAttribute()
        {
            RequiredAttribute attribute =
                (RequiredAttribute)Attribute.GetCustomAttribute(typeof(X).GetMember("TestValue", BindingFlags.NonPublic | BindingFlags.Static)[0], typeof(RequiredAttribute));
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






