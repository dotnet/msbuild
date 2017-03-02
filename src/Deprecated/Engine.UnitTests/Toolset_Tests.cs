// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using System.Xml;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class Toolset_Tests
    {
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToolsetCtorErrors1()
        {
            Toolset t = new Toolset(null, "x");
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToolsetCtorErrors2()
        {
            Toolset t = new Toolset("x", null);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ToolsetCtorErrors3()
        {
            Toolset t = new Toolset(String.Empty, "x");
        }

        [Test]
        public void Regress27993_TrailingSlashTrimmedFromMSBuildToolsPath()
        {
            Toolset t;

            t = new Toolset("x", "C:");
            Assertion.AssertEquals(@"C:", t.ToolsPath);
            t = new Toolset("x", @"C:\");
            Assertion.AssertEquals(@"C:\", t.ToolsPath);
            t = new Toolset("x", @"C:\\");
            Assertion.AssertEquals(@"C:\", t.ToolsPath);

            t = new Toolset("x", @"C:\foo");
            Assertion.AssertEquals(@"C:\foo", t.ToolsPath);
            t = new Toolset("x", @"C:\foo\");
            Assertion.AssertEquals(@"C:\foo", t.ToolsPath);
            t = new Toolset("x", @"C:\foo\\");
            Assertion.AssertEquals(@"C:\foo\", t.ToolsPath); // trim at most one slash

            t = new Toolset("x", @"\\foo\share");
            Assertion.AssertEquals(@"\\foo\share", t.ToolsPath);
            t = new Toolset("x", @"\\foo\share\");
            Assertion.AssertEquals(@"\\foo\share", t.ToolsPath);
            t = new Toolset("x", @"\\foo\share\\");
            Assertion.AssertEquals(@"\\foo\share\", t.ToolsPath); // trim at most one slash
        }
    }
 }
