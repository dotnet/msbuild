// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using System.Linq;
using System.Text;
using System.Xml;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class XmlPoke_Tests
    {
        private const string XmlNamespaceUsedByTests = "http://nsurl";

        private string _xmlFileWithNs = $@"<?xml version='1.0' encoding='utf-8'?>
        
<class AccessModifier='public' Name='test' xmlns:s='{XmlNamespaceUsedByTests}'>
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
            const string query = "//s:variable/@Name";

            XmlDocument xmlDocument = ExecuteXmlPoke(
                query: query,
                useNamespace: true,
                value: "Mert");

            XmlNamespaceManager ns = new XmlNamespaceManager(xmlDocument.NameTable);
            ns.AddNamespace("s", XmlNamespaceUsedByTests);

            List<XmlAttribute> nodes = xmlDocument.SelectNodes(query, ns)?.Cast<XmlAttribute>().ToList();

            Assert.True(nodes?.Count == 3, $"There should be 3 <variable /> elements with a Name attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            Assert.True(nodes?.All(i => i.Value.Equals("Mert")), $"All <variable /> elements should have Name=\"Mert\" {Environment.NewLine}{xmlDocument.OuterXml}");
        }

        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void PokeNoNamespace()
        {
            const string query = "//variable/@Name";
            const string value = "Mert";

            XmlDocument xmlDocument = ExecuteXmlPoke(query: query, value: value);

            List<XmlAttribute> nodes = xmlDocument.SelectNodes(query)?.Cast<XmlAttribute>().ToList();

            Assert.True(nodes?.Count == 3, $"There should be 3 <variable /> elements with a Name attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            Assert.True(nodes?.All(i => i.Value.Equals(value)), $"All <variable /> elements should have Name=\"{value}\" {Environment.NewLine}{xmlDocument.OuterXml}");
        }

        [Fact]
        public void PokeAttribute()
        {
            const string query = "//class[1]/@AccessModifier";
            const string value = "<Test>Testing</Test>";

            XmlDocument xmlDocument = ExecuteXmlPoke(query: query, value: value);

            List<XmlAttribute> nodes = xmlDocument.SelectNodes(query)?.Cast<XmlAttribute>().ToList();

            Assert.True(nodes?.Count == 1, $"There should be 1 <class /> element with an AccessModifier attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            Assert.Equal(value, nodes?.First().Value);
        }

        [Fact]
        public void PokeChildren()
        {
            const string query = "//class/.";
            const string value = "<Test>Testing</Test>";

            XmlDocument xmlDocument = ExecuteXmlPoke(query: query, value: value);

            List<XmlElement> nodes = xmlDocument.SelectNodes(query)?.Cast<XmlElement>().ToList();

            Assert.True(nodes?.Count == 1, $"There should be 1 <class /> element {Environment.NewLine}{xmlDocument.OuterXml}");

            var testNodes = nodes?.First().ChildNodes.Cast<XmlElement>().ToList();

            Assert.True(testNodes?.Count == 1, $"There should be 1 <class /> element with one child Test element {Environment.NewLine}{xmlDocument.OuterXml}");

            Assert.Equal("Testing", testNodes?.First().InnerText);
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
            Assert.Equal("<!THIS IS ERROR Namespace Prefix=\"s\" Uri=\"http://nsurl\" />", p.Namespaces);
            p.Value = new TaskItem("Nur");

            bool executeResult = p.Execute();
            Assert.Contains("MSB3731", engine.Log);
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
            const string query = "//variable/.";
            const string value = "<testing the=\"element\">With<somewhat complex=\"value\" /></testing>";

            XmlDocument xmlDocument = ExecuteXmlPoke(query: query, value: value);

            List<XmlElement> nodes = xmlDocument.SelectNodes(query)?.Cast<XmlElement>().ToList();

            Assert.True(nodes?.Count == 3, $"There should be 3 <variable/> elements {Environment.NewLine}{xmlDocument.OuterXml}");

            foreach (var node in nodes)
            {
                Assert.Equal(value, node.InnerXml);
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
            File.WriteAllText(xmlInputPath, xmlFile);
        }

        /// <summary>
        /// Executes an <see cref="XmlPoke"/> task with the specified arguments.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <param name="useNamespace"><code>true</code> to use namespaces, otherwise <code>false</code> (Default).</param>
        /// <param name="value">The value to use.</param>
        /// <returns>An <see cref="XmlDocument"/> containing the resulting XML after the XmlPoke task has executed.</returns>
        private XmlDocument ExecuteXmlPoke(string query, bool useNamespace = false, string value = null)
        {
            MockEngine engine = new MockEngine(true);

            string xmlInputPath;
            Prepare(useNamespace ? _xmlFileWithNs : _xmlFileNoNs, out xmlInputPath);

            XmlPoke p = new XmlPoke
            {
                BuildEngine = engine,
                XmlInputPath = new TaskItem(xmlInputPath),
                Query = query,
                Namespaces = useNamespace ? $"<Namespace Prefix=\"s\" Uri=\"{XmlNamespaceUsedByTests}\" />" : null,
                Value = value == null ? null : new TaskItem(value)
            };
            Assert.True(p.Execute(), engine.Log);

            string result = File.ReadAllText(xmlInputPath);

            XmlDocument xmlDocument = new XmlDocument();

            xmlDocument.LoadXml(result);

            return xmlDocument;
        }
    }
}
