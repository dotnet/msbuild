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
using System.Text.RegularExpressions;
using System.Text;
using System.Xml.Xsl;
using System.Xml;
using Xunit;

namespace Microsoft.Build.UnitTests
{
#if !MONO
    /// <summary>
    /// These tests run. The temporary output folder for this test is Path.Combine(Path.GetTempPath(), DateTime.Now.Ticks.ToString())
    /// 1. When combination of (xml, xmlfile) x (xsl, xslfile).
    /// 2. When Xsl parameters are missing.
    /// 3. When Xml parameters are missing.
    /// 4. Both missing.
    /// 5. Too many Xml parameters.
    /// 6. Too many Xsl parameters.
    /// 7. Setting Out parameter to file.
    /// 8. Setting Out parameter to screen.
    /// 9. Setting correct "Parameter" parameters for Xsl.
    /// 10. Setting the combination of "Parameter" parameters (Name, Namespace, Value) and testing the cases when they should run ok.
    /// 11. Setting "Parameter" parameter as empty string (should run OK).
    /// 12. Compiled Dll with type information.
    /// 13. Compiled Dll without type information.
    /// 14. Load Xslt with incorrect character as CNAME (load exception).
    /// 15. Missing XmlFile file.
    /// 16. Missing XslFile file.
    /// 17. Missing XsltCompiledDll file.
    /// 18. Bad XML on "Parameter" parameter.
    /// 19. Out parameter pointing to nonexistent location (K:\folder\file.xml)
    /// 20. XslDocument that throws runtime exception.
    /// 21. Passing a dll that has two types to XsltCompiledDll parameter without specifying a type.
    /// </summary>
    sealed public class XslTransformation_Tests
    {
        /// <summary>
        /// The "surround" regex.
        /// </summary>
        private readonly Regex _surroundMatch = new Regex("surround", RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// The contents of xmldocument for tests.
        /// </summary>
        private readonly string _xmlDocument = "<root Name=\"param1\" Value=\"value111\"><abc><cde/></abc></root>";

        /// <summary>
        /// The contents of another xmldocument for tests.
        /// </summary>
        private readonly string _xmlDocument2 = "<root></root>";

        /// <summary>
        /// The contents of xsl document for tests.
        /// </summary>
        private readonly string _xslDocument = "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" xmlns:msxsl=\"urn:schemas-microsoft-com:xslt\" exclude-result-prefixes=\"msxsl\"><xsl:output method=\"xml\" indent=\"yes\"/><xsl:template match=\"@* | node()\"><surround><xsl:copy><xsl:apply-templates select=\"@* | node()\"/></xsl:copy></surround></xsl:template></xsl:stylesheet>";
#if FEATURE_COMPILED_XSL
        /// <summary>
        /// The contents of another xsl document for tests
        /// </summary>
        private readonly string _xslDocument2 = "<?xml version = \"1.0\" ?><xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"><xsl:template match = \"myInclude\"><xsl:apply-templates select = \"document(@path)\"/></xsl:template><xsl:template match = \"@*|node()\"><xsl:copy><xsl:apply-templates select = \"@*|node()\"/></xsl:copy></xsl:template></xsl:stylesheet>";
#endif
        /// <summary>
        /// The contents of xslparameters for tests.
        /// </summary>
        private readonly string _xslParameters = "<Parameter Name=\"param1\" Value=\"1\" /><Parameter Name=\"param2\" Namespace=\"http://eksiduyuru.com\" Value=\"2\" />";

        /// <summary>
        /// The contents of xslt file for testing parameters.
        /// </summary>
        private readonly string _xslParameterDocument = "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" xmlns:msxsl=\"urn:schemas-microsoft-com:xslt\" exclude-result-prefixes=\"msxsl\" xmlns:myns=\"http://eksiduyuru.com\"><xsl:output method=\"xml\" indent=\"yes\"/><xsl:param name=\"param1\" /><xsl:param name=\"myns:param2\" /><xsl:template match=\"/\"><values>param 1: <xsl:value-of select=\"$param1\" />param 2: <xsl:value-of select=\"$myns:param2\" /></values></xsl:template></xsl:stylesheet>";

        /// <summary>
        /// The errorious xsl documents
        /// </summary>
        private readonly string _errorXslDocument = "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" xmlns:msxsl=\"urn:schemas-microsoft-com:xslt\"><xsl:template match=\"/\"><xsl:element name=\"$a\"></xsl:element></xsl:template></xsl:stylesheet>";

        /// <summary>
        /// The errorious xsl document 2.
        /// </summary>
        private readonly string _errorXslDocument2 = "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" xmlns:msxsl=\"urn:schemas-microsoft-com:xslt\" exclude-result-prefixes=\"msxsl\"><xsl:template match=\"/\"><xsl:message terminate=\"yes\">error?</xsl:message></xsl:template></xsl:stylesheet>";

        /// <summary>
        /// When combination of (xml, xmlfile) x (xsl, xslfile).
        /// </summary>
        [Fact]
        public void XmlXslParameters()
        {
            string dir;
            TaskItem[] outputPaths;
            List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
            List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
            MockEngine engine;
            Prepare(out dir, out _, out _, out _, out outputPaths, out xmlInputs, out xslInputs, out engine);

            // Test when Xml and Xsl parameters are correct
            for (int xmi = 0; xmi < xmlInputs.Count; xmi++)
            {
                for (int xsi = 0; xsi < xslInputs.Count; xsi++)
                {
                    XslTransformation t = new XslTransformation();
                    t.BuildEngine = engine;
                    t.OutputPaths = outputPaths;
                    XslTransformation.XmlInput.XmlModes xmlKey = xmlInputs[xmi].Key;
                    object xmlValue = xmlInputs[xmi].Value;
                    XslTransformation.XsltInput.XslModes xslKey = xslInputs[xsi].Key;
                    object xslValue = xslInputs[xsi].Value;

                    switch (xmlKey)
                    {
                        case XslTransformation.XmlInput.XmlModes.Xml:
                            t.XmlContent = (string)xmlValue;
                            break;
                        case XslTransformation.XmlInput.XmlModes.XmlFile:
                            t.XmlInputPaths = (TaskItem[])xmlValue;
                            break;
                        default:
                            Assert.True(false, "Test error");
                            break;
                    }

                    switch (xslKey)
                    {
                        case XslTransformation.XsltInput.XslModes.Xslt:
                            t.XslContent = (string)xslValue;
                            break;
                        case XslTransformation.XsltInput.XslModes.XsltFile:
                            t.XslInputPath = (TaskItem)xslValue;
                            break;
                        case XslTransformation.XsltInput.XslModes.XsltCompiledDll:
                            t.XslCompiledDllPath = (TaskItem)xslValue;
                            break;
                        default:
                            Assert.True(false, "Test error");
                            break;
                    }

                    Assert.True(t.Execute()); // "The test should have passed at the both params correct test"
                }
            }

            CleanUp(dir);
        }

        /// <summary>
        /// When Xsl parameters are missing.
        /// </summary>
        [Fact]
        public void MissingXslParameter()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
            List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out xslCompiledPath, out outputPaths, out xmlInputs, out xslInputs, out engine);

