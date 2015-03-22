// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Helper class to create projects for testing..</summary>
//-----------------------------------------------------------------------

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using System.IO;
using System.Xml;

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
            XmlReader reader = XmlReader.Create(new StringReader
                (
                @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='4.0'>
                      <Target Name='foo'/>
                  </Project>"
                ));

            Project project = new Project(reader);
            ProjectInstance instance = project.CreateProjectInstance();

            return instance;
        }
    }
}
