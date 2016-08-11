// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ResourceUtilitiesTests
    {
        [Test]
        public void ExtractMSBuildCode()
        {
            // most common message pattern
            string code;
            string messageOnly = ResourceUtilities.ExtractMessageCode(null, "MSB7007: This is a message.", out code);
            Assertion.AssertEquals("MSB7007", code);
            Assertion.AssertEquals("This is a message.", messageOnly);

            // no whitespace between colon and message is ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(null, "MSB7007:This is a message.", out code);
            Assertion.AssertEquals("MSB7007", code);
            Assertion.AssertEquals("This is a message.", messageOnly);

            // whitespace before code and after colon is ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(null, "  MSB7007:   This is a message.", out code);
            Assertion.AssertEquals("MSB7007", code);
            Assertion.AssertEquals("This is a message.", messageOnly);

            // whitespace between code and colon is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(null, "MSB7007 : This is a message.", out code);
            Assertion.AssertNull(code);
            Assertion.AssertEquals("MSB7007 : This is a message.", messageOnly);

            // whitespace in code is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(null, "MSB 7007: This is a message.", out code);
            Assertion.AssertNull(code);
            Assertion.AssertEquals("MSB 7007: This is a message.", messageOnly);

            // code with less than 4 digits is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(null, "MSB007: This is a message.", out code);
            Assertion.AssertNull(code);
            Assertion.AssertEquals("MSB007: This is a message.", messageOnly);

            // code without digits is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(null, "MSB: This is a message.", out code);
            Assertion.AssertNull(code);
            Assertion.AssertEquals("MSB: This is a message.", messageOnly);

            // code without MSB prefix is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(null, "1001: This is a message.", out code);
            Assertion.AssertNull(code);
            Assertion.AssertEquals("1001: This is a message.", messageOnly);

            // digits before MSB prefix is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(null, "7001MSB: This is a message.", out code);
            Assertion.AssertNull(code);
            Assertion.AssertEquals("7001MSB: This is a message.", messageOnly);

            // mixing letters and digits is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(null, "MSB564B: This is a message.", out code);
            Assertion.AssertNull(code);
            Assertion.AssertEquals("MSB564B: This is a message.", messageOnly);

            // lowercase code is not ok
            code = null;
            messageOnly = ResourceUtilities.ExtractMessageCode(null, "msb1001: This is a message.", out code);
            Assertion.AssertNull(code);
            Assertion.AssertEquals("msb1001: This is a message.", messageOnly);
        }
    }
}