            // test Xsl missing.
            for (int xmi = 0; xmi < xmlInputs.Count; xmi++)
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;

                XslTransformation.XmlInput.XmlModes xmlKey = xmlInputs[xmi].Key;
                object xmlValue = xmlInputs[xmi].Value;
                switch (xmlKey)
                {
                    case XslTransformation.XmlInput.XmlModes.Xml:
                        t.XmlContent = (string)xmlValue;
                        break;
                    case XslTransformation.XmlInput.XmlModes.XmlFile:
                        t.XmlInputPaths = (TaskItem[])xmlValue;
                        break;
                    default:
                        Assert.True(false, "Test error");
                        break;
                }

                Assert.False(t.Execute()); // "The test should fail when there is  missing Xsl params"
                Console.WriteLine(engine.Log);
                Assert.Contains("MSB3701", engine.Log); // "The output should contain MSB3701 error message at missing Xsl params test"
            }

            CleanUp(dir);
        }

        /// <summary>
        /// When Xml parameters are missing.
        /// </summary>
        [Fact]
        public void MissingXmlParameter()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
            List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out xslCompiledPath, out outputPaths, out xmlInputs, out xslInputs, out engine);

            // Test Xml missing.
            for (int xsi = 0; xsi < xslInputs.Count; xsi++)
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;

                XslTransformation.XsltInput.XslModes xslKey = xslInputs[xsi].Key;
                object xslValue = xslInputs[xsi].Value;
                switch (xslKey)
                {
                    case XslTransformation.XsltInput.XslModes.Xslt:
                        t.XslContent = (string)xslValue;
                        break;
                    case XslTransformation.XsltInput.XslModes.XsltFile:
                        t.XslInputPath = (TaskItem)xslValue;
                        break;
                    case XslTransformation.XsltInput.XslModes.XsltCompiledDll:
                        t.XslCompiledDllPath = (TaskItem)xslValue;
                        break;
                    default:
                        Assert.True(false, "Test error");
                        break;
                }

                Assert.False(t.Execute()); // "The test should fail when there is missing Xml params"
                Console.WriteLine(engine.Log);
                Assert.Contains("MSB3701", engine.Log); // "The output should contain MSB3701 error message at missing Xml params test"
                engine.Log = "";
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Both missing.
        /// </summary>
        [Fact]
        public void MissingXmlXslParameter()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
            List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out xslCompiledPath, out outputPaths, out xmlInputs, out xslInputs, out engine);

            // Test both missing.
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;

                Assert.False(t.Execute()); // "The test should fail when there is no params"
                Console.WriteLine(engine.Log);
                Assert.Contains("MSB3701", engine.Log); // "The output should contain MSB3701 error message"
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Too many Xml parameters.
        /// </summary>
        [Fact]
        public void ManyXmlParameters()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
            List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out xslCompiledPath, out outputPaths, out xmlInputs, out xslInputs, out engine);

            // Test too many Xml.
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                t.XmlInputPaths = xmlPaths;
                t.XslContent = _xslDocument;
                Assert.Equal(_xmlDocument, t.XmlContent);
                Assert.Equal(xmlPaths, t.XmlInputPaths);
                Assert.False(t.Execute()); // "The test should fail when there are too many files"
                Console.WriteLine(engine.Log);
                Assert.Contains("MSB3701", engine.Log);
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Too many Xsl parameters.
        /// </summary>
        [Fact]
        public void ManyXslParameters()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
            List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out xslCompiledPath, out outputPaths, out xmlInputs, out xslInputs, out engine);

            // Test too many Xsl.
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                t.XslContent = _xslDocument;
                t.XslInputPath = xslPath;
                Assert.Equal(_xslDocument, t.XslContent);
                Assert.Equal(xslPath, t.XslInputPath);
                Assert.False(t.Execute()); // "The test should fail when there are too many files"
                Console.WriteLine(engine.Log);
                Assert.Contains("MSB3701", engine.Log); // "The output should contain MSB3701 error message at no params test"
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Test out parameter.
        /// </summary>
        [Fact]
        public void OutputTest()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
            List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out xslCompiledPath, out outputPaths, out xmlInputs, out xslInputs, out engine);

            // Test Out
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.XmlContent = _xmlDocument;
                t.XslContent = _xslDocument;
                t.OutputPaths = outputPaths;
                Assert.True(t.Execute()); // "Test out should have given true when executed"
                Assert.Equal(String.Empty, engine.Log); // "The log should be empty"
                Console.WriteLine(engine.Log);
                using (StreamReader sr = new StreamReader(t.OutputPaths[0].ItemSpec))
                {
                    string fileContents = sr.ReadToEnd();
                    MatchCollection mc = _surroundMatch.Matches(fileContents);
                    Assert.Equal(8, mc.Count); // "The file test doesn't match"
                }
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Setting correct "Parameter" parameters for Xsl.
        /// </summary>
        [Fact]
        public void XsltParamatersCorrect()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
            List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out xslCompiledPath, out outputPaths, out xmlInputs, out xslInputs, out engine);

            // Test Correct Xslt Parameters
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                t.XslContent = _xslParameterDocument;
                t.Parameters = _xslParameters;
                t.Execute();
                Console.WriteLine(engine.Log);
                using (StreamReader sr = new StreamReader(t.OutputPaths[0].ItemSpec))
                {
                    string fileContents = sr.ReadToEnd();
                    Assert.Contains("param 1: 1param 2: 2", fileContents);
                }
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Setting the combination of "Parameter" parameters (Name, Namespace, Value) and testing the cases when they should run ok.
        /// </summary>
        [Fact]
        public void XsltParametersIncorrect()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
            List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out xslCompiledPath, out outputPaths, out xmlInputs, out xslInputs, out engine);

            // Test Xslt Parameters
            {
                string[] attrs = new string[] { "Name=\"param2\"", "Namespace=\"http://eksiduyuru.com\"", "Value=\"2\"" };
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

                    XslTransformation t = new XslTransformation();
                    t.BuildEngine = engine;
                    t.OutputPaths = outputPaths;
                    t.XmlContent = _xmlDocument;
                    t.XslContent = _xslParameterDocument;
                    t.Parameters = "<Parameter " + res + "/>";
                    Assert.Equal("<Parameter " + res + "/>", t.Parameters);
                    bool result = t.Execute();
                    Console.WriteLine(engine.Log);

                    if (i == 5 || i == 7)
                    {
                        Assert.True(result); // "Only 5th and 7th values should pass."
                    }
                    else
                    {
                        Assert.False(result); // "Only 5th and 7th values should pass."
                    }
                }
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Setting "Parameter" parameter as empty string (should run OK).
        /// </summary>
        [Fact]
        public void EmptyParameters()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
            List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out xslCompiledPath, out outputPaths, out xmlInputs, out xslInputs, out engine);

            // load empty parameters
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlInputPaths = xmlPaths;
                t.XslInputPath = xslPath;
                t.Parameters = "   ";
                Assert.True(t.Execute()); // "This test should've passed (empty parameters)."
                Console.WriteLine(engine.Log);
            }

            CleanUp(dir);
        }

