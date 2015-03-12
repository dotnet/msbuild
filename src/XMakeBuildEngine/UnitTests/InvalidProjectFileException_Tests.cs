// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Exceptions;
using System.Text.RegularExpressions;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class InvalidProjectFileExceptionTests
    {
        /// <summary>
        /// Verify I implemented ISerializable correctly
        /// </summary>
        [TestMethod]
        public void SerializeDeserialize()
        {
            InvalidProjectFileException e = new InvalidProjectFileException(
                "projectFile",
                1, 2, 3, 4,
                "message",
                "errorSubcategory",
                "errorCode",
                "helpKeyword");

            using (MemoryStream memstr = new MemoryStream())
            {
                BinaryFormatter frm = new BinaryFormatter();

                frm.Serialize(memstr, e);
                memstr.Position = 0;

                InvalidProjectFileException e2 = (InvalidProjectFileException)frm.Deserialize(memstr);

                Assert.AreEqual(e.ColumnNumber, e2.ColumnNumber);
                Assert.AreEqual(e.EndColumnNumber, e2.EndColumnNumber);
                Assert.AreEqual(e.EndLineNumber, e2.EndLineNumber);
                Assert.AreEqual(e.ErrorCode, e2.ErrorCode);
                Assert.AreEqual(e.ErrorSubcategory, e2.ErrorSubcategory);
                Assert.AreEqual(e.HasBeenLogged, e2.HasBeenLogged);
                Assert.AreEqual(e.HelpKeyword, e2.HelpKeyword);
                Assert.AreEqual(e.LineNumber, e2.LineNumber);
                Assert.AreEqual(e.Message, e2.Message);
                Assert.AreEqual(e.ProjectFile, e2.ProjectFile);
            }
        }

        /// <summary>
        /// Verify that nesting an IPFE copies the error code
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Changes to the current directory interfere with the toolset reader.
        public void ErrorCodeShouldAppearForCircularDependency()
        {
            string file = Path.GetTempPath() + Guid.NewGuid().ToString("N");

            try
            {
                File.WriteAllText(file, ObjectModelHelpers.CleanupFileContents(@"
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                        <Target Name='Build'>
                            <CallTarget Targets='Build'/>
                        </Target>
                    </Project>
                "));

                MockLogger ml = ObjectModelHelpers.BuildTempProjectFileExpectFailure(file);

                // Make sure the log contains the error code and file/line/col for the circular dependency
                ml.AssertLogContains("MSB4006");
                ml.AssertLogContains("(4,29)");
                ml.AssertLogContains(file);
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
