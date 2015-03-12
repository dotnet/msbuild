// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class AttributeTests
    {
        /// <summary>
        /// Test RequiredRuntimeAttribute
        /// </summary>
        [TestMethod]
        public void RequiredRuntimeAttribute()
        {
            RequiredRuntimeAttribute attribute =
                (RequiredRuntimeAttribute)Attribute.GetCustomAttribute(typeof(X), typeof(RequiredRuntimeAttribute));

            Assert.AreEqual("v5", attribute.RuntimeVersion);
        }

        [TestMethod]
        public void OutputAttribute()
        {
            OutputAttribute attribute =
                (OutputAttribute)Attribute.GetCustomAttribute(typeof(X).GetMember("TestValue2", BindingFlags.NonPublic | BindingFlags.Static)[0], typeof(OutputAttribute));
            Assert.IsNotNull(attribute);
        }

        [TestMethod]
        public void RequiredAttribute()
        {
            RequiredAttribute attribute =
                (RequiredAttribute)Attribute.GetCustomAttribute(typeof(X).GetMember("TestValue", BindingFlags.NonPublic | BindingFlags.Static)[0], typeof(RequiredAttribute));
            Assert.IsNotNull(attribute);
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