#if FEATURE_COMPILED_XSL
        /// <summary>
        /// Compiled Dll with type information.
        /// </summary>
        [Fact]
        public void CompiledDllWithType()
        {
            string dir;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out _, out _, out xslCompiledPath, out outputPaths, out _, out _, out engine);

            // Test Compiled DLLs

            // with type specified.
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                xslCompiledPath.ItemSpec = xslCompiledPath.ItemSpec + ";xslt";
                t.XslCompiledDllPath = xslCompiledPath;
                Assert.Equal(xslCompiledPath.ItemSpec, t.XslCompiledDllPath.ItemSpec);
                Assert.True(t.Execute()); // "XsltComiledDll1 execution should've passed"
                Console.WriteLine(engine.Log);
                Assert.DoesNotContain("MSB", engine.Log); // "The log should not contain any errors. (XsltComiledDll1)"
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Compiled Dll without type information.
        /// </summary>
        [Fact]
        public void CompiledDllWithoutType()
        {
            string dir;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out _, out _, out xslCompiledPath, out outputPaths, out _, out _, out engine);

            // without type specified.
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                t.XslCompiledDllPath = xslCompiledPath;
                Assert.True(t.Execute(), "XsltComiledDll2 execution should've passed" + engine.Log);
                Console.WriteLine(engine.Log);
                Assert.False(engine.MockLogger.ErrorCount > 0); // "The log should not contain any errors. (XsltComiledDll2)"
            }

            CleanUp(dir);
        }
