// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Shouldly;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for write code fragment task
    /// </summary>
    public class WriteCodeFragment_Tests
    {
        /// <summary>
        /// Need an available language
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void InvalidLanguage()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "xx";
            task.OutputFile = new TaskItem("foo");
            bool result = task.Execute();

            Assert.False(result);
            engine.AssertLogContains("MSB3712");
        }

        /// <summary>
        /// Need a language
        /// </summary>
        [Fact]
        public void NoLanguage()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.OutputFile = new TaskItem("foo");
            bool result = task.Execute();

            Assert.False(result);
            engine.AssertLogContains("MSB3098");
        }

        /// <summary>
        /// Need a location
        /// </summary>
        [Fact]
        public void NoFileOrDirectory()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "c#";
            bool result = task.Execute();

            Assert.False(result);
            engine.AssertLogContains("MSB3711");
        }

        /// <summary>
        /// Combine file and directory
        /// </summary>
        [Fact]
        public void CombineFileDirectory()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "c#";
            task.AssemblyAttributes = new TaskItem[] { new TaskItem("aa") };
            task.OutputFile = new TaskItem("CombineFileDirectory.tmp");
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);

            string file = Path.Combine(Path.GetTempPath(), "CombineFileDirectory.tmp");
            Assert.Equal(file, task.OutputFile.ItemSpec);
            Assert.True(File.Exists(file));
        }

        /// <summary>
        /// Ignore directory if file is rooted
        /// </summary>
        [Fact]
        public void DirectoryAndRootedFile()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "c#";
            task.AssemblyAttributes = new TaskItem[] { new TaskItem("aa") };

            string folder = Path.Combine(Path.GetTempPath(), "foo" + Path.DirectorySeparatorChar);
            string file = Path.Combine(folder, "CombineFileDirectory.tmp");
            Directory.CreateDirectory(folder);
            task.OutputFile = new TaskItem(file);
            task.OutputDirectory = new TaskItem("c:\\");
            bool result = task.Execute();

            Assert.True(result);

            Assert.Equal(file, task.OutputFile.ItemSpec);
            Assert.True(File.Exists(file));

            FileUtilities.DeleteWithoutTrailingBackslash(folder, true);
        }

        /// <summary>
        /// Given nothing to write, should succeed but
        /// produce no output file
        /// </summary>
        [Fact]
        public void NoAttributesShouldEmitNoFile()
        {
            string file = Path.Combine(Path.GetTempPath(), "NoAttributesShouldEmitNoFile.tmp");

            if (File.Exists(file))
            {
                File.Delete(file);
            }

            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "c#";
            task.AssemblyAttributes = new TaskItem[] { }; // MSBuild sets an empty array
            task.OutputFile = new TaskItem(file);
            bool result = task.Execute();

            Assert.True(result);
            Assert.False(File.Exists(file));
            Assert.Null(task.OutputFile);
        }

        /// <summary>
        /// Given nothing to write, should succeed but
        /// produce no output file
        /// </summary>
        [Fact]
        public void NoAttributesShouldEmitNoFile2()
        {
            string file = Path.Combine(Path.GetTempPath(), "NoAttributesShouldEmitNoFile.tmp");

            if (File.Exists(file))
            {
                File.Delete(file);
            }

            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "c#";
            task.AssemblyAttributes = null; // null this time
            task.OutputFile = new TaskItem(file);
            bool result = task.Execute();

            Assert.True(result);
            Assert.False(File.Exists(file));
            Assert.Null(task.OutputFile);
        }

        /// <summary>
        /// Bad file path
        /// </summary>
        [Fact]
        public void InvalidFilePath()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "c#";
            task.AssemblyAttributes = new TaskItem[] { new TaskItem("aa") };
            task.OutputFile = new TaskItem("||//invalid||");
            bool result = task.Execute();

            Assert.False(result);
            engine.AssertLogContains("MSB3713");
        }

        /// <summary>
        /// Bad directory path
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)] // "No invalid characters on Unix"
        public void InvalidDirectoryPath()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "c#";
            task.AssemblyAttributes = new TaskItem[] { new TaskItem("aa") };
            task.OutputDirectory = new TaskItem("||invalid||");
            bool result = task.Execute();

            Assert.False(result);
            engine.AssertLogContains("MSB3713");
        }

        /// <summary>
        /// Parameterless attribute
        /// </summary>
        [Fact]
        public void OneAttributeNoParams()
        {
            string file = Path.Combine(Path.GetTempPath(), "OneAttribute.tmp");

            try
            {
                WriteCodeFragment task = new WriteCodeFragment();
                MockEngine engine = new MockEngine(true);
                task.BuildEngine = engine;
                TaskItem attribute = new TaskItem("System.AssemblyTrademarkAttribute");
                task.AssemblyAttributes = new TaskItem[] { attribute };
                task.Language = "c#";
                task.OutputFile = new TaskItem(file);
                bool result = task.Execute();

                Assert.True(result);
                Assert.True(File.Exists(file));

                string content = File.ReadAllText(file);
                Console.WriteLine(content);

                CheckContentCSharp(content, "[assembly: System.AssemblyTrademarkAttribute()]");
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Test with the VB language
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void OneAttributeNoParamsVb()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("System.AssemblyTrademarkAttribute");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "visualbasic";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            CheckContentVB(content, "<Assembly: System.AssemblyTrademarkAttribute()>");
        }

        /// <summary>
        /// More than one attribute
        /// </summary>
        [Fact]
        public void TwoAttributes()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute1 = new TaskItem("AssemblyTrademarkAttribute");
            attribute1.SetMetadata("Name", "Microsoft");
            TaskItem attribute2 = new TaskItem("System.AssemblyCultureAttribute");
            attribute2.SetMetadata("Culture", "en-US");
            task.AssemblyAttributes = new TaskItem[] { attribute1, attribute2 };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            CheckContentCSharp(
                content,
                @"[assembly: AssemblyTrademarkAttribute(Name=""Microsoft"")]",
                @"[assembly: System.AssemblyCultureAttribute(Culture=""en-US"")]");
        }

        /// <summary>
        /// Specify directory instead
        /// </summary>
        [Fact]
        public void ToDirectory()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("System.AssemblyTrademarkAttribute");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);
            Assert.True(File.Exists(task.OutputFile.ItemSpec));
            Assert.Equal(Path.GetTempPath(), task.OutputFile.ItemSpec.Substring(0, Path.GetTempPath().Length));
            Assert.Equal(".cs", task.OutputFile.ItemSpec.Substring(task.OutputFile.ItemSpec.Length - 3));

            File.Delete(task.OutputFile.ItemSpec);
        }

        /// <summary>
        /// Regular case
        /// </summary>
        [Fact]
        public void OneAttributeTwoParams()
        {
            string file = Path.Combine(Path.GetTempPath(), "OneAttribute.tmp");

            try
            {
                WriteCodeFragment task = new WriteCodeFragment();
                MockEngine engine = new MockEngine(true);
                task.BuildEngine = engine;
                TaskItem attribute = new TaskItem("AssemblyTrademarkAttribute");
                attribute.SetMetadata("Company", "Microsoft");
                attribute.SetMetadata("Year", "2009");
                task.AssemblyAttributes = new TaskItem[] { attribute };
                task.Language = "c#";
                task.OutputFile = new TaskItem(file);
                bool result = task.Execute();

                Assert.True(result);
                Assert.True(File.Exists(file));

                string content = File.ReadAllText(file);
                Console.WriteLine(content);

                CheckContentCSharp(content, @"[assembly: AssemblyTrademarkAttribute(Company=""Microsoft"", Year=""2009"")]");
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// This produces invalid code, but the task works
        /// </summary>
        [Fact]
        public void OneAttributeTwoParamsSameName()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("AssemblyTrademarkAttribute");
            attribute.SetMetadata("Company", "Microsoft");
            attribute.SetMetadata("Company", "2009");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);

            File.Delete(task.OutputFile.ItemSpec);
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// </summary>
        [Fact]
        public void OneAttributePositionalParamInvalidSuffix()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("AssemblyTrademarkAttribute");
            attribute.SetMetadata("_ParameterXXXXXXXXXX", "Microsoft");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.False(result);

            engine.AssertLogContains("MSB3098");
        }


        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// </summary>
        [Fact]
        public void OneAttributeTwoPositionalParams()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("AssemblyTrademarkAttribute");
            attribute.SetMetadata("_Parameter1", "Microsoft");
            attribute.SetMetadata("_Parameter2", "2009");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            CheckContentCSharp(content, @"[assembly: AssemblyTrademarkAttribute(""Microsoft"", ""2009"")]");

            File.Delete(task.OutputFile.ItemSpec);
        }

        [Fact]
        public void OneAttributeTwoPositionalParamsWithSameValue()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("AssemblyMetadataAttribute");
            attribute.SetMetadata("_Parameter1", "TestValue");
            attribute.SetMetadata("_Parameter2", "TestValue");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            CheckContentCSharp(content, @"[assembly: AssemblyMetadataAttribute(""TestValue"", ""TestValue"")]");

            File.Delete(task.OutputFile.ItemSpec);
        }

        public static string EscapedLineSeparator => NativeMethodsShared.IsWindows ? "\\r\\n" : "\\n";

        /// <summary>
        /// Multi line argument values should cause a verbatim string to be used
        /// </summary>
        [Fact]
        public void MultilineAttributeCSharp()
        {
            var lines = new[] { "line 1", "line 2", "line 3" };
            var multilineString = String.Join(Environment.NewLine, lines);

            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("System.Reflection.AssemblyDescriptionAttribute");
            attribute.SetMetadata("_Parameter1", multilineString);
            attribute.SetMetadata("Description", multilineString);
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            var csMultilineString = lines.Aggregate((l1, l2) => l1 + EscapedLineSeparator + l2);
            CheckContentCSharp(content, $"[assembly: System.Reflection.AssemblyDescriptionAttribute(\"{csMultilineString}\", Description=\"{csMultilineString}\")]");

            File.Delete(task.OutputFile.ItemSpec);
        }

        private static readonly string VBCarriageReturn = "Global.Microsoft.VisualBasic.ChrW(13)";
        private static readonly string VBLineFeed = "Global.Microsoft.VisualBasic.ChrW(10)";

        public static readonly string VBLineSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{VBCarriageReturn}&{VBLineFeed}" : VBLineFeed;

        /// <summary>
        /// Multi line argument values should cause a verbatim string to be used
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void MultilineAttributeVB()
        {
            var lines = new []{ "line 1", "line 2", "line 3" };
            var multilineString = String.Join(Environment.NewLine, lines);

            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("System.Reflection.AssemblyDescriptionAttribute");
            attribute.SetMetadata("_Parameter1", multilineString);
            attribute.SetMetadata("Description", multilineString);
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "visualbasic";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            var vbMultilineString = lines
                .Select(l => $"\"{l}\"")
                .Aggregate((l1, l2) => $"{l1}&{VBLineSeparator}&{l2}");

            CheckContentVB(content, $"<Assembly: System.Reflection.AssemblyDescriptionAttribute({vbMultilineString}, Description:={vbMultilineString})>");

            File.Delete(task.OutputFile.ItemSpec);
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// If a parameter is skipped, it's an error.
        /// </summary>
        [Fact]
        public void OneAttributeSkippedPositionalParams()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("AssemblyTrademarkAttribute");
            attribute.SetMetadata("_Parameter2", "2009");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.False(result);

            engine.AssertLogContains("MSB3714");
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// This test is for "_ParameterX"
        /// </summary>
        [Fact]
        public void InvalidNumber()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("AssemblyTrademarkAttribute");
            attribute.SetMetadata("_ParameterX", "2009");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.False(result);

            engine.AssertLogContains("MSB3098");
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// This test is for "_Parameter"
        /// </summary>
        [Fact]
        public void NoNumber()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("AssemblyTrademarkAttribute");
            attribute.SetMetadata("_Parameter", "2009");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.False(result);

            engine.AssertLogContains("MSB3098");
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// These can also be combined with named params.
        /// </summary>
        [Fact]
        public void OneAttributePositionalAndNamedParams()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("AssemblyTrademarkAttribute");
            attribute.SetMetadata("_Parameter1", "Microsoft");
            attribute.SetMetadata("Date", "2009");
            attribute.SetMetadata("Copyright", "(C)");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "c#";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            CheckContentCSharp(content, @"[assembly: AssemblyTrademarkAttribute(""Microsoft"", Date=""2009"", Copyright=""(C)"")]");

            File.Delete(task.OutputFile.ItemSpec);
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// These can also be combined with named params.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void OneAttributePositionalAndNamedParamsVisualBasic()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            TaskItem attribute = new TaskItem("AssemblyTrademarkAttribute");
            attribute.SetMetadata("_Parameter1", "Microsoft");
            attribute.SetMetadata("_Parameter2", "2009");
            attribute.SetMetadata("Copyright", "(C)");
            task.AssemblyAttributes = new TaskItem[] { attribute };
            task.Language = "visualbasic";
            task.OutputDirectory = new TaskItem(Path.GetTempPath());
            bool result = task.Execute();

            Assert.True(result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            CheckContentVB(content, @"<Assembly: AssemblyTrademarkAttribute(""Microsoft"", ""2009"", Copyright:=""(C)"")>");

            File.Delete(task.OutputFile.ItemSpec);
        }

        private static void CheckContentCSharp(string actualContent, params string[] expectedAttributes)
        {
            CheckContent(
                actualContent,
                expectedAttributes,
                "//",
                "using System;",
                "using System.Reflection;");
        }

        private static void CheckContentVB(string actualContent, params string[] expectedAttributes)
        {
            CheckContent(
                actualContent,
                expectedAttributes,
                "'",
                "Option Strict Off",
                "Option Explicit On",
                "Imports System",
                "Imports System.Reflection");
        }

        private static void CheckContent(string actualContent, string[] expectedAttributes, string commentStart, params string[] expectedHeader)
        {
            string expectedContent = string.Join(Environment.NewLine, expectedHeader.Concat(expectedAttributes));

            // we tolerate differences in whitespace and comments between platforms
            string normalizedActualContent = string.Join(
                Environment.NewLine,
                actualContent.Split(MSBuildConstants.CrLf)
                             .Select(line => line.Trim())
                             .Where(line => line.Length > 0 && !line.StartsWith(commentStart)));

            expectedContent.ShouldBe(normalizedActualContent);
        }
    }
}



