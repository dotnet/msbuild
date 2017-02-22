// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// <copyright file="XslTransformation_Tests.cs" company="Microsoft">
// Copyright (c) 2015 All Right Reserved
// </copyright>
// <date>2008-12-28</date>
// <summary>The unit tests for XslTransformation buildtask.</summary>

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Xml.Xsl;
using System.Xml;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class XmlPoke_Tests
    {
        private string _xmlFileWithNs = @"<?xml version='1.0' encoding='utf-8'?>
        
<class AccessModifier='public' Name='test' xmlns:s='http://nsurl'>
  <s:variable Type='String' Name='a'></s:variable>
  <s:variable Type='String' Name='b'></s:variable>
  <s:variable Type='String' Name='c'></s:variable>
  <method AccessModifier='public static' Name='GetVal' />
</class>";

        private string _xmlFileNoNs = @"<?xml version='1.0' encoding='utf-8'?>
        
<class AccessModifier='public' Name='test'>
  <variable Type='String' Name='a'></variable>
  <variable Type='String' Name='b'></variable>
  <variable Type='String' Name='c'></variable>
  <method AccessModifier='public static' Name='GetVal' />
</class>";

        [Fact]
        public void PokeWithNamespace()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileWithNs, out xmlInputPath);

            XmlPoke p = new XmlPoke();
            p.BuildEngine = engine;
            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//s:variable/@Name";
            p.Namespaces = "<Namespace Prefix=\"s\" Uri=\"http://nurl\" />";
            p.Value = new TaskItem("Mert");
            p.Execute();

            List<int> positions = new List<int>();
            positions.AddRange(new int[] { 141, 200, 259 });

            string result;
            using (StreamReader sr = new StreamReader(xmlInputPath))
            {
                result = sr.ReadToEnd();
                Regex r = new Regex("Mert");
                MatchCollection mc = r.Matches(result);

                foreach (Match m in mc)
                {
                    Assert.True(positions.Contains(m.Index), "This test should effect 3 positions. There should be 3 occurances of 'Mert'\n" + result);
                }
            }
        }

        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void PokeNoNamespace()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileNoNs, out xmlInputPath);

            XmlPoke p = new XmlPoke();
            p.BuildEngine = engine;
            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//variable/@Name";
            p.Value = new TaskItem("Mert");
            p.Execute();

            List<int> positions = new List<int>();
            positions.AddRange(new int[] { 117, 172, 227 });

            string result;
            using (StreamReader sr = new StreamReader(xmlInputPath))
            {
                result = sr.ReadToEnd();
                Regex r = new Regex("Mert");
                MatchCollection mc = r.Matches(result);

                foreach (Match m in mc)
                {
                    Assert.True(positions.Contains(m.Index), "This test should effect 3 positions. There should be 3 occurances of 'Mert'\n" + result);
                }
            }
        }

        [Fact]
        public void PokeAttribute()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileNoNs, out xmlInputPath);

            XmlPoke p = new XmlPoke();
            p.BuildEngine = engine;
            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//class[1]/@AccessModifier";
            p.Value = new TaskItem("<Test>Testing</Test>");
            p.Execute();
            string result;
            using (StreamReader sr = new StreamReader(xmlInputPath))
            {
                result = sr.ReadToEnd();
                Regex r = new Regex("AccessModifier=\"&lt;Test&gt;Testing&lt;/Test&gt;\"");
                MatchCollection mc = r.Matches(result);

                Assert.Equal(1, mc.Count); // "Should match once"
            }
        }

        [Fact]
        public void PokeChildren()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileNoNs, out xmlInputPath);

            XmlPoke p = new XmlPoke();
            p.BuildEngine = engine;
            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//class/.";
            p.Value = new TaskItem("<Test>Testing</Test>");
            Assert.True(p.Execute(), engine.Log);

            string result;
            using (StreamReader sr = new StreamReader(xmlInputPath))
            {
                result = sr.ReadToEnd();

                Regex r = new Regex("<Test>Testing</Test>");
                MatchCollection mc = r.Matches(result);

                Assert.Equal(1, mc.Count); // "Should match once"
            }
        }

        [Fact]
        public void PokeMissingParams()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileNoNs, out xmlInputPath);

            for (int i = 0; i < 8; i++)
            {
                XmlPoke p = new XmlPoke();
                p.BuildEngine = engine;

                if ((i & 1) == 1)
                {
                    p.XmlInputPath = new TaskItem(xmlInputPath);
                }

                if ((i & 2) == 2)
                {
                    p.Query = "//variable/@Name";
                }

                if ((i & 4) == 4)
                {
                    p.Value = new TaskItem("Mert");
                }

                bool exceptionThrown = false;
                try
                {
                    p.Execute();
                }
                catch (ArgumentNullException)
                {
                    exceptionThrown = true;
                }

                if (i < 7)
                {
                    Assert.True(exceptionThrown); // "Expecting argumentnullexception for the first 7 tests"
                }
                else
                {
                    Assert.False(exceptionThrown); // "Expecting argumentnullexception for the first 7 tests"
                }
            }
        }

        [Fact]
        public void ErrorInNamespaceDecl()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileWithNs, out xmlInputPath);

            XmlPoke p = new XmlPoke();
            p.BuildEngine = engine;
            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//s:variable/@Name";
            p.Namespaces = "<!THIS IS ERROR Namespace Prefix=\"s\" Uri=\"http://nsurl\" />";
            Assert.True(p.Namespaces.Equals("<!THIS IS ERROR Namespace Prefix=\"s\" Uri=\"http://nsurl\" />"));
            p.Value = new TaskItem("Nur");

            bool executeResult = p.Execute();
            Assert.True(engine.Log.Contains("MSB3731"));
            Assert.False(executeResult); // "Execution should've failed"
        }

        [Fact]
        public void PokeNoNSWPrefixedQueryError()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileNoNs, out xmlInputPath);

            XmlPoke p = new XmlPoke();
            p.BuildEngine = engine;

            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//s:variable/@Name";
            p.Value = new TaskItem("Nur");
            Assert.False(p.Execute()); // "Test should've failed"
            Assert.True(engine.Log.Contains("MSB3732"), "Engine log should contain error code MSB3732 " + engine.Log);
        }

        [Fact]
        public void MissingNamespaceParameters()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileWithNs, out xmlInputPath);

            string[] attrs = new string[] { "Prefix=\"s\"", "Uri=\"http://nsurl\"" };
            for (int i = 0; i < Math.Pow(2, attrs.Length); i++)
            {
                string res = "";
                for (int k = 0; k < attrs.Length; k++)
                {
                    if ((i & (int)Math.Pow(2, k)) != 0)
                    {
                        res += attrs[k] + " ";
                    }
                }
                XmlPoke p = new XmlPoke();
                p.BuildEngine = engine;
                p.XmlInputPath = new TaskItem(xmlInputPath);
                p.Query = "//s:variable/@Name";
                p.Namespaces = "<Namespace " + res + " />";
                p.Value = new TaskItem("Nur");

                bool result = p.Execute();

                if (i == 3)
                {
                    Assert.True(result); // "Only 3rd value should pass."
                }
                else
                {
                    Assert.False(result); // "Only 3rd value should pass."
                }
            }
        }

        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void PokeElement()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileNoNs, out xmlInputPath);

            XmlPoke p = new XmlPoke();
            p.BuildEngine = engine;
            p.XmlInputPath = new TaskItem(xmlInputPath);
            Assert.True(p.XmlInputPath.ItemSpec.Equals(xmlInputPath));
            p.Query = "//variable/.";
            Assert.True(p.Query.Equals("//variable/."));
            string valueString = "<testing the=\"element\">With<somewhat complex=\"value\" /></testing>";
            p.Value = new TaskItem(valueString);
            Assert.True(p.Value.ItemSpec.Equals(valueString));

            Assert.True(p.Execute());

            List<int> positions = new List<int>();
            positions.AddRange(new int[] { 126, 249, 372 });

            string result;
            using (StreamReader sr = new StreamReader(xmlInputPath))
            {
                result = sr.ReadToEnd();

                Regex r = new Regex("<testing the=\"element\">With<somewhat complex=\"value\" /></testing>");
                MatchCollection mc = r.Matches(result);

                foreach (Match m in mc)
                {
                    Assert.True(positions.Contains(m.Index), "This test should effect 3 positions. There should be 3 occurances of 'Mert'\n" + result);
                }
            }
        }

        [Fact]
        public void PokeWithoutUsingTask()
        {
            string projectContents = @"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <Target Name='x'>
    <XmlPoke Value='abc' Query='def' XmlInputPath='ghi.jkl' ContinueOnError='true' />
  </Target>
</Project>";

            // The task will error, but ContinueOnError means that it will just be a warning.  
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(projectContents);

            // Verify that the task was indeed found. 
            logger.AssertLogDoesntContain("MSB4036");
        }

        private void Prepare(string xmlFile, out string xmlInputPath)
        {
            string dir = Path.Combine(Path.GetTempPath(), DateTime.Now.Ticks.ToString());
            Directory.CreateDirectory(dir);
            xmlInputPath = dir + Path.DirectorySeparatorChar + "doc.xml";
            using (StreamWriter sw = new StreamWriter(xmlInputPath, false))
            {
                sw.Write(xmlFile);
                sw.Close();
            }
        }
    }
}