#endif

        /// <summary>
        /// Load Xslt with incorrect character as CNAME (load exception).
        /// </summary>
        [Fact]
        public void BadXsltFile()
        {
            string dir;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out _, out _, out _, out outputPaths, out _, out _, out engine);

            // load bad xslt
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                t.XslContent = _errorXslDocument;
                try
                {
                    t.Execute();
                    Console.WriteLine(engine.Log);
                }
                catch (Exception e)
                {
                    Assert.Contains("The '$' character", e.Message);
                }
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Load Xslt with incorrect character as CNAME (load exception).
        /// </summary>
        [Fact]
        public void MissingOutputFile()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
            {
                string dir;
                TaskItem[] xmlPaths;
                TaskItem xslPath;
                TaskItem xslCompiledPath;
                TaskItem[] outputPaths;
                List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs;
                List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs;
                MockEngine engine;
                Prepare(out dir, out xmlPaths, out xslPath, out xslCompiledPath, out outputPaths, out xmlInputs, out xslInputs, out engine);

                // load missing xml
                {
                    XslTransformation t = new XslTransformation();
                    t.BuildEngine = engine;
                    t.XmlInputPaths = xmlPaths;
                    t.XslInputPath = xslPath;
                    Assert.False(t.Execute()); // "This test should've failed (no output)."
                    Console.WriteLine(engine.Log);
                }

                CleanUp(dir);
            }
           );
        }
        /// <summary>
        /// Missing XmlFile file.
        /// </summary>
        [Fact]
        public void MissingXmlFile()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out _, out outputPaths, out _, out _, out engine);

            // load missing xml
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                xmlPaths[0].ItemSpec = xmlPaths[0].ItemSpec + "bad";
                t.XmlInputPaths = xmlPaths;
                t.XslInputPath = xslPath;
                Console.WriteLine(engine.Log);
                Assert.False(t.Execute()); // "This test should've failed (bad xml)."
                Assert.Contains("MSB3703", engine.Log);
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Missing XslFile file.
        /// </summary>
        [Fact]
        public void MissingXsltFile()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out _, out outputPaths, out _, out _, out engine);

            // load missing xsl
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlInputPaths = xmlPaths;
                xslPath.ItemSpec = xslPath.ItemSpec + "bad";
                t.XslInputPath = xslPath;
                Assert.False(t.Execute()); // "This test should've failed (bad xslt)."
                Console.WriteLine(engine.Log);
                Assert.Contains("MSB3704", engine.Log);
            }

            CleanUp(dir);
        }

