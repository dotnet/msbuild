// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using System.Globalization;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   AlTests
     *
     * Test the AL task in various ways.
     *
     */
    sealed public class AlTests
    {
        /// <summary>
        /// Tests the AlgorithmId parameter
        /// </summary>
        [Fact]
        public void AlgorithmId()
        {
            AL t = new AL();

            Assert.Null(t.AlgorithmId); // "Default value"
            t.AlgorithmId = "whatisthis";
            Assert.Equal("whatisthis", t.AlgorithmId); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/algid:whatisthis");
        }

        /// <summary>
        /// Tests the BaseAddress parameter
        /// </summary>
        [Fact]
        public void BaseAddress()
        {
            AL t = new AL();

            Assert.Null(t.BaseAddress); // "Default value"
            t.BaseAddress = "12345678";
            Assert.Equal("12345678", t.BaseAddress); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/baseaddress:12345678");
        }

        /// <summary>
        /// Tests the CompanyName parameter
        /// </summary>
        [Fact]
        public void CompanyName()
        {
            AL t = new AL();

            Assert.Null(t.CompanyName); // "Default value"
            t.CompanyName = "Google";
            Assert.Equal("Google", t.CompanyName); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/company:Google");
        }

        /// <summary>
        /// Tests the Configuration parameter
        /// </summary>
        [Fact]
        public void Configuration()
        {
            AL t = new AL();

            Assert.Null(t.Configuration); // "Default value"
            t.Configuration = "debug";
            Assert.Equal("debug", t.Configuration); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/configuration:debug");
        }

        /// <summary>
        /// Tests the Copyright parameter
        /// </summary>
        [Fact]
        public void Copyright()
        {
            AL t = new AL();

            Assert.Null(t.Copyright); // "Default value"
            t.Copyright = "(C) 2005";
            Assert.Equal("(C) 2005", t.Copyright); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/copyright:(C) 2005");
        }

        /// <summary>
        /// Tests the Culture parameter
        /// </summary>
        [Fact]
        public void Culture()
        {
            AL t = new AL();

            Assert.Null(t.Culture); // "Default value"
            t.Culture = "aussie";
            Assert.Equal("aussie", t.Culture); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/culture:aussie");
        }

        /// <summary>
        /// Tests the DelaySign parameter.
        /// </summary>
        [Fact]
        public void DelaySign()
        {
            AL t = new AL();

            Assert.False(t.DelaySign); // "Default value"
            t.DelaySign = true;
            Assert.True(t.DelaySign); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, "/delaysign+");
        }

        /// <summary>
        /// Tests the Description parameter
        /// </summary>
        [Fact]
        public void Description()
        {
            AL t = new AL();

            Assert.Null(t.Description); // "Default value"
            t.Description = "whatever";
            Assert.Equal("whatever", t.Description); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/description:whatever");
        }

        /// <summary>
        /// Tests the EmbedResources parameter with an item that has metadata LogicalName and Access=private
        /// </summary>
        [Fact]
        public void EmbedResourcesWithPrivateAccess()
        {
            AL t = new AL();

            Assert.Null(t.EmbedResources); // "Default value"

            // Construct the task item.
            TaskItem i = new TaskItem();
            i.ItemSpec = "MyResource.bmp";
            i.SetMetadata("LogicalName", "Kenny");
            i.SetMetadata("Access", "Private");
            t.EmbedResources = new ITaskItem[] { i };

            Assert.Single(t.EmbedResources); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(
                t,
                "/embed:MyResource.bmp,Kenny,Private");
        }

        /// <summary>
        /// Tests the EvidenceFile parameter
        /// </summary>
        [Fact]
        public void EvidenceFile()
        {
            AL t = new AL();

            Assert.Null(t.EvidenceFile); // "Default value"
            t.EvidenceFile = "MyEvidenceFile";
            Assert.Equal("MyEvidenceFile", t.EvidenceFile); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/evidence:MyEvidenceFile");
        }

        /// <summary>
        /// Tests the FileVersion parameter
        /// </summary>
        [Fact]
        public void FileVersion()
        {
            AL t = new AL();

            Assert.Null(t.FileVersion); // "Default value"
            t.FileVersion = "1.2.3.4";
            Assert.Equal("1.2.3.4", t.FileVersion); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/fileversion:1.2.3.4");
        }

        /// <summary>
        /// Tests the Flags parameter
        /// </summary>
        [Fact]
        public void Flags()
        {
            AL t = new AL();

            Assert.Null(t.Flags); // "Default value"
            t.Flags = "0x8421";
            Assert.Equal("0x8421", t.Flags); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/flags:0x8421");
        }

        /// <summary>
        /// Tests the GenerateFullPaths parameter.
        /// </summary>
        [Fact]
        public void GenerateFullPaths()
        {
            AL t = new AL();

            Assert.False(t.GenerateFullPaths); // "Default value"
            t.GenerateFullPaths = true;
            Assert.True(t.GenerateFullPaths); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, "/fullpaths");
        }

        /// <summary>
        /// Tests the KeyFile parameter
        /// </summary>
        [Fact]
        public void KeyFile()
        {
            AL t = new AL();

            Assert.Null(t.KeyFile); // "Default value"
            t.KeyFile = "mykey.snk";
            Assert.Equal("mykey.snk", t.KeyFile); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/keyfile:mykey.snk");
        }

        /// <summary>
        /// Tests the KeyContainer parameter
        /// </summary>
        [Fact]
        public void KeyContainer()
        {
            AL t = new AL();

            Assert.Null(t.KeyContainer); // "Default value"
            t.KeyContainer = "MyKeyContainer";
            Assert.Equal("MyKeyContainer", t.KeyContainer); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/keyname:MyKeyContainer");
        }

        /// <summary>
        /// Tests the LinkResources parameter with an item that has metadata LogicalName, Target, and Access=private
        /// </summary>
        [Fact]
        public void LinkResourcesWithPrivateAccessAndTargetFile()
        {
            AL t = new AL();

            Assert.Null(t.LinkResources); // "Default value"

            // Construct the task item.
            TaskItem i = new TaskItem();
            i.ItemSpec = "MyResource.bmp";
            i.SetMetadata("LogicalName", "Kenny");
            i.SetMetadata("TargetFile", @"working\MyResource.bmp");
            i.SetMetadata("Access", "Private");
            t.LinkResources = new ITaskItem[] { i };

            Assert.Single(t.LinkResources); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(
                t,
                @"/link:MyResource.bmp,Kenny,working\MyResource.bmp,Private");
        }

        /// <summary>
        /// Tests the LinkResources parameter with two items with differing metadata.
        /// </summary>
        [Fact]
        public void LinkResourcesWithTwoItems()
        {
            AL t = new AL();

            Assert.Null(t.LinkResources); // "Default value"

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

            Assert.Equal(2, t.LinkResources.Length); // "New value"

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
        [Fact]
        public void MainEntryPoint()
        {
            AL t = new AL();

            Assert.Null(t.MainEntryPoint); // "Default value"
            t.MainEntryPoint = "Class1.Main";
            Assert.Equal("Class1.Main", t.MainEntryPoint); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/main:Class1.Main");
        }

        /// <summary>
        /// Tests the OutputAssembly parameter
        /// </summary>
        [Fact]
        public void OutputAssembly()
        {
            AL t = new AL();

            Assert.Null(t.OutputAssembly); // "Default value"
            t.OutputAssembly = new TaskItem("foo.dll");
            Assert.Equal("foo.dll", t.OutputAssembly.ItemSpec); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/out:foo.dll");
        }

        /// <summary>
        /// Tests the Platform parameter
        /// </summary>
        [Fact]
        public void Platform()
        {
            AL t = new AL();

            Assert.Null(t.Platform); // "Default value"
            t.Platform = "x86";
            Assert.Equal("x86", t.Platform); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
        }

        // Tests the "Platform" and "Prefer32Bit" parameter combinations on the AL task,
        // and confirms that it sets the /platform switch on the command-line correctly.
        [Fact]
        public void PlatformAndPrefer32Bit()
        {
            // Implicit "anycpu"
            AL t = new AL();
            CommandLine.ValidateNoParameterStartsWith(t, @"/platform:");
            t = new AL();
            t.Prefer32Bit = false;
            CommandLine.ValidateNoParameterStartsWith(t, @"/platform:");
            t = new AL();
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(
                t,
                @"/platform:anycpu32bitpreferred");

            // Explicit "anycpu"
            t = new AL();
            t.Platform = "anycpu";
            CommandLine.ValidateHasParameter(t, @"/platform:anycpu");
            t = new AL();
            t.Platform = "anycpu";
            t.Prefer32Bit = false;
            CommandLine.ValidateHasParameter(t, @"/platform:anycpu");
            t = new AL();
            t.Platform = "anycpu";
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(
                t,
                @"/platform:anycpu32bitpreferred");

            // Explicit "x86"
            t = new AL();
            t.Platform = "x86";
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
            t = new AL();
            t.Platform = "x86";
            t.Prefer32Bit = false;
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
            t = new AL();
            t.Platform = "x86";
            t.Prefer32Bit = true;
            CommandLine.ValidateHasParameter(t, @"/platform:x86");
        }

        /// <summary>
        /// Tests the ProductName parameter
        /// </summary>
        [Fact]
        public void ProductName()
        {
            AL t = new AL();

            Assert.Null(t.ProductName); // "Default value"
            t.ProductName = "VisualStudio";
            Assert.Equal("VisualStudio", t.ProductName); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/product:VisualStudio");
        }

        /// <summary>
        /// Tests the ProductVersion parameter
        /// </summary>
        [Fact]
        public void ProductVersion()
        {
            AL t = new AL();

            Assert.Null(t.ProductVersion); // "Default value"
            t.ProductVersion = "8.0";
            Assert.Equal("8.0", t.ProductVersion); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/productversion:8.0");
        }

        /// <summary>
        /// Tests the ResponseFiles parameter
        /// </summary>
        [Fact]
        public void ResponseFiles()
        {
            AL t = new AL();

            Assert.Null(t.ResponseFiles); // "Default value"
            t.ResponseFiles = new string[2] { "one.rsp", "two.rsp" };
            Assert.Equal(2, t.ResponseFiles.Length); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"@one.rsp");
            CommandLine.ValidateHasParameter(t, @"@two.rsp");
        }

        /// <summary>
        /// Tests the SourceModules parameter
        /// </summary>
        [Fact]
        public void SourceModules()
        {
            AL t = new AL();

            Assert.Null(t.SourceModules); // "Default value"

            // Construct the task items.
            TaskItem i1 = new TaskItem();
            i1.ItemSpec = "Strings.resources";
            i1.SetMetadata("TargetFile", @"working\MyResource.bmp");
            TaskItem i2 = new TaskItem();
            i2.ItemSpec = "Dialogs.resources";
            t.SourceModules = new ITaskItem[] { i1, i2 };

            Assert.Equal(2, t.SourceModules.Length); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"Strings.resources,working\MyResource.bmp");
            CommandLine.ValidateHasParameter(t, @"Dialogs.resources");
        }

        /// <summary>
        /// Tests the TargetType parameter
        /// </summary>
        [Fact]
        public void TargetType()
        {
            AL t = new AL();

            Assert.Null(t.TargetType); // "Default value"
            t.TargetType = "winexe";
            Assert.Equal("winexe", t.TargetType); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/target:winexe");
        }

        /// <summary>
        /// Tests the TemplateFile parameter
        /// </summary>
        [Fact]
        public void TemplateFile()
        {
            AL t = new AL();

            Assert.Null(t.TemplateFile); // "Default value"
            t.TemplateFile = "mymainassembly.dll";
            Assert.Equal("mymainassembly.dll", t.TemplateFile); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(
                t,
                @"/template:mymainassembly.dll");
        }

        /// <summary>
        /// Tests the Title parameter
        /// </summary>
        [Fact]
        public void Title()
        {
            AL t = new AL();

            Assert.Null(t.Title); // "Default value"
            t.Title = "WarAndPeace";
            Assert.Equal("WarAndPeace", t.Title); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/title:WarAndPeace");
        }

        /// <summary>
        /// Tests the Trademark parameter
        /// </summary>
        [Fact]
        public void Trademark()
        {
            AL t = new AL();

            Assert.Null(t.Trademark); // "Default value"
            t.Trademark = "MyTrademark";
            Assert.Equal("MyTrademark", t.Trademark); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/trademark:MyTrademark");
        }

        /// <summary>
        /// Tests the Version parameter
        /// </summary>
        [Fact]
        public void Version()
        {
            AL t = new AL();

            Assert.Null(t.Version); // "Default value"
            t.Version = "WowHowManyKindsOfVersionsAreThere";
            Assert.Equal("WowHowManyKindsOfVersionsAreThere", t.Version); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(
                t,
                @"/version:WowHowManyKindsOfVersionsAreThere");
        }

        /// <summary>
        /// Tests the Win32Icon parameter
        /// </summary>
        [Fact]
        public void Win32Icon()
        {
            AL t = new AL();

            Assert.Null(t.Win32Icon); // "Default value"
            t.Win32Icon = "foo.ico";
            Assert.Equal("foo.ico", t.Win32Icon); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/win32icon:foo.ico");
        }

        /// <summary>
        /// Tests the Win32Resource parameter
        /// </summary>
        [Fact]
        public void Win32Resource()
        {
            AL t = new AL();

            Assert.Null(t.Win32Resource); // "Default value"
            t.Win32Resource = "foo.res";
            Assert.Equal("foo.res", t.Win32Resource); // "New value"

            // Check the parameters.
            CommandLine.ValidateHasParameter(t, @"/win32res:foo.res");
        }
    }
}





