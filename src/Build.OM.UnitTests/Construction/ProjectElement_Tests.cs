//-----------------------------------------------------------------------
// <copyright file="ProjectElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectElement base class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

using NUnit.Framework;

using Microsoft.Build.Exceptions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectElement class
    /// </summary>
    [TestFixture]
    public class ProjectElementTests
    {
        /// <summary>
        /// No parents
        /// </summary>
        [Test]
        public void AllParentsNoParents()
        {
            string content = @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' />";

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Assertion.AssertEquals(null, xml.AllParents.Count());
        }

        /// <summary>
        /// Two parents
        /// </summary>
        [Test]
        public void AllParentsNoParents()
        {
            string content = @"
                   <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' />
                     <Target Name='t'>
                       <Warning Text='w'/>
                     </Target>
                   </Project>
";

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTaskElement task = xml.Targets.GetFirst().Tasks.GetFirst();
            Assertion.AssertEquals(2, task.AllParents.Count());
            Assertion.AssertEquals(xml.Targets.GetFirst(), task.AllParents.GetFirst());
            Assertion.AssertEquals(xml, task.AllParents.ItemAt(1));
        }

       
    }
}
