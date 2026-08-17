// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Contains helper methods for creating projects for testing.
    /// </summary>
    internal static class ProjectHelpers
    {
        /// <summary>
        /// Creates a project instance with a single empty target named 'foo'
        /// </summary>
        /// <returns>A project instance.</returns>
        internal static ProjectInstance CreateEmptyProjectInstance()
        {
            XmlReader reader = XmlReader.Create(new StringReader(
                @"<Project>
                      <Target Name='foo'/>
                  </Project>"));

            Project project = new Project(reader);
            ProjectInstance instance = project.CreateProjectInstance();

            return instance;
        }
    }
}