#if FEATURE_COMPILED_XSL
        /// <summary>
        /// Missing XsltCompiledDll file.
        /// </summary>
        [Fact]
        public void MissingCompiledDllFile()
        {
            string dir;
            TaskItem xslCompiledPath;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out _, out _, out xslCompiledPath, out outputPaths, out _, out _, out engine);

            // missing xsltCompiledDll
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                xslCompiledPath.ItemSpec = xslCompiledPath.ItemSpec + "bad;xslt";
                t.XslCompiledDllPath = xslCompiledPath;
                Assert.False(t.Execute()); // "XsltComiledDllBad execution should've failed"
                Console.WriteLine(engine.Log);
                Assert.Contains("MSB3704", engine.Log);
            }

            CleanUp(dir);
        }
#endif
        /// <summary>
        /// Bad XML on "Parameter" parameter.
        /// </summary>
        [Fact]
        public void BadXmlAsParameter()
        {
            string dir;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out _, out _, out _, out outputPaths, out _, out _, out engine);

            // load bad xml on parameters
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                t.XslContent = _xslParameterDocument;
                t.Parameters = "<<>>";
                try
                {
                    Assert.False(t.Execute()); // "This test should've failed (bad params1)."
                    Console.WriteLine(engine.Log);
                }
                catch (Exception e)
                {
                    Assert.Contains("'<'", e.Message);
                }
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Out parameter pointing to nonexistent location (K:\folder\file.xml)
        /// </summary>
        [Fact]
        public void OutputFileCannotBeWritten()
        {
            string dir;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out _, out _, out _, out outputPaths, out _, out _, out engine);

            // load bad output
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                t.XslContent = _xslDocument;
                t.OutputPaths = new TaskItem[] { new TaskItem("k:\\folder\\file.xml") };
                try
                {
                    Assert.False(t.Execute()); // "This test should've failed (bad output)."
                    Console.WriteLine(engine.Log);
                }
                catch (Exception e)
                {
                    Assert.Contains("MSB3701", e.Message);
                }
            }

            CleanUp(dir);
        }

        /// <summary>
        /// XslDocument that throws runtime exception.
        /// </summary>
        [Fact]
        public void XsltDocumentThrowsError()
        {
            string dir;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out _, out _, out _, out outputPaths, out _, out _, out engine);

            // load error xslDocument
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                t.XslContent = _errorXslDocument2;
                try
                {
                    Assert.False(t.Execute()); // "This test should've failed (xsl with error)."
                    Console.WriteLine(engine.Log);
                }
                catch (Exception e)
                {
                    Assert.Contains("error?", e.Message);
                }
            }

            CleanUp(dir);
        }

#if FEATURE_COMPILED_XSL
        /// <summary>
        /// Passing a dll that has two types to XsltCompiledDll parameter without specifying a type.
        /// </summary>
        [Fact]
        public void CompiledDllWithTwoTypes()
        {
            string dir;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out _, out _, out _, out outputPaths, out _, out _, out engine);

            // doubletype
            string doubleTypePath = Path.Combine(dir, "double.dll");

            CompileDoubleType(doubleTypePath);

            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.OutputPaths = outputPaths;
                t.XmlContent = _xmlDocument;
                t.XslCompiledDllPath = new TaskItem(doubleTypePath);
                try
                {
                    t.Execute();
                    Console.WriteLine(engine.Log);
                }
                catch (Exception e)
                {
                    Assert.Contains("error?", e.Message);
                }

                System.Diagnostics.Debug.WriteLine(engine.Log);
            }

            CleanUp(dir);
        }
