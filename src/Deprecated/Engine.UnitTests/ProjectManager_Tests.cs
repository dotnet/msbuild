// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#region Using directives

using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using System.Collections;

#endregion

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ProjectManager_Tests
    {
        /// <summary>
        /// Add a project to the ProjectManager, and try to get it back out using the 
        /// correct set of search criteria.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void SimpleAddAndRetrieveProject()
        {
            // Initialize engine.
            Engine engine = new Engine(@"c:\");

            // Instantiate new project manager.
            ProjectManager projectManager = new ProjectManager();

            // Set up variables that represent the information we would be getting from 
            // the "MSBuild" task.
            string fullPath = @"c:\rajeev\temp\myapp.proj";
            BuildPropertyGroup globalProperties = new BuildPropertyGroup();
            globalProperties.SetProperty("Configuration", "Debug");

            // Create a new project that matches the information that we're pretending
            // to receive from the MSBuild task.
            Project project1 = new Project(engine);
            project1.FullFileName = fullPath;
            project1.GlobalProperties = globalProperties;

            // Add the new project to the ProjectManager.
            projectManager.AddProject(project1);

            // Try and retrieve the project from the ProjectManager based on the fullpath + globalprops,
            // and make sure we get back the same project we added.
            Assertion.AssertEquals(project1, projectManager.GetProject(fullPath, globalProperties, null));
        }
        
        /// <summary>
        /// Verify project manager does not insert duplicates into project table.
        /// </summary>
        [Test]
        public void TestForDuplicatesInProjectTable()
        {
            ProjectManager projectManager = new ProjectManager();

            string fullPath = @"c:\foo\bar.proj";
            BuildPropertyGroup globalProperties = new BuildPropertyGroup();
            globalProperties.SetProperty("p1", "v1");

            Project p = new Project(new Engine());
            p.FullFileName = fullPath;
            p.GlobalProperties = globalProperties;
            p.ToolsVersion = "4.0";

            // Add the new project to the ProjectManager, twice
            Hashtable table = new Hashtable(StringComparer.OrdinalIgnoreCase);
            ProjectManager.AddProject(table, p);
            ProjectManager.AddProject(table, p);

            Assertion.AssertEquals(1, ((ArrayList)table[fullPath]).Count); // Didn't add a duplicate

            // Add a second, slightly different project, and ensure it DOES get added
            Project p2 = new Project(new Engine());
            p2.FullFileName = fullPath;
            p2.GlobalProperties = globalProperties;
            p2.ToolsVersion = "2.0";

            ProjectManager.AddProject(table, p2);

            Project p3 = new Project(new Engine());
            p3.FullFileName = fullPath;
            p3.GlobalProperties = new BuildPropertyGroup();
            p3.ToolsVersion = "2.0";

            ProjectManager.AddProject(table, p3);

            Assertion.AssertEquals(3, ((ArrayList)table[fullPath]).Count);
        }

        /// <summary>
        /// Verify project manager does not insert duplicates into project entry table.
        /// </summary>
        [Test]
        public void TestForDuplicatesInProjectEntryTable()
        {
            ProjectManager projectManager = new ProjectManager();

            string fullPath = @"c:\foo\bar.proj";
            BuildPropertyGroup globalProperties = new BuildPropertyGroup();
            globalProperties.SetProperty("p1", "v1");
            string toolsVersion = "3.5";

            // Add the new project entry to the ProjectManager, twice
            Hashtable table = new Hashtable(StringComparer.OrdinalIgnoreCase);
            ProjectManager.AddProjectEntry(table, fullPath, globalProperties, toolsVersion, 0);

            ProjectManager.AddProjectEntry(table, fullPath, globalProperties, toolsVersion, 0);

            Assertion.AssertEquals(1, ((ArrayList)table[fullPath]).Count); // Didn't add a duplicate

            // Add a second, slightly different project entry, and ensure it DOES get added
            ProjectManager.AddProjectEntry(table, fullPath, globalProperties, "2.0", 0);
            ProjectManager.AddProjectEntry(table, fullPath, new BuildPropertyGroup(), "2.0", 0);

            Assertion.AssertEquals(3, ((ArrayList)table[fullPath]).Count);
        }

        /// <summary>
        /// Add a project to the ProjectManager, and try to get it back out using the 
        /// wrong set of search criteria (different set of global properties).
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void SimpleAddAndRetrieveProjectWithDifferentGlobals()
        {
            // Initialize engine.
            Engine engine = new Engine(@"c:\");

            // Instantiate new project manager.
            ProjectManager projectManager = new ProjectManager();

            // Set up variables that represent the information we would be getting from 
            // the "MSBuild" task.
            string fullPath = @"c:\rajeev\temp\myapp.proj";
            BuildPropertyGroup globalProperties = new BuildPropertyGroup();
            globalProperties.SetProperty("Configuration", "Release");

            // Create a new project that matches the information that we're pretending
            // to receive from the MSBuild task.
            Project project1 = new Project(engine);
            project1.FullFileName = fullPath;
            project1.GlobalProperties = globalProperties;

            // Add the new project to the ProjectManager.
            projectManager.AddProject(project1);

            // Now search for a project with the same full path but a different set of global
            // properties.  We expect to get back null, because no such project exists.
            globalProperties.SetProperty("Configuration", "Debug");
            Assertion.AssertNull(projectManager.GetProject(fullPath, globalProperties, null));
        }

        /// <summary>
        /// Add a project to the ProjectManager, and try to get it back out using the 
        /// wrong set of search criteria (different full path).
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void SimpleAddAndRetrieveProjectWithDifferentFullPath()
        {
            // Initialize engine.
            Engine engine = new Engine(@"c:\");

            // Instantiate new project manager.
            ProjectManager projectManager = new ProjectManager();

            // Set up variables that represent the information we would be getting from 
            // the "MSBuild" task.
            BuildPropertyGroup globalProperties = new BuildPropertyGroup();
            globalProperties.SetProperty("Configuration", "Release");

            // Create a new project that matches the information that we're pretending
            // to receive from the MSBuild task.
            Project project1 = new Project(engine);
            project1.FullFileName = @"c:\rajeev\temp\myapp.proj";
            project1.GlobalProperties = globalProperties;

            // Add the new project to the ProjectManager.
            projectManager.AddProject(project1);

            // Now search for a project with a different full path but same set of global
            // properties.  We expect to get back null, because no such project exists.
            Assertion.AssertNull(projectManager.GetProject(@"c:\blah\wrong.proj", globalProperties, null));
        }

        /// <summary>
        /// Reset the build status for every project stored in the ProjectManager.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ResetBuildStatusForAllProjects()
        {
            // Initialize engine.  Need two separate engines because we don't allow two
            // projects with the same full path to be loaded in the same Engine.
            Engine engine1 = new Engine(@"c:\");
            Engine engine2 = new Engine(@"c:\");

            // Instantiate new project manager.
            ProjectManager projectManager = new ProjectManager();

            // Set up a global property group.
            BuildPropertyGroup globalProperties = new BuildPropertyGroup();
            globalProperties.SetProperty("Configuration", "Release");

            // Create a few new projects.
            Project project1 = new Project(engine1);
            project1.FullFileName = @"c:\rajeev\temp\myapp.proj";
            project1.GlobalProperties = globalProperties;

            Project project2 = new Project(engine1);
            project2.FullFileName = @"c:\blah\foo.proj";
            project2.GlobalProperties = globalProperties;

            Project project3 = new Project(engine2);
            project3.FullFileName = @"c:\blah\foo.proj";
            globalProperties.SetProperty("Configuration", "Debug");
            project3.GlobalProperties = globalProperties;

            // Add the new projects to the ProjectManager.
            projectManager.AddProject(project1);
            projectManager.AddProject(project2);
            projectManager.AddProject(project3);

            // Put all the projects in a non-reset state.
            project1.IsReset = false;
            project2.IsReset = false;
            project3.IsReset = false;

            // Call ResetAllProjects.
            projectManager.ResetBuildStatusForAllProjects();

            // Make sure they all got reset.
            Assertion.Assert(project1.IsReset);
            Assertion.Assert(project2.IsReset);
            Assertion.Assert(project3.IsReset);
        }

        /// <summary>
        /// Removes all projects of a given full path.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void RemoveProjectsByFullPath()
        {
            // Initialize engine.  Need two separate engines because we don't allow two
            // projects with the same full path to be loaded in the same Engine.
            Engine engine1 = new Engine(@"c:\");
            Engine engine2 = new Engine(@"c:\");

            // Instantiate new project manager.
            ProjectManager projectManager = new ProjectManager();

            // Set up a global property group.
            BuildPropertyGroup globalProperties = new BuildPropertyGroup();
            globalProperties.SetProperty("Configuration", "Release");

            // Create a few new projects.
            Project project1 = new Project(engine1);
            project1.FullFileName = @"c:\rajeev\temp\myapp.proj";
            project1.GlobalProperties = globalProperties;

            Project project2 = new Project(engine1);
            project2.FullFileName = @"c:\blah\foo.proj";
            project2.GlobalProperties = globalProperties;

            Project project3 = new Project(engine2);
            project3.FullFileName = @"c:\blah\foo.proj";
            globalProperties.SetProperty("Configuration", "Debug");
            project3.GlobalProperties = globalProperties;

            // Add the new projects to the ProjectManager.
            projectManager.AddProject(project1);
            projectManager.AddProject(project2);
            projectManager.AddProject(project3);

            // Remove all projects with the full path "c:\blah\foo.proj" (case insenstively).
            projectManager.RemoveProjects(@"c:\BLAH\FOO.Proj");

            // Make sure project 1 is still there.
            Assertion.AssertEquals(project1, projectManager.GetProject(project1.FullFileName, project1.GlobalProperties, null));

            // Make sure projects 2 and 3 are gone.
            Assertion.AssertNull(projectManager.GetProject(project2.FullFileName, project2.GlobalProperties, null));
            Assertion.AssertNull(projectManager.GetProject(project3.FullFileName, project3.GlobalProperties, null));
        }
    }
}
