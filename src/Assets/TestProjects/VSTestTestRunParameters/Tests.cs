// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestNamespace
{
    [TestClass]
    public class VSTestTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void VSTestTestRunParameters()
        {
            var myParam = TestContext.Properties["myParam"];
            Assert.AreEqual("value", myParam);

            var myParam2 = TestContext.Properties["myParam2"];
            Assert.AreEqual("value with space", myParam2);
        }
    }
}