#endif
        /// <summary>
        /// Matching XmlInputPaths and OutputPaths
        /// </summary>
        [Fact]
        public void MultipleXmlInputs_Matching()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out _, out outputPaths, out _, out _, out engine);

            var otherXmlPath = new TaskItem(Path.Combine(dir, Guid.NewGuid().ToString()));
            using (StreamWriter sw = new StreamWriter(otherXmlPath.ItemSpec, false))
            {
                sw.Write(_xmlDocument2);
            }

            // xmlPaths have one XmlPath, lets duplicate it
            TaskItem[] xmlMultiPaths = new TaskItem[] { xmlPaths[0], otherXmlPath, xmlPaths[0], xmlPaths[0] };

            // outputPaths have one output path, lets duplicate it
            TaskItem[] outputMultiPaths = new TaskItem[] { new TaskItem(outputPaths[0].ItemSpec + ".1.xml"),
                new TaskItem(outputPaths[0].ItemSpec + ".2.xml"), new TaskItem(outputPaths[0].ItemSpec + ".3.xml"), new TaskItem(outputPaths[0].ItemSpec + ".4.xml") };

            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.XslInputPath = xslPath;
                t.XmlInputPaths = xmlMultiPaths;
                t.OutputPaths = outputMultiPaths;
                Assert.True(t.Execute(), "CompiledDllWithTwoTypes execution should've passed" + engine.Log);
                Console.WriteLine(engine.Log);
                foreach (TaskItem tsk in t.OutputPaths)
                {
                    Assert.True(File.Exists(tsk.ItemSpec), tsk.ItemSpec + " should exist on output dir");
                }

                // The first and second input XML files are not equivalent, so their output files
                // should be different
                Assert.NotEqual(new FileInfo(xmlMultiPaths[0].ItemSpec).Length, new FileInfo(xmlMultiPaths[1].ItemSpec).Length);
                Assert.NotEqual(new FileInfo(outputMultiPaths[0].ItemSpec).Length, new FileInfo(outputMultiPaths[1].ItemSpec).Length);

                System.Diagnostics.Debug.WriteLine(engine.Log);
            }

            CleanUp(dir);
        }

        /// <summary>
        /// Not Matching XmlInputPaths and OutputPaths
        /// </summary>
        [Fact]
        public void MultipleXmlInputs_NotMatching()
        {
            string dir;
            TaskItem[] xmlPaths;
            TaskItem xslPath;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out xmlPaths, out xslPath, out _, out outputPaths, out _, out _, out engine);

            // xmlPaths have one XmlPath, lets duplicate it **4 times **
            TaskItem[] xmlMultiPaths = new TaskItem[] { xmlPaths[0], xmlPaths[0], xmlPaths[0], xmlPaths[0] };

            // outputPaths have one output path, lets duplicate it **3 times **
            TaskItem[] outputMultiPathsShort = new TaskItem[] { new TaskItem(outputPaths[0].ItemSpec + ".1.xml"),
                new TaskItem(outputPaths[0].ItemSpec + ".2.xml"),
                new TaskItem(outputPaths[0].ItemSpec + ".3.xml") };

            TaskItem[] outputMultiPathsLong = new TaskItem[] { new TaskItem(outputPaths[0].ItemSpec + ".1.xml"),
                new TaskItem(outputPaths[0].ItemSpec + ".2.xml"),
                new TaskItem(outputPaths[0].ItemSpec + ".3.xml"),
                new TaskItem(outputPaths[0].ItemSpec + ".4.xml"),
                new TaskItem(outputPaths[0].ItemSpec + ".5.xml") };
            // Short version.
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.XslInputPath = xslPath;
                t.XmlInputPaths = xmlMultiPaths;
                t.OutputPaths = outputMultiPathsShort;
                Assert.False(t.Execute(), "CompiledDllWithTwoTypes execution should've failed" + engine.Log);

                System.Diagnostics.Debug.WriteLine(engine.Log);
            }

            // Long version
            {
                XslTransformation t = new XslTransformation();
                t.BuildEngine = engine;
                t.XslInputPath = xslPath;
                t.XmlInputPaths = xmlMultiPaths;
                t.OutputPaths = outputMultiPathsLong;
                Assert.False(t.Execute(), "CompiledDllWithTwoTypes execution should've failed" + engine.Log);
                Console.WriteLine(engine.Log);

                System.Diagnostics.Debug.WriteLine(engine.Log);
            }

            CleanUp(dir);
        }

