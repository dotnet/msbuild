// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

    using System;
    using System.Collections;

    using NUnit.Framework;
    using System.IO;

    using Microsoft.Build.BuildEngine;
    using System.Threading;
    using System.Reflection;

    namespace Microsoft.Build.UnitTests
    {
        [TestFixture]
        public class PropertyGroupTests
        {
            /// <summary>
            /// </summary>
            /// <owner>SumedhK</owner>
            [Test]
            public void ImportOutputProperties()
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();
                pg.SetProperty("foo", "fooval");
                pg.SetProperty(new BuildProperty("bar", "barval", PropertyType.EnvironmentProperty));
                pg.SetProperty(new BuildProperty("baz", "bazval", PropertyType.GlobalProperty));
                pg.SetProperty(new BuildProperty("caz", "cazval", PropertyType.ImportedProperty));
                pg.SetProperty(new BuildProperty("barb", "barbval", PropertyType.OutputProperty));

                BuildPropertyGroup pgo = new BuildPropertyGroup();
                pgo.SetProperty(new BuildProperty("foo", "fooout", PropertyType.OutputProperty));
                pgo.SetProperty(new BuildProperty("bar", "barout", PropertyType.OutputProperty));
                pgo.SetProperty(new BuildProperty("baz", "bazout", PropertyType.OutputProperty));
                pgo.SetProperty(new BuildProperty("caz", "cazout", PropertyType.OutputProperty));
                pgo.SetProperty(new BuildProperty("barb", "barbout", PropertyType.OutputProperty));
                pgo.SetProperty(new BuildProperty("gaz", "gazout", PropertyType.OutputProperty));

                pg.ImportProperties(pgo);

                Assertion.AssertEquals(6, pg.Count);
                Assertion.AssertEquals("fooout", pg["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barout", pg["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazout", pg["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazout", pg["caz"].FinalValueEscaped);
                Assertion.AssertEquals("barbout", pg["barb"].FinalValueEscaped);
                Assertion.AssertEquals("gazout", pg["gaz"].FinalValueEscaped);

                pg.SetProperty(new BuildProperty("foo", "fooout2", PropertyType.OutputProperty));
                pg.SetProperty(new BuildProperty("gaz", "gazout2", PropertyType.OutputProperty));

                Assertion.AssertEquals("fooout2", pg["foo"].FinalValueEscaped);
                Assertion.AssertEquals("gazout2", pg["gaz"].FinalValueEscaped);

                pg.RemoveProperty("baz");
                pg.RevertAllOutputProperties();

                Assertion.AssertEquals(3, pg.Count);
                Assertion.AssertEquals("fooval", pg["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pg["bar"].FinalValueEscaped);
                Assertion.AssertNull(pg["baz"]);
                Assertion.AssertEquals("cazval", pg["caz"].FinalValueEscaped);
                Assertion.AssertNull(pg["barb"]);
            }

            /// <summary>
            /// </summary>
            /// <owner>SumedhK</owner>
            [Test]
            public void ClonePropertyGroup()
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();
                pg.SetProperty("foo", "fooval");
                pg.SetProperty(new BuildProperty("bar", "barval", PropertyType.EnvironmentProperty));
                pg.SetProperty(new BuildProperty("baz", "bazval", PropertyType.GlobalProperty));
                pg.SetProperty(new BuildProperty("caz", "cazval", PropertyType.ImportedProperty));
                pg.SetProperty(new BuildProperty("barb", "barbval", PropertyType.OutputProperty));

                pg.SetProperty(new BuildProperty("foo", "fooout", PropertyType.OutputProperty));
                pg.SetProperty(new BuildProperty("barb", "barbout", PropertyType.OutputProperty));
                pg.SetProperty(new BuildProperty("gaz", "gazout", PropertyType.OutputProperty));
                pg.SetProperty(new BuildProperty("foo", "fooout2", PropertyType.OutputProperty));

                BuildPropertyGroup pgsc = pg.Clone(false /* shallow clone */);
                BuildPropertyGroup pgdc = pg.Clone(true /* deep clone */);

                Assertion.AssertEquals(6, pg.Count);
                Assertion.AssertEquals("fooout2", pg["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pg["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pg["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pg["caz"].FinalValueEscaped);
                Assertion.AssertEquals("barbout", pg["barb"].FinalValueEscaped);
                Assertion.AssertEquals("gazout", pg["gaz"].FinalValueEscaped);

                Assertion.AssertEquals(6, pgsc.Count);
                Assertion.AssertEquals("fooout2", pgsc["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pgsc["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pgsc["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pgsc["caz"].FinalValueEscaped);
                Assertion.AssertEquals("barbout", pgsc["barb"].FinalValueEscaped);
                Assertion.AssertEquals("gazout", pgsc["gaz"].FinalValueEscaped);

                Assertion.AssertEquals(6, pgdc.Count);
                Assertion.AssertEquals("fooout2", pgdc["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pgdc["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pgdc["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pgdc["caz"].FinalValueEscaped);
                Assertion.AssertEquals("barbout", pgdc["barb"].FinalValueEscaped);
                Assertion.AssertEquals("gazout", pgdc["gaz"].FinalValueEscaped);

                pg.RevertAllOutputProperties();

                Assertion.AssertEquals(4, pg.Count);
                Assertion.AssertEquals("fooval", pg["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pg["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pg["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pg["caz"].FinalValueEscaped);
                Assertion.AssertNull(pg["barb"]);
                Assertion.AssertNull(pg["gaz"]);

                Assertion.AssertEquals(6, pgsc.Count);
                Assertion.AssertEquals("fooout2", pgsc["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pgsc["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pgsc["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pgsc["caz"].FinalValueEscaped);
                Assertion.AssertEquals("barbout", pgsc["barb"].FinalValueEscaped);
                Assertion.AssertEquals("gazout", pgsc["gaz"].FinalValueEscaped);

                Assertion.AssertEquals(6, pgdc.Count);
                Assertion.AssertEquals("fooout2", pgdc["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pgdc["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pgdc["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pgdc["caz"].FinalValueEscaped);
                Assertion.AssertEquals("barbout", pgdc["barb"].FinalValueEscaped);
                Assertion.AssertEquals("gazout", pgdc["gaz"].FinalValueEscaped);

                pgsc.RevertAllOutputProperties();

                Assertion.AssertEquals(4, pg.Count);
                Assertion.AssertEquals("fooval", pg["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pg["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pg["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pg["caz"].FinalValueEscaped);
                Assertion.AssertNull(pg["barb"]);
                Assertion.AssertNull(pg["gaz"]);

                Assertion.AssertEquals(4, pgsc.Count);
                Assertion.AssertEquals("fooval", pgsc["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pgsc["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pgsc["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pgsc["caz"].FinalValueEscaped);
                Assertion.AssertNull(pgsc["barb"]);
                Assertion.AssertNull(pgsc["gaz"]);

                Assertion.AssertEquals(6, pgdc.Count);
                Assertion.AssertEquals("fooout2", pgdc["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pgdc["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pgdc["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pgdc["caz"].FinalValueEscaped);
                Assertion.AssertEquals("barbout", pgdc["barb"].FinalValueEscaped);
                Assertion.AssertEquals("gazout", pgdc["gaz"].FinalValueEscaped);

                pgdc.RevertAllOutputProperties();

                Assertion.AssertEquals(4, pg.Count);
                Assertion.AssertEquals("fooval", pg["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pg["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pg["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pg["caz"].FinalValueEscaped);
                Assertion.AssertNull(pg["barb"]);
                Assertion.AssertNull(pg["gaz"]);

                Assertion.AssertEquals(4, pgsc.Count);
                Assertion.AssertEquals("fooval", pgsc["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pgsc["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pgsc["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pgsc["caz"].FinalValueEscaped);
                Assertion.AssertNull(pgsc["barb"]);
                Assertion.AssertNull(pgsc["gaz"]);

                Assertion.AssertEquals(4, pgdc.Count);
                Assertion.AssertEquals("fooval", pgdc["foo"].FinalValueEscaped);
                Assertion.AssertEquals("barval", pgdc["bar"].FinalValueEscaped);
                Assertion.AssertEquals("bazval", pgdc["baz"].FinalValueEscaped);
                Assertion.AssertEquals("cazval", pgdc["caz"].FinalValueEscaped);
                Assertion.AssertNull(pgdc["barb"]);
                Assertion.AssertNull(pgdc["gaz"]);
            }

            /// <summary>
            /// Tests the IsEquivalent method on BuildPropertyGroup, when the two PGs have a
            /// different number of properties.
            /// </summary>
            /// <owner>RGoel</owner>
            [Test]
            public void PropertyGroupIsEquivalent_DifferentCount()
            {
                BuildPropertyGroup pg1 = new BuildPropertyGroup();
                BuildPropertyGroup pg2 = new BuildPropertyGroup();

                pg1.SetProperty("Elmo", "Red");
                pg1.SetProperty("BigBird", "Yellow");
                pg1.SetProperty("OscartheGrouch", "Green");

                pg2.SetProperty("Elmo", "Red");
                pg2.SetProperty("BigBird", "Yellow");

                // The two property bags are not equivalent.
                Assertion.Assert(!pg1.IsEquivalent(pg2));
            }

            /// <summary>
            /// Tests the IsEquivalent method on BuildPropertyGroup, when the two PGs have differing
            /// cases on the property names.
            /// </summary>
            /// <owner>RGoel</owner>
            [Test]
            public void PropertyGroupIsEquivalent_DifferentCaseOnName()
            {
                BuildPropertyGroup pg1 = new BuildPropertyGroup();
                BuildPropertyGroup pg2 = new BuildPropertyGroup();

                pg1.SetProperty("oscarthegrouch", "Green");
                pg1.SetProperty("bigbird", "Yellow");
                pg1.SetProperty("elmo", "Red");

                pg2.SetProperty("Elmo", "Red");
                pg2.SetProperty("BigBird", "Yellow");
                pg2.SetProperty("OscartheGrouch", "Green");

                // The two property bags are not equivalent.
                Assertion.Assert(pg1.IsEquivalent(pg2));
            }

            /// <summary>
            /// Tests the IsEquivalent method on BuildPropertyGroup, when the two PGs have differing
            /// cases on the property values.
            /// </summary>
            /// <owner>RGoel</owner>
            [Test]
            public void PropertyGroupIsEquivalent_DifferentCaseOnValue()
            {
                BuildPropertyGroup pg1 = new BuildPropertyGroup();
                BuildPropertyGroup pg2 = new BuildPropertyGroup();

                pg1.SetProperty("Elmo", "Red");
                pg1.SetProperty("BigBird", "Yellow");
                pg1.SetProperty("OscartheGrouch", "green");

                pg2.SetProperty("Elmo", "Red");
                pg2.SetProperty("BigBird", "Yellow");
                pg2.SetProperty("OscartheGrouch", "Green");

                // The two property bags are not equivalent.
                Assertion.Assert(!pg1.IsEquivalent(pg2));
            }

            /// <summary>
            /// Tests the IsEquivalent method on BuildPropertyGroup, when one of the PGs has a blank
            /// property value.
            /// </summary>
            /// <owner>RGoel</owner>
            [Test]
            public void PropertyGroupIsEquivalent_BlankPropertyValue()
            {
                BuildPropertyGroup pg1 = new BuildPropertyGroup();
                BuildPropertyGroup pg2 = new BuildPropertyGroup();

                pg1.SetProperty("Elmo", "Red");
                pg1.SetProperty("BigBird", "Yellow");
                pg1.SetProperty("OscartheGrouch", "Green");

                pg2.SetProperty("Elmo", "Red");
                pg2.SetProperty("BigBird", "");
                pg2.SetProperty("OscartheGrouch", "Green");

                // The two property bags are not equivalent.
                Assertion.Assert(!pg1.IsEquivalent(pg2));
            }

            /// <summary>
            /// Test that the ImportInitialProperties method imports the properties from the given
            /// BuildPropertyGroup instances in the correct order.
            /// </summary>
            [Test]
            public void ImportInitialPropertiesHasCorrectPrecedence()
            {
                BuildPropertyGroup environmentProperties = new BuildPropertyGroup();
                environmentProperties.SetProperty("Property1", "Value1");
                environmentProperties.SetProperty("Property2", "Value2");
                environmentProperties.SetProperty("Property3", "Value3");
                environmentProperties.SetProperty("Property4", "Value4");
                BuildPropertyGroup reservedProperties = new BuildPropertyGroup();
                reservedProperties.SetProperty("Property2", "Value5");
                reservedProperties.SetProperty("Property3", "Value6");
                reservedProperties.SetProperty("Property4", "Value7");
                BuildPropertyGroup toolsVersionDependentProperties = new BuildPropertyGroup();
                toolsVersionDependentProperties.SetProperty("Property3", "Value8");
                toolsVersionDependentProperties.SetProperty("Property4", "Value9");
                BuildPropertyGroup globalProperties = new BuildPropertyGroup();
                globalProperties.SetProperty("Property4", "Value10");

                BuildPropertyGroup evaluatedProperties = new BuildPropertyGroup();
                evaluatedProperties.ImportInitialProperties(environmentProperties, reservedProperties, toolsVersionDependentProperties, globalProperties);

                Assertion.AssertEquals("Value10", evaluatedProperties["Property4"].Value);
                Assertion.AssertEquals("Value8",  evaluatedProperties["Property3"].Value);
                Assertion.AssertEquals("Value5",  evaluatedProperties["Property2"].Value);
                Assertion.AssertEquals("Value1",  evaluatedProperties["Property1"].Value);
            }

            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void ItemGroupInAPropertyGroupCondition()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                        <ItemGroup>
                            <x Include=`x1`/>
                        </ItemGroup>

                        <PropertyGroup Condition=`@(x)=='x1'`>
                            <a>@(x)</a>
                        </PropertyGroup>

                        <Target Name=`t`>
                            <Message Text=`[$(a)]`/>
                        </Target>

                    </Project>

                ");

                p.Build(new string[] { "t" }, null);
            }

            [Test]
            public void RemoveProperty()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                        <PropertyGroup>
                            <a>123</a>
                            <b>456</b>
                        </PropertyGroup>

                        <PropertyGroup>
                            <c>789</c>
                        </PropertyGroup>

                    </Project>

                ");

                BuildPropertyGroup[] propertyGroups = new BuildPropertyGroup[2];
                p.PropertyGroups.CopyTo(propertyGroups, 0);

                BuildPropertyGroup first = propertyGroups[0].Count == 2 ? propertyGroups[0] : propertyGroups[1];
                BuildPropertyGroup second = propertyGroups[1].Count == 1 ? propertyGroups[1] : propertyGroups[0];

                BuildProperty property = null;
                foreach (BuildProperty tempProperty in first)
                {
                    if (tempProperty.Name.Equals("a", StringComparison.OrdinalIgnoreCase))
                    {
                        property = tempProperty;
                        break;
                    }
                }

                first.RemoveProperty(property);

                Assertion.AssertEquals("First property group should now only have 1 property.", 1, first.Count);
                Assertion.AssertEquals("Second property group should still have 1 property.", 1, second.Count);

                Assertion.Assert("Project xml should not have a definition for property 'a'.", !p.Xml.Contains(@"<a>"));
            }

            [Test]
            [ExpectedException(typeof(InvalidOperationException))]
            public void RemovePropertyFromPropertyGroupThatIsntItsParent()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                        <PropertyGroup>
                            <a>123</a>
                            <b>456</b>
                        </PropertyGroup>

                        <PropertyGroup>
                            <c>789</c>
                        </PropertyGroup>

                    </Project>

                ");

                BuildPropertyGroup[] propertyGroups = new BuildPropertyGroup[2];
                p.PropertyGroups.CopyTo(propertyGroups, 0);

                BuildPropertyGroup first = propertyGroups[0].Count == 2 ? propertyGroups[0] : propertyGroups[1];
                BuildPropertyGroup second = propertyGroups[1].Count == 1 ? propertyGroups[1] : propertyGroups[0];

                BuildProperty property = null;
                foreach (BuildProperty tempProperty in first)
                {
                    if (tempProperty.Name.Equals("a", StringComparison.OrdinalIgnoreCase))
                    {
                        property = tempProperty;
                        break;
                    }
                }
                
                //this should throw because the property comes from another property group
                second.RemoveProperty(property);
            }

            [Test]
            public void SetGlobalPropertyAfterLoadBeforeBuild()
            {
                MockLogger logger = new MockLogger();

                Project project = ObjectModelHelpers.CreateInMemoryProject(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`log_property`>
                            <Message Text=`[$(p)]`/>
                        </Target>
                    </Project>
                ", logger);

                BuildPropertyGroup globals = project.GlobalProperties;

                // Set a property before building -- this should work
                BuildProperty p = new BuildProperty("p", "v1");
                globals.SetProperty(p);

                project.Build();

                logger.AssertLogContains("[v1]");
            }

            [Test]
            [ExpectedException(typeof(InvalidOperationException))]
            public void SetGlobalPropertyToDifferentValueDuringBuild()
            {
                SetPropertyDuringBuild("v1", "v2");
            }

            [Test]
            public void SetGlobalPropertyDuringBuildToValueItAlreadyHas()
            {
                MockLogger logger = SetPropertyDuringBuild("v1", "v1");
                logger.AssertLogContains("[v1]");
            }

            private MockLogger SetPropertyDuringBuild(string initialValue, string newValue)
            {
                Thread t = null;
                MockLogger logger = new MockLogger();

                try
                {
                    Project project = ObjectModelHelpers.CreateInMemoryProject(String.Format(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <UsingTask TaskName=`WaitForSignal` AssemblyFile=`{0}`/>

                        <Target Name=`wait`>
                            <WaitForSignal/>
                            <Message Text=`[$(p)]`/>
                        </Target>
                    </Project>

                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath), logger);

                    BuildPropertyGroup globals = project.GlobalProperties;
                    Console.WriteLine("Setting property to " + initialValue);
                    globals.SetProperty("p", initialValue);

                    // Set a property during build -- this should fail
                    t = new Thread(BuildSuppliedProject);
                    t.Start(project);

                    // Wait for the build to get going
                    while (!WaitForSignal.ReadyForSignal)
                    {
                        Thread.Sleep(50);
                    }

                    // Now set a property
                    Console.WriteLine("Setting property to " + newValue);
                    globals.SetProperty("p", newValue);
                }
                finally
                {
                    if (t != null)
                    {
                        // Clean up the thread, if necessary
                        while (!WaitForSignal.ReadyForSignal)
                        {
                            Thread.Sleep(50);
                        }
                        WaitForSignal.Signal();
                        t.Join();
                    }
                }

                return logger;
            }

            private void BuildSuppliedProject(Object o)
            {
                Project project = (Project) o;
                project.Build();
            }

            [Test]
            public void PropertyGroupCustomSerialization()
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();
                pg.SetProperty(new BuildProperty("bar", "barval", PropertyType.EnvironmentProperty));
                pg.SetProperty(new BuildProperty("baz", "bazval", PropertyType.GlobalProperty));
                pg.SetProperty(new BuildProperty("caz", "cazval", PropertyType.ImportedProperty));
                pg.SetProperty(new BuildProperty("barb", "barbout", PropertyType.OutputProperty));
                pg.SetProperty(new BuildProperty("gaz", "gazout", PropertyType.OutputProperty));
                pg.SetProperty(new BuildProperty("foo", "fooout2", PropertyType.OutputProperty));

                MemoryStream stream = new MemoryStream();
                BinaryWriter writer = new BinaryWriter(stream);
                BinaryReader reader = new BinaryReader(stream);
                try
                {
                    stream.Position = 0;
                    pg.WriteToStream(writer);
                    long streamWriteEndPosition = stream.Position;

                    stream.Position = 0;
                    BuildPropertyGroup pg2 = new BuildPropertyGroup();
                    pg2.CreateFromStream(reader);
                    long streamReadEndPosition = stream.Position;
                    Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream end positions should be equal");
                    Assert.AreEqual(6, pg.Count);
                    Assert.AreEqual("fooout2", pg2["foo"].FinalValueEscaped);
                    Assert.AreEqual("barval", pg2["bar"].FinalValueEscaped);
                    Assert.AreEqual("bazval", pg2["baz"].FinalValueEscaped);
                    Assert.AreEqual("cazval", pg2["caz"].FinalValueEscaped);
                    Assert.AreEqual("barbout", pg2["barb"].FinalValueEscaped);
                    Assert.AreEqual("gazout", pg2["gaz"].FinalValueEscaped);
                }
                finally
                {
                    reader.Close();
                    writer = null;
                    stream = null;
                }
            }
        }

        /// <summary>
        /// Task sets "Ready" when it's started, and sleeps until "Signal" is set.
        /// Useful if you want a test to do something while a build is in progress.
        /// </summary>
        public class WaitForSignal : Microsoft.Build.Utilities.Task
        {
            private static bool signal;
            private static bool readyForSignal;
            private static Object locker = new Object();

            public static void Signal()
            {
                lock (locker)
                {
                    // Lock, so that we don't race the caller's check for ready
                    if (readyForSignal)
                    {
                        signal = true;
                        readyForSignal = false;
                    }
                }
            }

            public static bool ReadyForSignal
            {
                get { return readyForSignal; }
            }

            public override bool Execute()
            {
                signal = false;
                readyForSignal = true;

                Console.WriteLine("waiting for signal");
                while (!signal)
                {
                    Thread.Sleep(50);
                }
                Console.WriteLine(" ... signaled");

                readyForSignal = false;
                signal = false;

                return true;
            }
        }
    }
