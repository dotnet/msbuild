// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class ResourceUtilitiesTests
    {
        [TestMethod]
        public void ExtractMSBuildCode()
        {
            // most common message pattern
            string code;
            string messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB7007: This is a message.", out code);
            Assert.AreEqual("MSB7007", code);
            Assert.AreEqual("This is a message.", messageOnly);

            // no whitespace between colon and message is ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB7007:This is a message.", out code);
            Assert.AreEqual("MSB7007", code);
            Assert.AreEqual("This is a message.", messageOnly);

            // whitespace before code and after colon is ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "  MSB7007:   This is a message.", out code);
            Assert.AreEqual("MSB7007", code);
            Assert.AreEqual("This is a message.", messageOnly);

            // whitespace between code and colon is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB7007 : This is a message.", out code);
            Assert.IsNull(code);
            Assert.AreEqual("MSB7007 : This is a message.", messageOnly);

            // whitespace in code is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB 7007: This is a message.", out code);
            Assert.IsNull(code);
            Assert.AreEqual("MSB 7007: This is a message.", messageOnly);

            // code with less than 4 digits is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB007: This is a message.", out code);
            Assert.IsNull(code);
            Assert.AreEqual("MSB007: This is a message.", messageOnly);

            // code without digits is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB: This is a message.", out code);
            Assert.IsNull(code);
            Assert.AreEqual("MSB: This is a message.", messageOnly);

            // code without MSB prefix is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "1001: This is a message.", out code);
            Assert.IsNull(code);
            Assert.AreEqual("1001: This is a message.", messageOnly);

            // digits before MSB prefix is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "7001MSB: This is a message.", out code);
            Assert.IsNull(code);
            Assert.AreEqual("7001MSB: This is a message.", messageOnly);

            // mixing letters and digits is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB564B: This is a message.", out code);
            Assert.IsNull(code);
            Assert.AreEqual("MSB564B: This is a message.", messageOnly);

            // lowercase code is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "msb1001: This is a message.", out code);
            Assert.IsNull(code);
            Assert.AreEqual("msb1001: This is a message.", messageOnly);
        }
    }
}
