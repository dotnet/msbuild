// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Tests for TargetCollection
    /// </summary>
    [TestFixture]
    public class TargetCollection_Tests
    {
        #region Common Helpers
        /// <summary>
        /// Basic project content with several targets, where depends on targets is used
        /// </summary>
        private const string ProjectContentSeveralTargets = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1' DependsOnTargets='t2' Inputs='in' Outputs='out'/>
                                    <Target Name='t2' DependsOnTargets='t3' Condition=""'true' == 'true'"">
                                        <Message Text='t2.task' />
                                    </Target>
                                    <Target Name='t3' DependsOnTargets='t4'>
                                        <Message Text='t3.task' />
                                    </Target>
                                    <Target Name='t4'>
                                        <Message Text='t4.task' />
                                    </Target>
                                    <Target Name='t5'>
                                        <Message Text='t5.task' />
                                    </Target>
                                </Project>
                            ";

        /// <summary>
        /// Basic project content with no targets - an emtpy project
        /// </summary>
        private const string ProjectContentNoTargets = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                </Project>
                            ";

        /// <summary>
        /// Engine that is used through out test class
        /// </summary>
        private Engine engine;

        /// <summary>
        /// Project that is used through out test class
        /// </summary>
        private Project project;

        /// <summary>
        /// Creates the engine and parent object.
        /// </summary>
        [SetUp()]
        public void Initialize()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            engine = new Engine();
            project = new Project(engine);
        }

        /// <summary>
        /// Unloads projects
        /// </summary>
        [TearDown()]
        public void Cleanup()
        {
            engine.UnloadProject(project);
            engine.UnloadAllProjects();

            ObjectModelHelpers.DeleteTempProjectDirectory();
        }
        #endregion

        #region Count Tests
        /// <summary>
        /// Tests TargetCollection.Count with many targets
        /// </summary>
        [Test]
        public void CountMany()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;

            Assertion.AssertEquals(5, targets.Count);
        }

        /// <summary>
        /// Tests TargetCollection.Count with some targets that are imported
        /// </summary>
        [Test]
        public void CountWithImportedTargets()
        {
            string importProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t2'>
                            <Message Text='imported.t2.task' />
                        </Target>
                    <Target Name='t3' />
                    </Project>
                ";

            string projectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1'>
                            <Message Text='parent.t1.task' />
                        </Target>
                        <Import Project='import.proj' />
                    </Project>
                ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, projectContents);
            TargetCollection targets = p.Targets;

            Assertion.AssertEquals(3, targets.Count);
        }

        /// <summary>
        /// Tests TargetCollection.Count when the imported project and parent project both contain
        ///     a target of the same name.
        /// </summary>
        [Test]
        public void CountWhenImportedAndParentBothContainSameTarget()
        {
            string importProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1'>
                            <Message Text='imported.t2.task' />
                        </Target>
                    </Project>
                ";

            string parentProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1'>
                            <Message Text='parent.t1.task' />
                        </Target>
                        <Import Project='import.proj' />
                    </Project>
                ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, parentProjectContents);
            TargetCollection targets = p.Targets;

            Assertion.AssertEquals(1, targets.Count);
        }

        /// <summary>
        /// Tests TargetCollection.Count when no targets exist
        /// </summary>
        [Test]
        public void CountWithNoTargets()
        {
            project.LoadXml(ProjectContentNoTargets);
            TargetCollection targets = project.Targets;

            Assertion.AssertEquals(0, targets.Count);
        }

        /// <summary>
        /// Tests TargetCollection.Count after adding a new target
        /// </summary>
        [Test]
        public void CountAfterAddingNewTarget()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            project.Targets.AddNewTarget("t6");

            TargetCollection targets = project.Targets;
            Assertion.AssertEquals(6, targets.Count);
        }

        /// <summary>
        /// Tests TargetCollection.Count after removing a target
        /// </summary>
        [Test]
        public void CountAfterRemovingTarget()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            Target t = GetSpecificTargetFromProject(project, "t5");
            project.Targets.RemoveTarget(t);

            TargetCollection targets = project.Targets;
            Assertion.AssertEquals(4, targets.Count);
        }
        #endregion

        #region Exists Tests
        /// <summary>
        /// Tests TargetCollection.Exists when target exists
        /// </summary>
        [Test]
        public void ExistsWhenTargetExists()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;

            Assertion.AssertEquals(true, targets.Exists("t2"));
        }

        /// <summary>
        /// Tests TargetCollection.Exists when target doesn't exist
        /// </summary>
        [Test]
        public void ExistsWhenTargetDoesNotExist()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;

            Assertion.AssertEquals(false, targets.Exists("tNot"));
        }

        /// <summary>
        /// Tests TargetCollection.Exists of an imported target
        /// </summary>
        [Test]
        public void ExistsOfImportedTarget()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            TargetCollection targets = p.Targets;

            Assertion.AssertEquals(true, targets.Exists("t4"));
        }

        /// <summary>
        /// Tests TargetCollection.Exists of a target that comes from an import as well as parent project
        /// </summary>
        [Test]
        public void ExistsWhenImportedTargetAndParentTargetHaveSameName()
        {
            string importProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1'>
                            <Message Text='imported.t2.task' />
                        </Target>
                    </Project>
                ";

            string parentProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1'>
                            <Message Text='parent.t1.task' />
                        </Target>
                        <Target Name='t2' />
                        <Import Project='import.proj' />
                    </Project>
                ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, parentProjectContents);
            TargetCollection targets = p.Targets;

            Assertion.AssertEquals(true, targets.Exists("t1"));
        }
        #endregion

        #region AddNewTarget Tests
        /// <summary>
        /// Tests TargetCollection.AddNewTarget by adding a new target
        /// </summary>
        [Test]
        public void AddNewTargetSimple()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;
            targets.AddNewTarget("tNew");

            Assertion.AssertEquals(true, targets.Exists("tNew"));
            Assertion.AssertEquals(6, targets.Count);
        }

        /// <summary>
        /// Tests TargetCollection.AddNewTarget by adding a new target of the same name
        /// </summary>
        [Test]
        public void AddNewTargetWhenTargetOfSameNameAlreadyExists()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;
            targets.AddNewTarget("t1");

            Assertion.AssertEquals(true, targets.Exists("t1"));
            Assertion.AssertEquals(5, targets.Count);
        }

        /// <summary>
        /// Tests TargetCollection.AddNewTarget by adding a new target with an String.Empty name
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void AddNewTargetEmptyStringName()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;
            targets.AddNewTarget(String.Empty);
        }

        /// <summary>
        /// Tests TargetCollection.AddNewTarget by adding a new target with a null name
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void AddNewTargetNullName()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;
            targets.AddNewTarget(null);
        }

        /// <summary>
        /// Tests TargetCollection.AddNewTarget by adding a new target with special characters in the name
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void AddNewTargetSpecialCharacterName()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;
            targets.AddNewTarget("%24%40%3b%5c%25");
        }

        /// <summary>
        /// Tests TargetCollection.AddNewTarget by adding a new target to a project with no other targets
        /// </summary>
        [Test]
        public void AddNewTargetWhenNoOtherTargetsExist()
        {
            project.LoadXml(ProjectContentNoTargets);
            TargetCollection targets = project.Targets;
            targets.AddNewTarget("t");

            Assertion.AssertEquals(true, targets.Exists("t"));
            Assertion.AssertEquals(1, targets.Count);
        }
        #endregion

        #region RemoveTarget Tests
        /// <summary>
        /// Tests TargetCollection.RemoveTarget by removing an existing target
        /// </summary>
        [Test]
        public void RemoveTargetOfExistingTarget()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;
            targets.RemoveTarget(GetSpecificTargetFromProject(project, "t1"));

            Assertion.AssertEquals(false, targets.Exists("t1"));
        }

        /// <summary>
        /// Tests TargetCollection.RemoveTarget passing in null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RemoveTargetNull()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;
            targets.RemoveTarget(null);
        }

        /// <summary>
        /// Tests TargetCollection.RemoveTarget of an imported target
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveTargetFromImport()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            TargetCollection targets = p.Targets;
            targets.RemoveTarget(GetSpecificTargetFromProject(p, "t2"));
        }
        #endregion

        #region CopyTo Tests
        /// <summary>
        /// Tests TargetCollection.CopyTo basic case
        /// </summary>
        [Test]
        public void CopyToSimple()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;

            object[] array = new object[targets.Count];
            targets.CopyTo(array, 0);
            
            List<string> listOfTargets = new List<string>();
            foreach (Target t in array)
            {
                listOfTargets.Add(t.Name);
            }

            // This originates in a hashtable, whose ordering is undefined
            // and indeed changes in CLR4
            listOfTargets.Sort();

            Assertion.AssertEquals(targets["t1"].Name, listOfTargets[0]);
            Assertion.AssertEquals(targets["t2"].Name, listOfTargets[1]);
            Assertion.AssertEquals(targets["t3"].Name, listOfTargets[2]);
            Assertion.AssertEquals(targets["t4"].Name, listOfTargets[3]);
            Assertion.AssertEquals(targets["t5"].Name, listOfTargets[4]);
        }

        /// <summary>
        /// Tests TargetCollection.CopyTo passing in a null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CopyToNull()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;

            targets.CopyTo(null, 0);
        }

        /// <summary>
        /// Tests TargetCollection.CopyTo when you attempt CopyTo into an Array that's not long enough
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void CopyToArrayThatsNotLargeEnough()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;

            object[] array = new object[2];
            targets.CopyTo(array, 0);
        }
        #endregion

        #region IsSynchronized Tests
        /// <summary>
        /// Tests TargetCollection.IsSynchronized for the default case
        /// </summary>
        [Test]
        public void IsSynchronizedDefault()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;

            Assertion.AssertEquals(false, targets.IsSynchronized);
        }
        #endregion

        #region TargetThis Tests
        /// <summary>
        /// Tests TargetCollection["targetname"].property where no imports exist
        /// </summary>
        [Test]
        public void TargetThisNoImports()
        {
            project.LoadXml(ProjectContentSeveralTargets);
            TargetCollection targets = project.Targets;

            Assertion.AssertEquals("in", targets["t1"].Inputs);
            Assertion.AssertEquals("out", targets["t1"].Outputs);
            Assertion.AssertEquals(false, targets["t2"].IsImported);
            Assertion.AssertEquals("'true' == 'true'", targets["t2"].Condition);
            Assertion.AssertEquals("t3", targets["t2"].DependsOnTargets);
            Assertion.AssertEquals("t2", targets["t2"].Name);
        }

        /// <summary>
        /// Tests TargetCollection["targetname"].property where imports exist
        /// </summary>
        [Test]
        public void TargetThisWithImports()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            TargetCollection targets = p.Targets;

            Assertion.AssertEquals("in", targets["t3"].Inputs);
            Assertion.AssertEquals("out", targets["t3"].Outputs);
            Assertion.AssertEquals(true, targets["t3"].IsImported);
            Assertion.AssertEquals("'true' == 'true'", targets["t3"].Condition);
            Assertion.AssertEquals("t2", targets["t3"].DependsOnTargets);
            Assertion.AssertEquals("t3", targets["t3"].Name);
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Gets a specified Target from a Project
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="nameOfTarget">Target name of the Target you want</param>
        /// <returns>Target requested.  null if specific target isn't found</returns>
        private Target GetSpecificTargetFromProject(Project p, string nameOfTarget)
        {
            foreach (Target t in p.Targets)
            {
                if (String.Equals(t.Name, nameOfTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a Project that imports another Project
        /// </summary>
        /// <param name="importProjectContents">Project Contents of the imported Project, to get default content, pass in an empty string</param>
        /// <param name="parentProjectContents">Project Contents of the Parent Project, to get default content, pass in an empty string</param>
        /// <returns>Project</returns>
        private Project GetProjectThatImportsAnotherProject(string importProjectContents, string parentProjectContents)
        {
            if (String.IsNullOrEmpty(importProjectContents))
            {
                importProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t2' />
                        <Target Name='t3' DependsOnTargets='t2' Inputs='in' Outputs='out' Condition=""'true' == 'true'""/>
                        <Target Name='t4' />
                    </Project>
                ";
            }

            if (String.IsNullOrEmpty(parentProjectContents))
            {
                parentProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1'>
                            <Message Text='parent.t1.task' />
                        </Target>
                        <Import Project='import.proj' />
                    </Project>
                ";
            }

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", importProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", parentProjectContents);
            return ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);
        }
        #endregion
    }
}
