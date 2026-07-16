// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
#if NETFRAMEWORK
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Shouldly;
#endif
using Microsoft.Build.Evaluation;
using Microsoft.Build.Tasks;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   AspNetCompilerTests
     *
     * Test the AspNetCompiler task in various ways.
     *
     */
    [TestClass]
    public sealed class AspNetCompilerTests
    {
        private readonly TestContext _output;

        public AspNetCompilerTests(TestContext output)
        {
            _output = output;
        }

        [MSBuildTestMethod]
        public void NoParameters()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            // It's invalid to have zero parameters, so we expect a "false" return value from ValidateParameters.
            Assert.IsFalse(CommandLine.CallValidateParameters(t));
        }

        [MSBuildTestMethod]
        public void OnlyMetabasePath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.MetabasePath = @"/LM/W3SVC/1/Root/MyApp";

            // This should be valid.
            Assert.IsTrue(CommandLine.CallValidateParameters(t));

            CommandLine.ValidateEquals(t, @"-m /LM/W3SVC/1/Root/MyApp", false);
        }

        [MSBuildTestMethod]
        public void OnlyVirtualPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.VirtualPath = @"/MyApp";

            // This should be valid.
            Assert.IsTrue(CommandLine.CallValidateParameters(t));

            CommandLine.ValidateEquals(t, @"-v /MyApp", false);
        }


        [MSBuildTestMethod]
        public void OnlyPhysicalPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.PhysicalPath = @"c:\MyApp";

            // This is not valid.  Either MetabasePath or VirtualPath must be specified.
            Assert.IsFalse(CommandLine.CallValidateParameters(t));
        }

        [MSBuildTestMethod]
        public void OnlyTargetPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.TargetPath = @"c:\MyTarget";

            // This is not valid.  Either MetabasePath or VirtualPath must be specified.
            Assert.IsFalse(CommandLine.CallValidateParameters(t));
        }

        [MSBuildTestMethod]
        public void MetabasePathAndVirtualPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.MetabasePath = @"/LM/W3SVC/1/Root/MyApp";
            t.VirtualPath = @"/MyApp";

            // This is not valid.  Can't specify both MetabasePath and (VirtualPath or PhysicalPath).
            Assert.IsFalse(CommandLine.CallValidateParameters(t));
        }

        [MSBuildTestMethod]
        public void MetabasePathAndPhysicalPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.MetabasePath = @"/LM/W3SVC/1/Root/MyApp";
            t.PhysicalPath = @"c:\MyApp";

            // This is not valid.  Can't specify both MetabasePath and (VirtualPath or PhysicalPath).
            Assert.IsFalse(CommandLine.CallValidateParameters(t));
        }

        [MSBuildTestMethod]
        public void MetabasePathAndTargetPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.MetabasePath = @"/LM/W3SVC/1/Root/MyApp";
            t.TargetPath = @"c:\MyTarget";

            // This is valid.
            Assert.IsTrue(CommandLine.CallValidateParameters(t));

            CommandLine.ValidateEquals(t, @"-m /LM/W3SVC/1/Root/MyApp c:\MyTarget", false);
        }

        [MSBuildTestMethod]
        public void VirtualPathAndPhysicalPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.VirtualPath = @"/MyApp";
            t.PhysicalPath = @"c:\MyApp";

            // This is valid.
            Assert.IsTrue(CommandLine.CallValidateParameters(t));

            CommandLine.ValidateEquals(t, @"-v /MyApp -p c:\MyApp", false);
        }

        [MSBuildTestMethod]
        public void VirtualPathAndTargetPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.VirtualPath = @"/MyApp";
            t.TargetPath = @"c:\MyTarget";

            // This is valid.
            Assert.IsTrue(CommandLine.CallValidateParameters(t));

            CommandLine.ValidateEquals(t, @"-v /MyApp c:\MyTarget", false);
        }

        [MSBuildTestMethod]
        public void PhysicalPathAndTargetPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.PhysicalPath = @"c:\MyApp";
            t.TargetPath = @"c:\MyTarget";

            // This is not valid.  Either MetabasePath or VirtualPath must be specified.
            Assert.IsFalse(CommandLine.CallValidateParameters(t));
        }

        [MSBuildTestMethod]
        public void AllExceptMetabasePath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.VirtualPath = @"/MyApp";
            t.PhysicalPath = @"c:\MyApp";
            t.TargetPath = @"c:\MyTarget";

            // This is valid.
            Assert.IsTrue(CommandLine.CallValidateParameters(t));

            CommandLine.ValidateEquals(t, @"-v /MyApp -p c:\MyApp c:\MyTarget", false);
        }

        [MSBuildTestMethod]
        public void AllExceptVirtualPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.MetabasePath = @"/LM/W3SVC/1/Root/MyApp";
            t.PhysicalPath = @"c:\MyApp";
            t.TargetPath = @"c:\MyTarget";

            // This is not valid.  Can't specify both MetabasePath and (VirtualPath or PhysicalPath).
            Assert.IsFalse(CommandLine.CallValidateParameters(t));
        }

        [MSBuildTestMethod]
        public void AllExceptPhysicalPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.MetabasePath = @"/LM/W3SVC/1/Root/MyApp";
            t.VirtualPath = @"/MyApp";
            t.TargetPath = @"c:\MyTarget";

            // This is not valid.  Can't specify both MetabasePath and (VirtualPath or PhysicalPath).
            Assert.IsFalse(CommandLine.CallValidateParameters(t));
        }

        [MSBuildTestMethod]
        public void AllExceptTargetPath()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.MetabasePath = @"/LM/W3SVC/1/Root/MyApp";
            t.VirtualPath = @"/MyApp";
            t.PhysicalPath = @"c:\MyApp";

            // This is not valid.  Can't specify both MetabasePath and (VirtualPath or PhysicalPath).
            Assert.IsFalse(CommandLine.CallValidateParameters(t));
        }

        [MSBuildTestMethod]
        public void AllParameters()
        {
            AspNetCompiler t = new AspNetCompiler();
            t.BuildEngine = new MockEngine(_output);

            t.MetabasePath = @"/LM/W3SVC/1/Root/MyApp";
            t.VirtualPath = @"/MyApp";
            t.PhysicalPath = @"c:\MyApp";
            t.TargetPath = @"c:\MyTarget";

            // This is not valid.  Can't specify both MetabasePath and (VirtualPath or PhysicalPath).
            Assert.IsFalse(CommandLine.CallValidateParameters(t));
        }

        /// <summary>
        /// Make sure AspNetCompiler sends ExternalProjectStarted/Finished events properly. The tasks will fail since
        /// the project files don't exist, but we only care about the events anyway.
        /// </summary>
        [MSBuildTestMethod]
        public void TestExternalProjectEvents()
        {
            string projectFileContents = @"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <UsingTask TaskName=`AspNetCompiler` AssemblyName=`{0}`/>

                        <Target Name=`Build`>
                            <AspNetCompiler VirtualPath=`/WebSite1` PhysicalPath=`..\..\solutions\WebSite1\`/>
                            <OnError ExecuteTargets=`Build2`/>
                        </Target>
                        <Target Name=`Build2`>
                            <AspNetCompiler VirtualPath=`/WebSite2` Clean=`true`/>
                            <OnError ExecuteTargets=`Build3`/>
                        </Target>
                        <Target Name=`Build3`>
                            <AspNetCompiler MetabasePath=`/LM/W3SVC/1/Root/MyApp`/>
                        </Target>
                    </Project>";

            string fullProjectFile = string.Format(CultureInfo.InvariantCulture, projectFileContents, typeof(AspNetCompiler).Assembly.FullName);

            MockLogger logger = new MockLogger(_output);
            Project proj = ObjectModelHelpers.CreateInMemoryProject(fullProjectFile, logger);
            Assert.IsFalse(proj.Build(logger));

            Assert.AreEqual(3, logger.ExternalProjectStartedEvents.Count);
            Assert.AreEqual(3, logger.ExternalProjectFinishedEvents.Count);

            Assert.AreEqual(@"..\..\solutions\WebSite1\", logger.ExternalProjectStartedEvents[0].ProjectFile);
            Assert.AreEqual("/WebSite2", logger.ExternalProjectStartedEvents[1].ProjectFile);
            Assert.AreEqual("/LM/W3SVC/1/Root/MyApp", logger.ExternalProjectStartedEvents[2].ProjectFile);

            Assert.AreEqual(@"..\..\solutions\WebSite1\", logger.ExternalProjectFinishedEvents[0].ProjectFile);
            Assert.AreEqual("/WebSite2", logger.ExternalProjectFinishedEvents[1].ProjectFile);
            Assert.AreEqual("/LM/W3SVC/1/Root/MyApp", logger.ExternalProjectFinishedEvents[2].ProjectFile);

            Assert.IsNull(logger.ExternalProjectStartedEvents[0].TargetNames);
            Assert.AreEqual("Clean", logger.ExternalProjectStartedEvents[1].TargetNames);
            Assert.IsNull(logger.ExternalProjectStartedEvents[2].TargetNames);

            Assert.IsFalse(logger.ExternalProjectFinishedEvents[0].Succeeded);
            Assert.IsFalse(logger.ExternalProjectFinishedEvents[1].Succeeded);
            Assert.IsFalse(logger.ExternalProjectFinishedEvents[2].Succeeded);
        }

#if NETFRAMEWORK
        /// <summary>
        /// Verifies that GenerateFullPathToTool returns an absolute path (or null)
        /// when called with a multithreaded TaskEnvironment, validating the
        /// TaskEnvironment.GetAbsolutePath() integration.
        /// </summary>
        [MSBuildTestMethod]
        public void GenerateFullPathToTool_ReturnsAbsolutePathOrNull()
        {
            string projectDir = Path.GetTempPath();
            TaskEnvironment taskEnv = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);

            TestableAspNetCompiler t = new TestableAspNetCompiler();
            t.TaskEnvironment = taskEnv;
            t.BuildEngine = new MockEngine(_output);

            string result = t.CallGenerateFullPathToTool();

            if (result is not null)
            {
                Path.IsPathRooted(result).ShouldBeTrue(
                    $"GenerateFullPathToTool should return an absolute path, got: {result}");
            }
        }

        /// <summary>
        /// Verifies that the GetProcessStartInfo override routes through
        /// GetProcessStartInfoMultiThreaded when TaskEnvironment is set,
        /// and that the working directory comes from the TaskEnvironment.
        /// </summary>
        [MSBuildTestMethod]
        public void GetProcessStartInfo_UsesTaskEnvironmentWorkingDirectory()
        {
            string expectedWorkingDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            TaskEnvironment taskEnv = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(expectedWorkingDir);

            TestableAspNetCompiler t = new TestableAspNetCompiler();
            t.TaskEnvironment = taskEnv;
            t.BuildEngine = new MockEngine(_output);

            ProcessStartInfo startInfo = t.CallGetProcessStartInfo(@"C:\test\aspnet_compiler.exe", "-v /MyApp", null);

            startInfo.WorkingDirectory.ShouldBe(expectedWorkingDir);
        }

        /// <summary>
        /// Subclass that exposes protected methods.
        /// </summary>
        private sealed class TestableAspNetCompiler : AspNetCompiler
        {
            public string CallGenerateFullPathToTool() => GenerateFullPathToTool();

            public ProcessStartInfo CallGetProcessStartInfo(string pathToTool, string commandLineCommands, string responseFileSwitch)
                => GetProcessStartInfo(pathToTool, commandLineCommands, responseFileSwitch);
        }
#endif
    }
}
