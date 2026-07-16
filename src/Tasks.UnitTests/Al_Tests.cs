// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   AlTests
     *
     * Test the AL task in various ways.
     *
     */
    [TestClass]
    public sealed class AlTests
    {
        private readonly TestContext _output;

        public AlTests(TestContext output)
        {
            _output = output;
        }
        /// <summary>
        /// Tests the AlgorithmId parameter
        /// </summary>
        [MSBuildTestMethod]
        public void AlgorithmId()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.AlgorithmId); // "Default value"
            t.AlgorithmId = "whatisthis";
            Assert.AreEqual("whatisthis", t.AlgorithmId); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/algid:whatisthis");
        }

        /// <summary>
        /// Tests the BaseAddress parameter
        /// </summary>
        [MSBuildTestMethod]
        public void BaseAddress()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.BaseAddress); // "Default value"
            t.BaseAddress = "12345678";
            Assert.AreEqual("12345678", t.BaseAddress); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/baseaddress:12345678");
        }

        /// <summary>
        /// Tests the CompanyName parameter
        /// </summary>
        [MSBuildTestMethod]
        public void CompanyName()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.CompanyName); // "Default value"
            t.CompanyName = "Google";
            Assert.AreEqual("Google", t.CompanyName); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/company:Google");
        }

        /// <summary>
        /// Tests the Configuration parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Configuration()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Configuration); // "Default value"
            t.Configuration = "debug";
            Assert.AreEqual("debug", t.Configuration); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/configuration:debug");
        }

        /// <summary>
        /// Tests the Copyright parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Copyright()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Copyright); // "Default value"
            t.Copyright = "(C) 2005";
            Assert.AreEqual("(C) 2005", t.Copyright); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/copyright:(C) 2005");
        }

        /// <summary>
        /// Tests the Culture parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Culture()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Culture); // "Default value"
            t.Culture = "aussie";
            Assert.AreEqual("aussie", t.Culture); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/culture:aussie");
        }

        /// <summary>
        /// Tests the DelaySign parameter.
        /// </summary>
        [MSBuildTestMethod]
        public void DelaySign()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsFalse(t.DelaySign); // "Default value"
            t.DelaySign = true;
            Assert.IsTrue(t.DelaySign); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, "/delaysign+");
        }

        /// <summary>
        /// Tests the Description parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Description()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Description); // "Default value"
            t.Description = "whatever";
            Assert.AreEqual("whatever", t.Description); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/description:whatever");
        }

        /// <summary>
        /// Tests the EmbedResources parameter with an item that has metadata LogicalName and Access=private
        /// </summary>
        [MSBuildTestMethod]
        public void EmbedResourcesWithPrivateAccess()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.EmbedResources); // "Default value"

            // Construct the task item.
            TaskItem i = new TaskItem();
            i.ItemSpec = "MyResource.bmp";
            i.SetMetadata("LogicalName", "Kenny");
            i.SetMetadata("Access", "Private");
            t.EmbedResources = new ITaskItem[] { i };

            Assert.ContainsSingle(t.EmbedResources); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(
                t,
                "/embed:MyResource.bmp,Kenny,Private");
        }

        /// <summary>
        /// Tests the EvidenceFile parameter
        /// </summary>
        [MSBuildTestMethod]
        public void EvidenceFile()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.EvidenceFile); // "Default value"
            t.EvidenceFile = "MyEvidenceFile";
            Assert.AreEqual("MyEvidenceFile", t.EvidenceFile); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/evidence:MyEvidenceFile");
        }

        /// <summary>
        /// Tests the FileVersion parameter
        /// </summary>
        [MSBuildTestMethod]
        public void FileVersion()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.FileVersion); // "Default value"
            t.FileVersion = "1.2.3.4";
            Assert.AreEqual("1.2.3.4", t.FileVersion); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/fileversion:1.2.3.4");
        }

        /// <summary>
        /// Tests the Flags parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Flags()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Flags); // "Default value"
            t.Flags = "0x8421";
            Assert.AreEqual("0x8421", t.Flags); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/flags:0x8421");
        }

        /// <summary>
        /// Tests the GenerateFullPaths parameter.
        /// </summary>
        [MSBuildTestMethod]
        public void GenerateFullPaths()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsFalse(t.GenerateFullPaths); // "Default value"
            t.GenerateFullPaths = true;
            Assert.IsTrue(t.GenerateFullPaths); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, "/fullpaths");
        }

        /// <summary>
        /// Tests the KeyFile parameter
        /// </summary>
        [MSBuildTestMethod]
        public void KeyFile()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.KeyFile); // "Default value"
            t.KeyFile = "mykey.snk";
            Assert.AreEqual("mykey.snk", t.KeyFile); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/keyfile:mykey.snk");
        }

        /// <summary>
        /// Tests the KeyContainer parameter
        /// </summary>
        [MSBuildTestMethod]
        public void KeyContainer()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.KeyContainer); // "Default value"
            t.KeyContainer = "MyKeyContainer";
            Assert.AreEqual("MyKeyContainer", t.KeyContainer); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/keyname:MyKeyContainer");
        }

        /// <summary>
        /// Tests the LinkResources parameter with an item that has metadata LogicalName, Target, and Access=private
        /// </summary>
        [MSBuildTestMethod]
        public void LinkResourcesWithPrivateAccessAndTargetFile()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.LinkResources); // "Default value"

            // Construct the task item.
            TaskItem i = new TaskItem();
            i.ItemSpec = "MyResource.bmp";
            i.SetMetadata("LogicalName", "Kenny");
            i.SetMetadata("TargetFile", @"working\MyResource.bmp");
            i.SetMetadata("Access", "Private");
            t.LinkResources = new ITaskItem[] { i };

            Assert.ContainsSingle(t.LinkResources); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(
                t,
                @"/link:MyResource.bmp,Kenny,working\MyResource.bmp,Private");
        }

        /// <summary>
        /// Tests the LinkResources parameter with two items with differing metadata.
        /// </summary>
        [MSBuildTestMethod]
        public void LinkResourcesWithTwoItems()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.LinkResources); // "Default value"

            // Construct the task item.
            TaskItem i1 = new TaskItem();
            i1.ItemSpec = "MyResource.bmp";
            i1.SetMetadata("LogicalName", "Kenny");
            i1.SetMetadata("TargetFile", @"working\MyResource.bmp");
            i1.SetMetadata("Access", "Private");
            TaskItem i2 = new TaskItem();
            i2.ItemSpec = "MyResource2.bmp";
            i2.SetMetadata("LogicalName", "Chef");
            i2.SetMetadata("TargetFile", @"working\MyResource2.bmp");
            t.LinkResources = new ITaskItem[] { i1, i2 };

            Assert.AreEqual(2, t.LinkResources.Length); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(
                t,
                @"/link:MyResource.bmp,Kenny,working\MyResource.bmp,Private");
            CommandLine.ValidateHasParameter(
                t,
                @"/link:MyResource2.bmp,Chef,working\MyResource2.bmp");
        }

        /// <summary>
        /// Tests the MainEntryPoint parameter
        /// </summary>
        [MSBuildTestMethod]
        public void MainEntryPoint()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.MainEntryPoint); // "Default value"
            t.MainEntryPoint = "Class1.Main";
            Assert.AreEqual("Class1.Main", t.MainEntryPoint); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/main:Class1.Main");
        }

        /// <summary>
        /// Tests the OutputAssembly parameter
        /// </summary>
        [MSBuildTestMethod]
        public void OutputAssembly()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.OutputAssembly); // "Default value"
            t.OutputAssembly = new TaskItem("foo.dll");
            Assert.AreEqual("foo.dll", t.OutputAssembly.ItemSpec); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/out:foo.dll");
        }

        /// <summary>
        /// Tests the Platform parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Platform()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Platform); // "Default value"
            t.Platform = "x86";
            Assert.AreEqual("x86", t.Platform); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
        }

        // Tests the "Platform" and "Prefer32Bit" parameter combinations on the AL task,
        // and confirms that it sets the /platform switch on the command-line correctly.
        [MSBuildTestMethod]
        public void PlatformAndPrefer32Bit()
        {
            // Implicit "anycpu"
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };
            CommandLine.ValidateNoParameterStartsWith(t, @"/platform:");
            t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };
            t.Prefer32Bit = false;
            CommandLine.ValidateNoParameterStartsWith(t, @"/platform:");
            t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(
                t,
                @"/platform:anycpu32bitpreferred");

            // Explicit "anycpu"
            t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };
            t.Platform = "anycpu";
            CommandLine.ValidateHasParameter(t, @"/platform:anycpu");
            t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };
            t.Platform = "anycpu";
            t.Prefer32Bit = false;
            CommandLine.ValidateHasParameter(t, @"/platform:anycpu");
            t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };
            t.Platform = "anycpu";
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(
                t,
                @"/platform:anycpu32bitpreferred");

            // Explicit "x86"
            t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };
            t.Platform = "x86";
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
            t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };
            t.Platform = "x86";
            t.Prefer32Bit = false;
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
            t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };
            t.Platform = "x86";
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
        }

        /// <summary>
        /// Tests the ProductName parameter
        /// </summary>
        [MSBuildTestMethod]
        public void ProductName()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.ProductName); // "Default value"
            t.ProductName = "VisualStudio";
            Assert.AreEqual("VisualStudio", t.ProductName); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/product:VisualStudio");
        }

        /// <summary>
        /// Tests the ProductVersion parameter
        /// </summary>
        [MSBuildTestMethod]
        public void ProductVersion()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.ProductVersion); // "Default value"
            t.ProductVersion = "8.0";
            Assert.AreEqual("8.0", t.ProductVersion); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/productversion:8.0");
        }

        /// <summary>
        /// Tests the ResponseFiles parameter
        /// </summary>
        [MSBuildTestMethod]
        public void ResponseFiles()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.ResponseFiles); // "Default value"
            t.ResponseFiles = new string[2] { "one.rsp", "two.rsp" };
            Assert.AreEqual(2, t.ResponseFiles.Length); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"@one.rsp");
            CommandLine.ValidateHasParameter(t, @"@two.rsp");
        }

        /// <summary>
        /// Tests the SourceModules parameter
        /// </summary>
        [MSBuildTestMethod]
        public void SourceModules()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.SourceModules); // "Default value"

            // Construct the task items.
            TaskItem i1 = new TaskItem();
            i1.ItemSpec = "Strings.resources";
            i1.SetMetadata("TargetFile", @"working\MyResource.bmp");
            TaskItem i2 = new TaskItem();
            i2.ItemSpec = "Dialogs.resources";
            t.SourceModules = new ITaskItem[] { i1, i2 };

            Assert.AreEqual(2, t.SourceModules.Length); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"Strings.resources,working\MyResource.bmp");
            CommandLine.ValidateHasParameter(t, @"Dialogs.resources");
        }

        /// <summary>
        /// Tests the TargetType parameter
        /// </summary>
        [MSBuildTestMethod]
        public void TargetType()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.TargetType); // "Default value"
            t.TargetType = "winexe";
            Assert.AreEqual("winexe", t.TargetType); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/target:winexe");
        }

        /// <summary>
        /// Tests the TemplateFile parameter
        /// </summary>
        [MSBuildTestMethod]
        public void TemplateFile()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.TemplateFile); // "Default value"
            t.TemplateFile = "mymainassembly.dll";
            Assert.AreEqual("mymainassembly.dll", t.TemplateFile); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(
                t,
                @"/template:mymainassembly.dll");
        }

        /// <summary>
        /// Tests the Title parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Title()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Title); // "Default value"
            t.Title = "WarAndPeace";
            Assert.AreEqual("WarAndPeace", t.Title); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/title:WarAndPeace");
        }

        /// <summary>
        /// Tests the Trademark parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Trademark()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Trademark); // "Default value"
            t.Trademark = "MyTrademark";
            Assert.AreEqual("MyTrademark", t.Trademark); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/trademark:MyTrademark");
        }

        /// <summary>
        /// Tests the Version parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Version()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Version); // "Default value"
            t.Version = "WowHowManyKindsOfVersionsAreThere";
            Assert.AreEqual("WowHowManyKindsOfVersionsAreThere", t.Version); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(
                t,
                @"/version:WowHowManyKindsOfVersionsAreThere");
        }

        /// <summary>
        /// Tests the Win32Icon parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Win32Icon()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Win32Icon); // "Default value"
            t.Win32Icon = "foo.ico";
            Assert.AreEqual("foo.ico", t.Win32Icon); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/win32icon:foo.ico");
        }

        /// <summary>
        /// Tests the Win32Resource parameter
        /// </summary>
        [MSBuildTestMethod]
        public void Win32Resource()
        {
            AL t = new AL() { TaskEnvironment = TaskEnvironmentHelper.CreateForTest() };

            Assert.IsNull(t.Win32Resource); // "Default value"
            t.Win32Resource = "foo.res";
            Assert.AreEqual("foo.res", t.Win32Resource); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/win32res:foo.res");
        }

        /// <summary>
        /// Verifies that GenerateFullPathToTool returns an absolute path (or null)
        /// when called with a multithreaded TaskEnvironment, validating the
        /// TaskEnvironment.GetAbsolutePath() integration.
        /// </summary>
        [WindowsFullFrameworkOnlyFact]
        public void GenerateFullPathToTool_ReturnsAbsolutePathOrNull()
        {
            string projectDir = Path.GetTempPath();
            using var driver = new MultiThreadedTaskEnvironmentDriver(projectDir);
            var taskEnv = new TaskEnvironment(driver);

            TestableAL t = new TestableAL();
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
        [WindowsFullFrameworkOnlyFact]
        public void GetProcessStartInfo_UsesTaskEnvironmentWorkingDirectory()
        {
            string expectedWorkingDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            using var driver = new MultiThreadedTaskEnvironmentDriver(expectedWorkingDir);
            var taskEnv = new TaskEnvironment(driver);

            TestableAL t = new TestableAL();
            t.TaskEnvironment = taskEnv;
            t.BuildEngine = new MockEngine(_output);

            ProcessStartInfo startInfo = t.CallGetProcessStartInfo(@"C:\test\al.exe", "/nologo", null);

            startInfo.WorkingDirectory.ShouldBe(expectedWorkingDir);
        }

        /// <summary>
        /// Subclass that exposes protected methods for testing without reflection.
        /// </summary>
        private sealed class TestableAL : AL
        {
            public string CallGenerateFullPathToTool() => GenerateFullPathToTool();

            public ProcessStartInfo CallGetProcessStartInfo(string pathToTool, string commandLineCommands, string responseFileSwitch)
                => GetProcessStartInfo(pathToTool, commandLineCommands, responseFileSwitch);
        }
    }
}
