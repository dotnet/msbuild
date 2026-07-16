// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Exceptions;


#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class InvalidProjectFileExceptionTests
    {
        private readonly TestContext _testOutput;

        public InvalidProjectFileExceptionTests(TestContext output)
        {
            _testOutput = output;
        }

        /// <summary>
        /// Verify that nesting an IPFE copies the error code
        /// </summary>
        [MSBuildTestMethod]
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

                MockLogger ml = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileExpectFailure(file, ml);

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
        /// Regression test for https://github.com/dotnet/msbuild/issues/1286
        /// </summary>
        [MSBuildTestMethod]
        public void LogErrorShouldHavePathAndLocation()
        {
            string file = Path.GetTempPath() + Guid.NewGuid().ToString("N");

            try
            {
                File.WriteAllText(file, ObjectModelHelpers.CleanupFileContents(@"
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                        <Target Name=[invalid] />
                    </Project>"));

                MockLogger logger = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileExpectFailure(file, logger);

                Assert.Fail("Loading an invalid project should have thrown an InvalidProjectFileException.");
            }
            catch (InvalidProjectFileException e)
            {
                Assert.AreEqual(3, e.LineNumber);
                Assert.AreEqual(38, e.ColumnNumber);
                Assert.AreEqual(file, e.ProjectFile); // https://github.com/dotnet/msbuild/issues/1286
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
