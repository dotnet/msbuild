// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

using Microsoft.Build;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Test Fixture Class for the v9 Object Model Public Interface Compatibility Tests for the EngineFileUtilities Class. 
    /// This is not a PRI 1 class for coverage
    /// </summary>
    [TestFixture]
    public class EngineFileUtilities_Tests
    {
        /// <summary>
        /// Test for thrown InternalErrorException when escaping a null string
        /// </summary>
        /// <remarks>found by kevinpi, Managed Lanaguages Team</remarks>
        [Test]
        public void EscapeString_Null() 
        {
            try
            {
                Microsoft.Build.BuildEngine.Utilities.Escape(null);
                Assertion.Fail(); // Should not get here.
            }
            catch (Exception e)
            {
                Assertion.AssertEquals(true, e.GetType().ToString().Contains("InternalErrorException"));
            }
        }
    }
}
