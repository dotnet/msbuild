// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.Build.UnitTests
{
    internal class MyHostObject : ITaskHost
    {
    }

    [TestFixture]
    public class BuildTask_Tests
    {
        /***********************************************************************
         * Test:            SetGetHostObjectInProject
         * Owner:           RGoel
         * 
         * Slightly more advanced unit test that loads a real project, finds the 
         * appropriate task, and sets the host object for it.
         * 
         **********************************************************************/
        [Test]
        public void SetGetHostObjectInProject()
        {
            string projectContents = @"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <Target Name=`Build` >
                        <MakeDir Directories=`c:\rajeev` />
                        <Csc Sources=`foo.cs` />
                    </Target>
                
                </Project>
                ";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            MyHostObject hostObject1 = new MyHostObject();
            MyHostObject hostObject2 = new MyHostObject();

            // Set hostObject1 to the "MakeDir" task, and set hostObject2 to the "Csc" task.
            foreach (Target target in project.Targets)
            {
                foreach (BuildTask myTask in target)
                {
                    if (myTask.Name == "MakeDir")
                    {
                        myTask.HostObject = hostObject1;
                    }
                    else if (myTask.Name == "Csc")
                    {
                        myTask.HostObject = hostObject2;
                    }
                    else
                    {
                        Assertion.Assert("Unknown task", false);
                    }
                }
            }

            bool foundMakeDir = false;
            bool foundCsc = false;

            // Now retrieve the host objects for "MakeDir" and "Csc", and confirm they're the
            // same ones we set originally.
            foreach (Target target in project.Targets)
            {
                foreach (BuildTask myTask in target)
                {
                    if (myTask.Name == "MakeDir")
                    {
                        Assertion.AssertSame(myTask.HostObject, hostObject1);
                        Assertion.Assert(myTask.HostObject != hostObject2);
                        foundMakeDir = true;
                    }
                    else if (myTask.Name == "Csc")
                    {
                        Assertion.AssertSame(myTask.HostObject, hostObject2);
                        Assertion.Assert(myTask.HostObject != hostObject1);
                        foundCsc = true;
                    }
                    else
                    {
                        Assertion.Assert("Unknown task", false);
                    }
                }
            }

            Assertion.Assert(foundMakeDir && foundCsc);
        }

        /// <summary>
        /// We want to warn if there is an @ in the item name in an output tag
        /// as this was probably an error (#177257)
        /// </summary>
        [Test]
        public void CatchAtSignInOutputItemName()
        {
            string projectContents = @"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <CreateItem Include=`x`>
                            <Output TaskParameter=`Include` ItemName=`@(x)`/>
                        </CreateItem>
                    </Target>
                </Project>
                ";

            Project p = ObjectModelHelpers.CreateInMemoryProject(projectContents);
            MockLogger l = new MockLogger();
            p.ParentEngine.RegisterLogger(l);

            p.Build(new string[] { "t" }, null);
            string warning = String.Format(MockLogger.GetString("AtSignInTaskOutputItemName"), "@(x)");
            Assertion.Assert(-1 != l.FullLog.IndexOf(warning));
        }

        /// <summary>
        /// We want to warn if there is an $ in the property name in an output tag
        /// as this was probably an error (#177257)
        /// </summary>
        [Test]
        public void CatchDollarSignInOutputPropertyName()
        {
            string projectContents = @"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <CreateProperty Value=`x`>
                            <Output TaskParameter=`Value` PropertyName=`$(x)`/>
                        </CreateProperty>
                    </Target>
                </Project>
                ";

            Project p = ObjectModelHelpers.CreateInMemoryProject(projectContents);
            MockLogger l = new MockLogger();
            p.ParentEngine.RegisterLogger(l);

            p.Build(new string[] { "t" }, null);
            string warning = String.Format(MockLogger.GetString("DollarSignInTaskOutputPropertyName"), "$(x)");
            Assertion.Assert(-1 != l.FullLog.IndexOf(warning));
        }

        /// <summary>
        /// Regress 567058.
        /// Item expansions using nonexistent metadata can produce items with blank itemspecs -- so
        /// can tasks that do new TaskItem("").
        /// The engine should not barf when retrieving these from tasks.
        /// </summary>
        [Test]
        public void EmptyItemSpecsToCreateItem()
        {
            string projectContents = @"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <CppPreprocess Include=`foo`/>
                    </ItemGroup>
                    <Target Name=`t` >
                        <CreateItem Include=`@(CppPreprocess->'%(OutputFile)')`>
                            <Output TaskParameter=`Include` ItemName=`Include`/>
                        </CreateItem>
                    </Target>
                </Project>
                ";

            Project p = ObjectModelHelpers.CreateInMemoryProject(projectContents);
            MockLogger l = new MockLogger();
            p.ParentEngine.RegisterLogger(l);

            p.Build(new string[] { "t" }, null);
            Assertion.Assert(l.ErrorCount == 0);
            Assertion.Assert(l.WarningCount == 0);
        }

        /// <summary>
        /// Items coming out of tasks should have their itemspecs escaped
        /// </summary>
        [Test]
        public void EscapeOutputItemspecs()
        {
            string projectContents = @"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`t` >
                        <CreateItem Include=`%2520`><!-- should be a literal %20 not a space -->
                            <Output TaskParameter=`Include` ItemName=`Include`/>
                        </CreateItem>
                        <Message Text=`@(include)` Importance=`high`/>
                    </Target>
                </Project>
                ";

            Project p = ObjectModelHelpers.CreateInMemoryProject(projectContents);
            MockLogger l = new MockLogger();
            p.ParentEngine.RegisterLogger(l);

            p.Build(new string[] { "t" }, null);
            Assertion.Assert(l.ErrorCount == 0);
            Assertion.Assert(l.WarningCount == 0);
            l.AssertLogContains("%20");
        }
    }
}
