// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

using NUnit.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ToolsetState_Tests
    {
        [Test]
        public void DefaultTasksAreFoundInToolsPath()
        {
            //Note Engine's BinPath is distinct from the ToolsVersion's ToolsPath
            Engine e = new Engine();
            ToolsetState t = new ToolsetState(e, new Toolset("toolsversionname", "c:\\directory1\\directory2"), new GetFiles(this.getFiles), new LoadXmlFromPath(this.loadXmlFromPath));

            TaskRegistry taskRegistry = (TaskRegistry) t.GetTaskRegistry(null);

            string[] expectedRegisteredTasks = { "a1", "a2", "a3", "a4", "b1", "e1", "g1", "g2", "g3" };
            string[] unexpectedRegisteredTasks = { "c1", "d1", "f1", "11", "12", "13", "21" };

            foreach (string expectedRegisteredTask in expectedRegisteredTasks)
            {
                Hashtable registeredTasks;
                Assert.IsTrue(taskRegistry.FindRegisteredTasks(expectedRegisteredTask, true, out registeredTasks),
                              String.Format("Expected task '{0}' registered!", expectedRegisteredTask));
            }
            foreach (string unexpectedRegisteredTask in unexpectedRegisteredTasks)
            {
                Hashtable registeredTasks;
                Assert.IsFalse(taskRegistry.FindRegisteredTasks(unexpectedRegisteredTask, true, out registeredTasks),
                              String.Format("Unexpected task '{0}' registered!", unexpectedRegisteredTask));
            }
        }

        [Test]
        public void WarningLoggedIfNoDefaultTasksFound()
        {
            //Note Engine's BinPath is distinct from the ToolsVersion's ToolsPath
            Engine e = new Engine();
            MockLogger mockLogger = new MockLogger();
            e.RegisterLogger(mockLogger);

            ToolsetState t = new ToolsetState(e, new Toolset("toolsversionname", "c:\\directory1\\directory2\\doesntexist"), new GetFiles(this.getFiles), new LoadXmlFromPath(this.loadXmlFromPath));

            TaskRegistry taskRegistry = (TaskRegistry) t.GetTaskRegistry(null);

            string[] unexpectedRegisteredTasks = { "a1", "a2", "a3", "a4", "b1", "c1", "d1", "e1", "f1", "g1", "g2", "g3", "11", "12", "13", "21" };

            Assert.AreEqual(1, mockLogger.WarningCount, "Expected 1 warning logged!");
            foreach (string unexpectedRegisteredTask in unexpectedRegisteredTasks)
            {
                Hashtable registeredTasks;
                Assert.IsFalse(taskRegistry.FindRegisteredTasks(unexpectedRegisteredTask, true, out registeredTasks),
                               String.Format("Unexpected task '{0}' registered!", unexpectedRegisteredTask));
            }
        }

        [Test]
        public void InvalidToolPath()
        {
            //Note Engine's BinPath is distinct from the ToolsVersion's ToolsPath
            Engine e = new Engine();
            MockLogger mockLogger = new MockLogger();
            e.RegisterLogger(mockLogger);
            ToolsetState t = new ToolsetState(e, new Toolset("toolsversionname", "invalid||path"), new GetFiles(this.getFiles), new LoadXmlFromPath(this.loadXmlFromPath));

            TaskRegistry taskRegistry = (TaskRegistry) t.GetTaskRegistry(null);

            Console.WriteLine(mockLogger.FullLog);
            Assert.AreEqual(1, mockLogger.WarningCount, "Expected a warning for invalid character in toolpath"); 
        }

        public ToolsetState_Tests()
        {
            defaultTasksFileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DefaultTasksFile defaultTasksFileCandidate in defaultTasksFileCandidates)
            {
                defaultTasksFileMap.Add(defaultTasksFileCandidate.Path, defaultTasksFileCandidate.XmlContents);
            }
        }

        private string[] getFiles(string path, string pattern)
        {
            // Cause an exception if the path is invalid
            Path.GetFileName(path);

            string pathWithoutTrailingSlash = path.EndsWith("\\") ? path.Substring(0, path.Length - 1) : path;
            //NOTE: the Replace calls below are a very minimal attempt to convert a basic, cmd.exe-style wildcard
            //into something Regex.IsMatch will know how to use.
            string finalPattern = "^" + pattern.Replace(".", "\\.").Replace("*", "[\\w\\W]*") + "$";

            List<string> matches = new List<string>(defaultTasksFileMap.Keys);
            matches.RemoveAll(
                delegate(string candidate)
                {
                    bool sameFolder = (0 == String.Compare(Path.GetDirectoryName(candidate),
                                                           pathWithoutTrailingSlash,
                                                           StringComparison.OrdinalIgnoreCase));
                    return !sameFolder || !Regex.IsMatch(Path.GetFileName(candidate), finalPattern);
                });
            return matches.ToArray();
        }

        private XmlDocument loadXmlFromPath(string path)
        {
            string xmlContents = defaultTasksFileMap[path];
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(xmlContents);
            return xmlDocument;
        }

        private readonly Dictionary<string, string> defaultTasksFileMap;

        private DefaultTasksFile[] defaultTasksFileCandidates =
            { new DefaultTasksFile(
                      "c:\\directory1\\directory2\\a.tasks",
                      @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <UsingTask TaskName='a1' AssemblyName='a' />
                            <UsingTask TaskName='a2' AssemblyName='a' />
                            <UsingTask TaskName='a3' AssemblyName='a' />
                            <UsingTask TaskName='a4' AssemblyName='a' />
                       </Project>"),
              new DefaultTasksFile("c:\\directory1\\directory2\\b.tasks",
                      @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <UsingTask TaskName='b1' AssemblyName='b' />
                       </Project>"),
              new DefaultTasksFile("c:\\directory1\\directory2\\c.tasksfile",
                      @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <UsingTask TaskName='c1' AssemblyName='c' />
                       </Project>"),
              new DefaultTasksFile("c:\\directory1\\directory2\\directory3\\d.tasks",
                      @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <UsingTask TaskName='d1' AssemblyName='d' />
                       </Project>"),
              new DefaultTasksFile("c:\\directory1\\directory2\\e.tasks",
                      @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <UsingTask TaskName='e1' AssemblyName='e' />
                       </Project>"),
              new DefaultTasksFile("d:\\directory1\\directory2\\f.tasks",
                      @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <UsingTask TaskName='f1' AssemblyName='f' />
                       </Project>"), 
              new DefaultTasksFile("c:\\directory1\\directory2\\g.custom.tasks",
                      @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <UsingTask TaskName='g1' AssemblyName='g' />
                            <UsingTask TaskName='g2' AssemblyName='g' />
                            <UsingTask TaskName='g3' AssemblyName='g' />
                       </Project>"),
              new DefaultTasksFile("c:\\somepath\\1.tasks",
                      @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <UsingTask TaskName='11' AssemblyName='1' />
                            <UsingTask TaskName='12' AssemblyName='1' />
                            <UsingTask TaskName='13' AssemblyName='1' />
                       </Project>"),
              new DefaultTasksFile("c:\\somepath\\2.tasks",
                      @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <UsingTask TaskName='21' AssemblyName='2' />
                       </Project>") };

        public struct DefaultTasksFile
        {
            public string Path;
            public string XmlContents;
            public DefaultTasksFile(string path, string xmlContents)
            {
                this.Path = path;
                this.XmlContents = xmlContents;
            }
        }
    }
}