#if FEATURE_COMPILED_XSL
        /// <summary>
        /// Validate that the XslTransformation task allows use of the document function
        /// </summary>
        [Fact]
        public void XslDocumentFunctionWorks()
        {
            string dir;
            TaskItem[] outputPaths;
            MockEngine engine;
            Prepare(out dir, out _, out _, out _, out outputPaths, out _, out _, out engine);

            var otherXslPath = new TaskItem(Path.Combine(dir, Guid.NewGuid().ToString() + ".xslt"));
            using (StreamWriter sw = new StreamWriter(otherXslPath.ItemSpec, false))
            {
                sw.Write(_xslDocument2);
            }

            // Initialize first xml file for the XslTransformation task to consume
            var myXmlPath1 = new TaskItem(Path.Combine(dir, "a.xml"));
            using (StreamWriter sw = new StreamWriter(myXmlPath1.ItemSpec, false))
            {
                sw.Write("<document><myInclude path = \"b.xml\"/></document>");
            }

            // Initialize second xml file for the first one to consume
            var myXmlPath2 = new TaskItem(Path.Combine(dir, "b.xml"));
            using (StreamWriter sw = new StreamWriter(myXmlPath2.ItemSpec, false))
            {
                sw.Write("<stuff/>");
            }

            // Validate that execution passes when UseTrustedSettings is true
            XslTransformation t = new XslTransformation();
            t.BuildEngine = engine;
            t.OutputPaths = outputPaths;
            t.XmlInputPaths = new TaskItem[] { myXmlPath1 };
            t.XslInputPath = otherXslPath;
            t.UseTrustedSettings = true;

            Assert.True(t.Execute()); // "Test should have passed and allowed the use of the document() function within the xslt file"

            // Validate that execution fails when UseTrustedSettings is false
            t = new XslTransformation();
            t.BuildEngine = engine;
            t.OutputPaths = outputPaths;
            t.XmlInputPaths = new TaskItem[] { myXmlPath1 };
            t.XslInputPath = otherXslPath;
            t.UseTrustedSettings = false;

            Assert.False(t.Execute()); // "Test should have failed and not allowed the use of the document() function within the xslt file"

            CleanUp(dir);
        }
#endif

        /// <summary>
        /// Prepares the test environment, creates necessary files.
        /// </summary>
        /// <param name="dir">The temp dir</param>
        /// <param name="xmlPaths">The xml file's path</param>
        /// <param name="xslPath">The xsl file's path</param>
        /// <param name="xslCompiledPath">The xsl dll's path</param>
        /// <param name="outputPaths">The output file's path</param>
        /// <param name="xmlInputs">The xml input ways</param>
        /// <param name="xslInputs">The xsl input ways</param>
        /// <param name="engine">The Mock engine</param>
        private void Prepare(out string dir, out TaskItem[] xmlPaths, out TaskItem xslPath, out TaskItem xslCompiledPath, out TaskItem[] outputPaths, out List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>> xmlInputs, out List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>> xslInputs, out MockEngine engine)
        {
            dir = Path.Combine(Path.GetTempPath(), DateTime.Now.Ticks.ToString());
            Directory.CreateDirectory(dir);

            // save XML and XSLT documents.
            xmlPaths = new TaskItem[] { new TaskItem(Path.Combine(dir, "doc.xml")) };
            xslPath = new TaskItem(Path.Combine(dir, "doc.xslt"));
            xslCompiledPath = new TaskItem(Path.Combine(dir, "doc.dll"));
            outputPaths = new TaskItem[] { new TaskItem(Path.Combine(dir, "testout.xml")) };
            using (StreamWriter sw = new StreamWriter(xmlPaths[0].ItemSpec, false))
            {
                sw.Write(_xmlDocument);
                sw.Close();
            }

            using (StreamWriter sw = new StreamWriter(xslPath.ItemSpec, false))
            {
                sw.Write(_xslDocument);
                sw.Close();
            }

            xmlInputs = new List<KeyValuePair<XslTransformation.XmlInput.XmlModes, object>>();
            xslInputs = new List<KeyValuePair<XslTransformation.XsltInput.XslModes, object>>();

            xmlInputs.Add(new KeyValuePair<XslTransformation.XmlInput.XmlModes, object>(XslTransformation.XmlInput.XmlModes.Xml, _xmlDocument));
            xmlInputs.Add(new KeyValuePair<XslTransformation.XmlInput.XmlModes, object>(XslTransformation.XmlInput.XmlModes.XmlFile, xmlPaths));

            xslInputs.Add(new KeyValuePair<XslTransformation.XsltInput.XslModes, object>(XslTransformation.XsltInput.XslModes.Xslt, _xslDocument));
            xslInputs.Add(new KeyValuePair<XslTransformation.XsltInput.XslModes, object>(XslTransformation.XsltInput.XslModes.XsltFile, xslPath));
#if FEATURE_COMPILED_XSL
            Compile(xslPath.ItemSpec, xslCompiledPath.ItemSpec);
#endif

            engine = new MockEngine();
            List<bool> results = new List<bool>();
        }

        /// <summary>
        /// Clean ups the test files
        /// </summary>
        /// <param name="dir">The directory for temp files.</param>
        private void CleanUp(string dir)
        {
            try
            {
                FileUtilities.DeleteWithoutTrailingBackslash(dir, true);
            }
            catch
            {
            }
        }

        #region Compiler

