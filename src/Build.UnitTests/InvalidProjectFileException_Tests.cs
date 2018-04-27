// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Exceptions;

using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class InvalidProjectFileExceptionTests
    {
        /// <summary>
        /// Verify I implemented ISerializable correctly
        /// </summary>
        [Fact]
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

                Assert.Equal(e.ColumnNumber, e2.ColumnNumber);
                Assert.Equal(e.EndColumnNumber, e2.EndColumnNumber);
                Assert.Equal(e.EndLineNumber, e2.EndLineNumber);
                Assert.Equal(e.ErrorCode, e2.ErrorCode);
                Assert.Equal(e.ErrorSubcategory, e2.ErrorSubcategory);
                Assert.Equal(e.HasBeenLogged, e2.HasBeenLogged);
                Assert.Equal(e.HelpKeyword, e2.HelpKeyword);
                Assert.Equal(e.LineNumber, e2.LineNumber);
                Assert.Equal(e.Message, e2.Message);
                Assert.Equal(e.ProjectFile, e2.ProjectFile);
            }
        }

        /// <summary>
        /// Verify that nesting an IPFE copies the error code
        /// </summary>
        [Fact]
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

        /// <summary>
        /// Regression test for https://github.com/Microsoft/msbuild/issues/1286
        /// </summary>
        [Fact]
        public void LogErrorShouldHavePathAndLocation()
        {
            string file = Path.GetTempPath() + Guid.NewGuid().ToString("N");

            try
            {
                File.WriteAllText(file, ObjectModelHelpers.CleanupFileContents(@"
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                        <Target Name=[invalid] />
                    </Project>"));

                var _ = ObjectModelHelpers.BuildTempProjectFileExpectFailure(file);

                Assert.True(false, "Loading an invalid project should have thrown an InvalidProjectFileException.");
            }
            catch (InvalidProjectFileException e)
            {
                Assert.Equal(3, e.LineNumber);
                Assert.Equal(38, e.ColumnNumber);
                Assert.Equal(file, e.ProjectFile); // https://github.com/Microsoft/msbuild/issues/1286
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
