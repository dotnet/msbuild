// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for write code fragment task.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;

using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for write code fragment task
    /// </summary>
    [TestFixture]
    public class WriteCodeFragment_Tests
    {
        /// <summary>
        /// Need an available language
        /// </summary>
        [Test]
        public void InvalidLanguage()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "xx";
            task.OutputFile = new TaskItem("foo");
            bool result = task.Execute();

            Assert.AreEqual(false, result);
            engine.AssertLogContains("MSB3712");
        }

        /// <summary>
        /// Need a language
        /// </summary>
        [Test]
        public void NoLanguage()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.OutputFile = new TaskItem("foo");
            bool result = task.Execute();

            Assert.AreEqual(false, result);
            engine.AssertLogContains("MSB3098");
        }

        /// <summary>
        /// Need a location
        /// </summary>
        [Test]
        public void NoFileOrDirectory()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "c#";
            bool result = task.Execute();

            Assert.AreEqual(false, result);
            engine.AssertLogContains("MSB3711");
        }

        /// <summary>
        /// Combine file and directory
        /// </summary>
        [Test]
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

            Assert.AreEqual(true, result);

            string file = Path.Combine(Path.GetTempPath(), "CombineFileDirectory.tmp");
            Assert.AreEqual(file, task.OutputFile.ItemSpec);
            Assert.AreEqual(true, File.Exists(file));
        }

        /// <summary>
        /// Ignore directory if file is rooted
        /// </summary>
        [Test]
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

            Assert.AreEqual(true, result);

            Assert.AreEqual(file, task.OutputFile.ItemSpec);
            Assert.AreEqual(true, File.Exists(file));

            Directory.Delete(folder, true);
        }

        /// <summary>
        /// Given nothing to write, should succeed but
        /// produce no output file
        /// </summary>
        [Test]
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

            Assert.AreEqual(true, result);
            Assert.AreEqual(false, File.Exists(file));
            Assert.AreEqual(null, task.OutputFile);
        }

        /// <summary>
        /// Given nothing to write, should succeed but
        /// produce no output file
        /// </summary>
        [Test]
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

            Assert.AreEqual(true, result);
            Assert.AreEqual(false, File.Exists(file));
            Assert.AreEqual(null, task.OutputFile);
        }

        /// <summary>
        /// Bad file path
        /// </summary>
        [Test]
        public void InvalidFilePath()
        {
            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "c#";
            task.AssemblyAttributes = new TaskItem[] { new TaskItem("aa") };
            task.OutputFile = new TaskItem("||//invalid||");
            bool result = task.Execute();

            Assert.AreEqual(false, result);
            engine.AssertLogContains("MSB3713");
        }

        /// <summary>
        /// Bad directory path
        /// </summary>
        [Test]
        public void InvalidDirectoryPath()
        {
            if (!NativeMethodsShared.IsUnixLike)
            {
                Assert.Ignore("No invalid characters on Unix");
            }

            WriteCodeFragment task = new WriteCodeFragment();
            MockEngine engine = new MockEngine(true);
            task.BuildEngine = engine;
            task.Language = "c#";
            task.AssemblyAttributes = new TaskItem[] { new TaskItem("aa") };
            task.OutputDirectory = new TaskItem("||invalid||");
            bool result = task.Execute();

            Assert.AreEqual(false, result);
            engine.AssertLogContains("MSB3713");
        }

        /// <summary>
        /// Parameterless attribute
        /// </summary>
        [Test]
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

                Assert.AreEqual(true, result);
                Assert.AreEqual(true, File.Exists(file));

                string content = File.ReadAllText(file);
                Console.WriteLine(content);

                Assert.AreEqual(true, content.Contains("using System;"));
                Assert.AreEqual(true, content.Contains("[assembly: System.AssemblyTrademarkAttribute()]"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Test with the VB language
        /// </summary>
        [Test]
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

            Assert.AreEqual(true, result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            Assert.AreEqual(true, content.Contains("Imports System"));
            Assert.AreEqual(true, content.Contains("<Assembly: System.AssemblyTrademarkAttribute()>"));
        }

        /// <summary>
        /// More than one attribute
        /// </summary>
        [Test]
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

            Assert.AreEqual(true, result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            Assert.AreEqual(true, content.Contains(@"[assembly: AssemblyTrademarkAttribute(Name=""Microsoft"")]"));
            Assert.AreEqual(true, content.Contains(@"[assembly: System.AssemblyCultureAttribute(Culture=""en-US"")]"));
        }

        /// <summary>
        /// Specify directory instead
        /// </summary>
        [Test]
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

            Assert.AreEqual(true, result);
            Assert.AreEqual(true, File.Exists(task.OutputFile.ItemSpec));
            Assert.AreEqual(true, String.Equals(task.OutputFile.ItemSpec.Substring(0, Path.GetTempPath().Length), Path.GetTempPath(), StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(".cs", task.OutputFile.ItemSpec.Substring(task.OutputFile.ItemSpec.Length - 3));

            File.Delete(task.OutputFile.ItemSpec);
        }

        /// <summary>
        /// Regular case
        /// </summary>
        [Test]
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

                Assert.AreEqual(true, result);
                Assert.AreEqual(true, File.Exists(file));

                string content = File.ReadAllText(file);
                Console.WriteLine(content);

                Assert.AreEqual(true, content.Contains("using System;"));
                Assert.AreEqual(true, content.Contains("using System.Reflection;"));
                Assert.AreEqual(true, content.Contains(@"[assembly: AssemblyTrademarkAttribute(Company=""Microsoft"", Year=""2009"")]"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// This produces invalid code, but the task works
        /// </summary>
        [Test]
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

            Assert.AreEqual(true, result);

            File.Delete(task.OutputFile.ItemSpec);
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// </summary>
        [Test]
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

            Assert.AreEqual(false, result);

            engine.AssertLogContains("MSB3098");
        }


        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// </summary>
        [Test]
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

            Assert.AreEqual(true, result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            Assert.AreEqual(true, content.Contains(@"[assembly: AssemblyTrademarkAttribute(""Microsoft"", ""2009"")]"));

            File.Delete(task.OutputFile.ItemSpec);
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// If a parameter is skipped, it's an error.
        /// </summary>
        [Test]
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

            Assert.AreEqual(false, result);

            engine.AssertLogContains("MSB3714");
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// This test is for "_ParameterX"
        /// </summary>
        [Test]
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

            Assert.AreEqual(false, result);

            engine.AssertLogContains("MSB3098");
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// This test is for "_Parameter"
        /// </summary>
        [Test]
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

            Assert.AreEqual(false, result);

            engine.AssertLogContains("MSB3098");
        }

        /// <summary>
        /// Some attributes only allow positional constructor arguments.
        /// To set those, use metadata names like "_Parameter1", "_Parameter2" etc.
        /// These can also be combined with named params.
        /// </summary>
        [Test]
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

            Assert.AreEqual(true, result);

            string content = File.ReadAllText(task.OutputFile.ItemSpec);
            Console.WriteLine(content);

            Assert.AreEqual(true, content.Contains(@"[assembly: AssemblyTrademarkAttribute(""Microsoft"", Date=""2009"", Copyright=""(C)"")]"));

            File.Delete(task.OutputFile.ItemSpec);
        }
    }
}



