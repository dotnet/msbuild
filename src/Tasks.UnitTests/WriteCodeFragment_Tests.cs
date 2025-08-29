// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable disable

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

            File.Delete(task.OutputFile.ItemSpec);
        }

        /// <summary>
        /// Combine file and directory where the directory does not already exist
        /// </summary>
        [Fact]
        public void CombineFileDirectoryAndDirectoryDoesNotExist()
        {
            using TestEnvironment env = TestEnvironment.Create();

            TaskItem folder = new TaskItem(env.CreateFolder(folderPath: null, createFolder: false).Path);

            TaskItem file = new TaskItem("CombineFileDirectory.tmp");

            string expectedFile = Path.Combine(folder.ItemSpec, file.ItemSpec);
            WriteCodeFragment task = CreateTask("c#", folder, file, new TaskItem[] { new TaskItem("aa") });
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(expectedFile, task.OutputFile.ItemSpec);
            Assert.True(File.Exists(expectedFile));
        }

        /// <summary>
        /// Combine file and directory where the directory does not already exist
        /// </summary>
        [Fact]
        public void FileWithPathAndDirectoryDoesNotExist()
        {
            using TestEnvironment env = TestEnvironment.Create();

            TaskItem file = new TaskItem(Path.Combine(env.CreateFolder(folderPath: null, createFolder: false).Path, "File.tmp"));

            WriteCodeFragment task = CreateTask("c#", null, file, new TaskItem[] { new TaskItem("aa") });
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(file.ItemSpec, task.OutputFile.ItemSpec);
            Assert.True(File.Exists(task.OutputFile.ItemSpec));
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
            task.AssemblyAttributes = Array.Empty<TaskItem>(); // MSBuild sets an empty array
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
        [WindowsOnlyFact(additionalMessage: "No invalid characters on Unix.")]
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
        [WindowsOnlyFact(additionalMessage: "No invalid characters on Unix.")]
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
        /// Specify directory where the directory does not already exist
        /// </summary>
        [Fact]
        public void ToDirectoryAndDirectoryDoesNotExist()
        {
            using TestEnvironment env = TestEnvironment.Create();

            TaskItem folder = new TaskItem(env.CreateFolder(folderPath: null, createFolder: false).Path);

            WriteCodeFragment task = CreateTask("c#", folder, null, new TaskItem[] { new TaskItem("System.AssemblyTrademarkAttribute") });
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            bool result = task.Execute();

            Assert.True(result);
            Assert.True(File.Exists(task.OutputFile.ItemSpec));
            Assert.Equal(folder.ItemSpec, task.OutputFile.ItemSpec.Substring(0, folder.ItemSpec.Length));
            Assert.Equal(".cs", task.OutputFile.ItemSpec.Substring(task.OutputFile.ItemSpec.Length - 3));
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

        private const string VBCarriageReturn = "Global.Microsoft.VisualBasic.ChrW(13)";
        private const string VBLineFeed = "Global.Microsoft.VisualBasic.ChrW(10)";

        public static readonly string VBLineSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{VBCarriageReturn}&{VBLineFeed}" : VBLineFeed;

        /// <summary>
        /// Multi line argument values should cause a verbatim string to be used
        /// </summary>
        [Fact]
        public void MultilineAttributeVB()
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

            // NOTE: order here is defined by dictionary traversal order and may change
            // based on implementation details there, but named parameters can have different
            // orders so that's ok.
            CheckContentCSharp(content, @"[assembly: AssemblyTrademarkAttribute(""Microsoft"", Copyright=""(C)"", Date=""2009"")]");

            File.Delete(task.OutputFile.ItemSpec);
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// These can also be combined with named params.
        /// </summary>
        [Fact]
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

        /// <summary>
        /// A type can be declared for a positional arguments using the metadata
        /// "_Parameter1_TypeName" where the value is the full type name.
        /// </summary>
        [Fact]
        public void DeclaredTypeForPositionalParameter()
        {
            TaskItem attribute = new("CLSCompliantAttribute");
            attribute.SetMetadata("_Parameter1", "True");
            attribute.SetMetadata("_Parameter1_TypeName", "System.Boolean");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: CLSCompliantAttribute(true)]");
        }

        /// <summary>
        /// A type can be declared for a positional arguments using the metadata
        /// "Foo_TypeName" where the value is the full type name.
        /// </summary>
        [Fact]
        public void DeclaredTypeForNamedParameter()
        {
            TaskItem attribute = new TaskItem("TestAttribute");
            attribute.SetMetadata("BoolArgument", "False");
            attribute.SetMetadata("BoolArgument_TypeName", "System.Boolean");
            attribute.SetMetadata("Int32Argument", "42");
            attribute.SetMetadata("Int32Argument_TypeName", "System.Int32");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: TestAttribute(Int32Argument=42, BoolArgument=false)]");
        }

        /// <summary>
        /// Metadata that looks like a declared type, but doesn't have corresponding named parameter
        /// metadata should be treated as another named parameter for backward-compatibility.
        /// </summary>
        [Fact]
        public void DeclaredTypedWithoutCorrespondingNamedParameter()
        {
            TaskItem attribute = new TaskItem("TestAttribute");
            attribute.SetMetadata("BoolArgument", "False");
            attribute.SetMetadata("BoolArgument_TypeName", "System.Boolean");
            attribute.SetMetadata("Int32Argument_TypeName", "System.Int32");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: TestAttribute(Int32Argument_TypeName=""System.Int32"", BoolArgument=false)]");
        }

        /// <summary>
        /// An unknown type name for a parameter should cause a failure.
        /// </summary>
        [Fact]
        public void DeclaredTypeIsUnknown()
        {
            TaskItem attribute = new("TestAttribute");
            attribute.SetMetadata("TestParameter", "99");
            attribute.SetMetadata("TestParameter_TypeName", "Foo.Bar");

            ExecuteAndVerifyFailure(
                CreateTask("c#", attribute),
                "MSB3715");
        }

        /// <summary>
        /// A parameter value that cannot be converted to the declared type should cause a failure.
        /// </summary>
        [Fact]
        public void DeclaredTypeCausesConversionFailure()
        {
            TaskItem attribute = new("TestAttribute");
            attribute.SetMetadata("TestParameter", "99");
            attribute.SetMetadata("TestParameter_TypeName", "System.Boolean");

            ExecuteAndVerifyFailure(
                CreateTask("c#", attribute),
                "MSB3716");
        }

        /// <summary>
        /// Parameter value that is too large for the declared data type should cause a failure.
        /// </summary>
        [Fact]
        public void DeclaredTypeCausesOverflow()
        {
            TaskItem attribute = new("TestAttribute");
            attribute.SetMetadata("TestParameter", "1000");
            attribute.SetMetadata("TestParameter_TypeName", "System.Byte");

            ExecuteAndVerifyFailure(
                CreateTask("c#", attribute),
                "MSB3716");
        }

        /// <summary>
        /// The metadata value should convert successfully to an enum.
        /// </summary>
        [Fact]
        public void DeclaredTypeIsEnum()
        {
            TaskItem attribute = new("TestAttribute");
            attribute.SetMetadata("_Parameter1", "Local");
            attribute.SetMetadata("_Parameter1_TypeName", "System.DateTimeKind");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: TestAttribute(System.DateTimeKind.Local)]");
        }

        /// <summary>
        /// The metadata value should convert successfully to a type name in C#.
        /// </summary>
        [Fact]
        public void DeclaredTypeIsTypeInCSharp()
        {
            TaskItem attribute = new("TestAttribute");
            attribute.SetMetadata("_Parameter1", "System.Console");
            attribute.SetMetadata("_Parameter1_TypeName", "System.Type");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: TestAttribute(typeof(System.Console))]");
        }

        /// <summary>
        /// The metadata value should convert successfully to a type name in VB.NET.
        /// </summary>
        [Fact]
        public void DeclaredTypeIsTypeInVB()
        {
            TaskItem attribute = new("TestAttribute");
            attribute.SetMetadata("_Parameter1", "System.Console");
            attribute.SetMetadata("_Parameter1_TypeName", "System.Type");

            ExecuteAndVerifySuccess(
                CreateTask("visualbasic", attribute),
                @"<Assembly: TestAttribute(GetType(System.Console))>");
        }

        /// <summary>
        /// Arrays are not supported for declared types. Literal arguments need to be used instead.
        /// This test confirms that it fails instead of falling back to being treated as a string.
        /// </summary>
        [Fact]
        public void DeclaredTypeOfArrayIsNotSupported()
        {
            TaskItem attribute = new("TestAttribute");
            attribute.SetMetadata("_Parameter1", "1,2,3");
            attribute.SetMetadata("_Parameter1_TypeName", "System.Int32[]");

            ExecuteAndVerifyFailure(
                CreateTask("c#", attribute),
                "MSB3716");
        }

        /// <summary>
        /// The exact code for a positional argument can be specified using
        /// the metadata "_Parameter1_IsLiteral" with a value of "true".
        /// </summary>
        [Fact]
        public void LiteralPositionalParameter()
        {
            TaskItem attribute = new("TestAttribute");
            attribute.SetMetadata("_Parameter1", "42 /* A comment */");
            attribute.SetMetadata("_Parameter1_IsLiteral", "true");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: TestAttribute(42 /* A comment */)]");
        }

        /// <summary>
        /// The exact code for a named argument can be specified using
        /// the metadata "Foo_IsLiteral" with a value of "true".
        /// </summary>
        [Fact]
        public void LiteralNamedParameter()
        {
            TaskItem attribute = new("TestAttribute");
            attribute.SetMetadata("TestParameter", "42 /* A comment */");
            attribute.SetMetadata("TestParameter_IsLiteral", "true");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: TestAttribute(TestParameter=42 /* A comment */)]");
        }

        /// <summary>
        /// The type of a positional argument can be inferred
        /// if the type of the attribute is in mscorlib.
        /// </summary>
        [Fact]
        public void InferredTypeForPositionalParameter()
        {
            TaskItem attribute = new("CLSCompliantAttribute");
            attribute.SetMetadata("_Parameter1", "True");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: CLSCompliantAttribute(true)]");
        }

        /// <summary>
        /// The type of a named argument can be inferred
        /// if the type of the attribute is in mscorlib.
        /// </summary>
        [Fact]
        public void InferredTypeForNamedParameter()
        {
            TaskItem attribute = new("System.Runtime.CompilerServices.InternalsVisibleToAttribute");
            attribute.SetMetadata("_Parameter1", "MyAssembly");
            attribute.SetMetadata("AllInternalsVisible", "True");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute(""MyAssembly"", AllInternalsVisible=true)]");
        }

        /// <summary>
        /// For backward-compatibility, if multiple constructors are found with the same number
        /// of position arguments that was specified in the metadata, then the constructor that
        /// has strings for every parameter should be used.
        /// </summary>
        [Fact]
        public void InferredTypePrefersStringWhenMultipleConstructorsAreFound()
        {
            TaskItem attribute = new("System.Diagnostics.Contracts.ContractOptionAttribute");
            attribute.SetMetadata("_Parameter1", "a");
            attribute.SetMetadata("_Parameter2", "b");
            attribute.SetMetadata("_Parameter3", "false");

            // There are two constructors with three parameters:
            //   * ContractOptionAttribute(string, string, bool)
            //   * ContractOptionAttribute(string, string, string)
            //
            // The first overload would come first when comparing the type names
            // ("System.Boolean" comes before "System.String"), but because we
            // need to remain backward-compatible, the constructor that takes
            // all strings should be preferred over all other constructors.
            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: System.Diagnostics.Contracts.ContractOptionAttribute(""a"", ""b"", ""false"")]");
        }

        /// <summary>
        /// When multiple constructors are found with the same number of
        /// position arguments that was specified in the metadata, and none
        /// of them have parameters of all strings, then the constructors
        /// should be sorted by the names of the parameter types.
        /// The first constructor is then selected.
        /// </summary>
        [Fact]
        public void InferredTypeWithMultipleAttributeConstructorsIsDeterministic()
        {
            TaskItem attribute = new("System.Reflection.AssemblyFlagsAttribute");
            attribute.SetMetadata("_Parameter1", "2");

            // There are three constructors with a single parameter:
            //   * AssemblyFlagsAttribute(int)
            //   * AssemblyFlagsAttribute(uint)
            //   * AssemblyFlagsAttribute(System.Reflection.AssemblyNameFlags)
            //
            // The int overload should be used, because "System.Int32"
            // is alphabetically before any of the other types.
            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: System.Reflection.AssemblyFlagsAttribute(2)]");

            // To prove that it's treating the argument as an int,
            // we can specify an enum value which should fail type
            // conversion and fall back to being used as a string.
            attribute.SetMetadata("_Parameter1", "PublicKey");
            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: System.Reflection.AssemblyFlagsAttribute(""PublicKey"")]");
        }

        /// <summary>
        /// If the attribute type is not in mscorlib, then the
        /// parameter should be treated as a string when the parameter
        /// is not given a declared type or is not marked as a literal.
        /// </summary>
        [Fact]
        public void InferredTypeFallsBackToStringWhenTypeCannotBeInferred()
        {
            // Use an attribute that is not in mscorlib. TypeConverterAttribute is in the "System" assembly.
            TaskItem attribute = new("System.ComponentModel.TypeConverterAttribute");
            attribute.SetMetadata("_Parameter1", "false");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: System.ComponentModel.TypeConverterAttribute(""false"")]");
        }

        /// <summary>
        /// If the parameter type cannot be converted to the inferred type,
        /// then the parameter should be treated as a string.
        /// </summary>
        [Fact]
        public void InferredTypeFallsBackToStringWhenTypeConversionFails()
        {
            TaskItem attribute = new("System.Diagnostics.DebuggableAttribute");
            attribute.SetMetadata("_Parameter1", "True"); // Should be a boolean. Will be converted.
            attribute.SetMetadata("_Parameter2", "42"); // Should be a boolean. Will fail type conversion.

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: System.Diagnostics.DebuggableAttribute(true, ""42"")]");
        }

        /// <summary>
        /// Individual parameters can be typed differently.
        /// </summary>
        [Fact]
        public void UsingInferredDeclaredTypesAndLiteralsInSameAttribute()
        {
            TaskItem attribute = new("System.Diagnostics.Contracts.ContractOptionAttribute");
            attribute.SetMetadata("_Parameter1", "foo");                    // Inferred as string.
            attribute.SetMetadata("_Parameter2", @"""bar"" /* setting */"); // Literal string.
            attribute.SetMetadata("_Parameter2_IsLiteral", "true");
            attribute.SetMetadata("_Parameter3", "False");                  // Typed as boolean.
            attribute.SetMetadata("_Parameter3_TypeName", "System.Boolean");

            ExecuteAndVerifySuccess(
                CreateTask("c#", attribute),
                @"[assembly: System.Diagnostics.Contracts.ContractOptionAttribute(""foo"", ""bar"" /* setting */, false)]");
        }

        private WriteCodeFragment CreateTask(string language, params TaskItem[] attributes)
        {
            return CreateTask(language, new TaskItem(Path.GetTempPath()), null, attributes);
        }

        private WriteCodeFragment CreateTask(string language, TaskItem outputDirectory, TaskItem outputFile, params TaskItem[] attributes)
        {
            return new WriteCodeFragment()
            {
                Language = language,
                OutputDirectory = outputDirectory,
                OutputFile = outputFile,
                AssemblyAttributes = attributes
            };
        }

        private void ExecuteAndVerifySuccess(WriteCodeFragment task, params string[] expectedAttributes)
        {
            MockEngine engine = new(true);
            task.BuildEngine = engine;

            try
            {
                var result = task.Execute();

                // Provide the log output as the user message so that the assertion failure
                // message is a bit more meaningful than just "Expected false to equal true".
                Assert.True(result, engine.Log);

                string content = File.ReadAllText(task.OutputFile.ItemSpec);
                Console.WriteLine(content);

                if (task.Language == "c#")
                {
                    CheckContentCSharp(content, expectedAttributes);
                }
                else
                {
                    CheckContentVB(content, expectedAttributes);
                }
            }
            finally
            {
                if ((task.OutputFile is not null) && File.Exists(task.OutputFile.ItemSpec))
                {
                    File.Delete(task.OutputFile.ItemSpec);
                }
            }
        }

        private void ExecuteAndVerifyFailure(WriteCodeFragment task, string errorCode)
        {
            MockEngine engine = new(true);
            task.BuildEngine = engine;

            try
            {
                var result = task.Execute();

                // Provide the log output as the user message so that the assertion failure
                // message is a bit more meaningful than just "Expected true to equal false".
                Assert.False(result, engine.Log);

                engine.AssertLogContains(errorCode);
            }
            finally
            {
                if ((task.OutputFile is not null) && File.Exists(task.OutputFile.ItemSpec))
                {
                    File.Delete(task.OutputFile.ItemSpec);
                }
            }
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
