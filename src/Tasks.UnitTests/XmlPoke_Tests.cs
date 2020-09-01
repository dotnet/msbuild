// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class XmlPoke_Tests
    {
        private const string XmlNamespaceUsedByTests = "http://nsurl";

        private const string _xmlFileWithNs = @"<?xml version='1.0' encoding='utf-8'?>
        
<class AccessModifier='public' Name='test' xmlns:s='" + XmlNamespaceUsedByTests + @"'>
  <s:variable Type='String' Name='a'></s:variable>
  <s:variable Type='String' Name='b'></s:variable>
  <s:variable Type='String' Name='c'></s:variable>
  <method AccessModifier='public static' Name='GetVal' />
</class>";

        private const string _xmlFileNoNs = @"<?xml version='1.0' encoding='utf-8'?>
        
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

            nodes.ShouldNotBeNull($"There should be <variable /> elements with a Name attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            nodes.Count.ShouldBe(3, $"There should be 3 <variable /> elements with a Name attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            nodes.ShouldAllBe(i => i.Value.Equals("Mert"), $"All <variable /> elements should have Name=\"Mert\" {Environment.NewLine}{xmlDocument.OuterXml}");
        }

        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void PokeNoNamespace()
        {
            const string query = "//variable/@Name";
            const string value = "Mert";

            XmlDocument xmlDocument = ExecuteXmlPoke(query: query, value: value);

            List<XmlAttribute> nodes = xmlDocument.SelectNodes(query)?.Cast<XmlAttribute>().ToList();

            nodes.ShouldNotBeNull($"There should be <variable /> elements with a Name attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            nodes.Count.ShouldBe(3, $"There should be 3 <variable /> elements with a Name attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            nodes.ShouldAllBe(i => i.Value.Equals(value), $"All <variable /> elements should have Name=\"{value}\" {Environment.NewLine}{xmlDocument.OuterXml}");
        }

        [Fact]
        public void PokeAttribute()
        {
            const string query = "//class[1]/@AccessModifier";
            const string value = "<Test>Testing</Test>";

            XmlDocument xmlDocument = ExecuteXmlPoke(query: query, value: value);

            List<XmlAttribute> nodes = xmlDocument.SelectNodes(query)?.Cast<XmlAttribute>().ToList();

            nodes.ShouldNotBeNull($"There should be <class /> elements with an AccessModifier attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            nodes.Count.ShouldBe(1, $"There should be 1 <class /> element with an AccessModifier attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            nodes[0].Value.ShouldBe(value);
        }

        [Fact]
        public void PokeChildren()
        {
            const string query = "//class/.";
            const string value = "<Test>Testing</Test>";

            XmlDocument xmlDocument = ExecuteXmlPoke(query: query, value: value);

            List<XmlElement> nodes = xmlDocument.SelectNodes(query)?.Cast<XmlElement>().ToList();

            nodes.ShouldNotBeNull($"There should be <class /> elements {Environment.NewLine}{xmlDocument.OuterXml}");

            nodes.Count.ShouldBe(1, $"There should be 1 <class /> element {Environment.NewLine}{xmlDocument.OuterXml}");

            var testNodes = nodes?.First().ChildNodes.Cast<XmlElement>().ToList();

            testNodes.ShouldNotBeNull($"There should be <class /> elements with one child Test element {Environment.NewLine}{xmlDocument.OuterXml}");

            testNodes.Count.ShouldBe(1, $"There should be 1 <class /> element with one child Test element {Environment.NewLine}{xmlDocument.OuterXml}");

            testNodes[0].InnerText.ShouldBe("Testing");
        }

        [Fact]
        public void PokeAttributeWithCondition()
        {
            const string original = "b";
            const string value = "x";
            const string queryTemplate = "/class/variable[@Name='{0}']/@Name";

            XmlDocument xmlDocument = ExecuteXmlPoke(query: string.Format(queryTemplate, original), value: value);

            List<XmlAttribute> nodes = xmlDocument.SelectNodes(string.Format(queryTemplate, value))?.Cast<XmlAttribute>().ToList();

            nodes.ShouldNotBeNull($"There should be <class /> element with an AccessModifier attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            nodes.Count.ShouldBe(1, $"There should be 1 <class /> element with an AccessModifier attribute {Environment.NewLine}{xmlDocument.OuterXml}");

            nodes[0].Value.ShouldBe(value);
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

                // "Expecting argumentnullexception for the first 7 tests"
                if (i < 7)
                {
                    Should.Throw<ArgumentNullException>(() => p.Execute());
                }
                else
                {
                    Should.NotThrow(() => p.Execute());
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
            p.Namespaces.ShouldBe("<!THIS IS ERROR Namespace Prefix=\"s\" Uri=\"http://nsurl\" />");
            p.Value = new TaskItem("Nur");

            p.Execute().ShouldBeFalse(); // "Execution should've failed"
            engine.AssertLogContains("MSB3731");
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
            p.Execute().ShouldBeFalse(); // "Test should've failed"
            engine.AssertLogContains("MSB3732");
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
                    result.ShouldBeTrue(); // "Only 3rd value should pass."
                }
                else
                {
                    result.ShouldBeFalse(); // "Only 3rd value should pass."
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

            nodes.ShouldNotBeNull($"There should be <variable/> elements {Environment.NewLine}{xmlDocument.OuterXml}");

            nodes.Count.ShouldBe(3, $"There should be 3 <variable/> elements {Environment.NewLine}{xmlDocument.OuterXml}");

            foreach (var node in nodes)
            {
                node.InnerXml.ShouldBe(value);
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

        private static void Prepare(string xmlFile, out string xmlInputPath)
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
        private static XmlDocument ExecuteXmlPoke(string query, bool useNamespace = false, string value = null)
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
