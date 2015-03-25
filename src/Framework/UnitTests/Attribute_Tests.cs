// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;

using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class AttributeTests
    {
        /// <summary>
        /// Test RequiredRuntimeAttribute
        /// </summary>
        [Test]
        public void RequiredRuntimeAttribute()
        {
            RequiredRuntimeAttribute attribute =
                (RequiredRuntimeAttribute)Attribute.GetCustomAttribute(typeof(X), typeof(RequiredRuntimeAttribute));

            Assert.AreEqual("v5", attribute.RuntimeVersion);
        }

        [Test]
        public void OutputAttribute()
        {
            OutputAttribute attribute =
                (OutputAttribute)Attribute.GetCustomAttribute(typeof(X).GetMember("TestValue2", BindingFlags.NonPublic | BindingFlags.Static)[0], typeof(OutputAttribute));
            Assert.IsNotNull(attribute);
        }

        [Test]
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