#pragma warning disable 0618 // XmlReaderSettings.ProhibitDtd is obsolete

#if FEATURE_COMPILED_XSL
        /// <summary>
        /// Compiles given stylesheets into an assembly.
        /// </summary>
        private void Compile(string inputFile, string outputFile)
        {
            const string CompiledQueryName = "xslt";
            string outputDir = Path.GetDirectoryName(outputFile) + Path.DirectorySeparatorChar;
            XsltSettings xsltSettings = new XsltSettings(true, true);

            XmlUrlResolver xmlResolver = new XmlUrlResolver();
            XmlReaderSettings readerSettings = new XmlReaderSettings();

            AssemblyBuilder asmBldr;

            readerSettings.ProhibitDtd = false;
            readerSettings.XmlResolver = xmlResolver;

            string scriptAsmPathPrefix = outputDir + Path.GetFileNameWithoutExtension(outputFile) + ".script";

            // Create assembly and module builders
            AssemblyName asmName = new AssemblyName();
            asmName.Name = CompiledQueryName;

            asmBldr = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Save, outputDir);

            // Add custom attribute to assembly marking it as security transparent so that Assert will not be allowed
            // and link demands will be converted to full demands.
            asmBldr.SetCustomAttribute(new CustomAttributeBuilder(typeof(System.Security.SecurityTransparentAttribute).GetConstructor(Type.EmptyTypes), new object[] { }));

            // Mark the assembly with GeneratedCodeAttribute to improve profiling experience
            asmBldr.SetCustomAttribute(new CustomAttributeBuilder(typeof(GeneratedCodeAttribute).GetConstructor(new Type[] { typeof(string), typeof(string) }), new object[] { "XsltCompiler", "2.0.0.0" }));

            ModuleBuilder modBldr = asmBldr.DefineDynamicModule(Path.GetFileName(outputFile), Path.GetFileName(outputFile), true);

            string sourceUri = inputFile;
            string className = Path.GetFileNameWithoutExtension(inputFile);
            string scriptAsmId = "";

            // Always use the .dll extension; otherwise Fusion won't be able to locate this dependency
            string scriptAsmPath = scriptAsmPathPrefix + scriptAsmId + ".dll";

            // Create TypeBuilder and compile the stylesheet into it
            TypeBuilder typeBldr = modBldr.DefineType(CompiledQueryName, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);

            CompilerErrorCollection errors = null;
            try
            {
                using (XmlReader reader = XmlReader.Create(sourceUri, readerSettings))
                {
                    errors = XslCompiledTransform.CompileToType(
                        reader, xsltSettings, xmlResolver, false, typeBldr, scriptAsmPath
                    );
                }
            }
            catch (Exception e)
            {
                Assert.True(false, "Compiler didn't work" + e.ToString());
            }

            asmBldr.Save(Path.GetFileName(outputFile), PortableExecutableKinds.ILOnly, ImageFileMachine.I386);
        }
#endif

#pragma warning restore 0618
#if FEATURE_COMPILED_XSL
        /// <summary>
        /// Creates a dll that has 2 types in it.
        /// </summary>
        /// <param name="outputFile">The dll name.</param>
        private void CompileDoubleType(string outputFile)
        {
            string outputDir = Path.GetDirectoryName(outputFile) + Path.DirectorySeparatorChar;
            const string CompiledQueryName = "xslt";

            AssemblyBuilder asmBldr;

            // Create assembly and module builders
            AssemblyName asmName = new AssemblyName();
            asmName.Name = "assmname";

            asmBldr = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Save, outputDir);

            ModuleBuilder modBldr = asmBldr.DefineDynamicModule(Path.GetFileName(outputFile), Path.GetFileName(outputFile), true);

            // Create TypeBuilder and compile the stylesheet into it
            TypeBuilder typeBldr = modBldr.DefineType(CompiledQueryName, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);

            typeBldr.DefineField("x", typeof(int), FieldAttributes.Private);

            TypeBuilder typeBldr2 = modBldr.DefineType(CompiledQueryName + "2", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);

            typeBldr2.DefineField("x", typeof(int), FieldAttributes.Private);

            typeBldr.CreateType();
            typeBldr2.CreateType();

            asmBldr.Save(Path.GetFileName(outputFile), PortableExecutableKinds.ILOnly, ImageFileMachine.I386);
        }

#endif
        #endregion
    }
#endif
}
