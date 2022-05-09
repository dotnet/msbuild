// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestNamespace
{
    [TestClass]
    public class MyTestClass
    {
        [TestMethod]
        [TestCategory("CategoryA,CategoryB")]
        public void MyTestMethod()
        {
        }
    }
}
