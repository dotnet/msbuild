// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

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
