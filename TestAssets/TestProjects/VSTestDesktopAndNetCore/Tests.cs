// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestNamespace
{
    [TestClass]
    public class VSTestTests
    {
        [TestMethod]
        public void VSTestPassTest()
        {
        }

        [TestMethod]
        public void VSTestFailTest()
        {
            Assert.Fail();
        }

#if DESKTOP
        [TestMethod]
        public void VSTestPassTestDesktop()
        {
        }
#else
        [TestMethod]
        public void VSTestFailTestNetCoreApp()
        {
            Assert.Fail();
        }
#endif
    }
}
