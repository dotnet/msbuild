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
    [TestFixture]
    public class TaskItemTests
    {
        /*
        * Method:  SimpleCopyMetadataTo
        * Owner:   jomof
        * 
        * Try the most basic CopyMetadataTo(...)
        * 
        */
        [Test]
        public void SimpleCopyMetadataTo()
        {
            TaskItem from = new TaskItem("myfile.txt");
            from.SetMetadata("Culture", "fr");
            
            TaskItem to = new TaskItem("myfile.bin");
            from.CopyMetadataTo(to);
            
            Assertion.AssertEquals("fr", to.GetMetadata("Culture"));
        }
        
        /*
        * Method:  CopyMetadataToDoesNotCopyExtension
        * Owner:   jomof
        * 
        * Make sure that CopyMetadataTo(...) does not copy extension.
        * 
        */
        [Test]
        public void CopyMetadataToDoesNotCopyExtension()
        {
            TaskItem from = new TaskItem("myfile.txt");
            TaskItem to = new TaskItem("myfile.bin");

            from.CopyMetadataTo(to);
            
            Assertion.AssertEquals(".bin", to.GetMetadata("Extension"));
        }

        [Test]
        public void CopyMetadataToWithDefaults()
        {
            BuildItem fromBuildItem = BuildItem_Tests.GetXmlBackedItemWithDefinitionLibrary(); // i1;  has m=m1 (default) and n=n1 (regular)
            TaskItem from = new TaskItem(fromBuildItem);

            TaskItem to = new TaskItem("i2");
            from.CopyMetadataTo(to);

            Assertion.AssertEquals("n1", to.GetMetadata("n"));
            Assertion.AssertEquals("m1", to.GetMetadata("m"));
        }

        /// <summary>
        /// Verify items cannot be created with null itemspec
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CreateItemWithNullItemSpec()
        {
            string nullItem = null;
            BuildItem item = new BuildItem("x", nullItem);
        }

        /// <summary>
        /// Verify items can be created with empty itemspec
        /// (To be consistent with the shipped TaskItem class...)
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void CreateItemWithEmptyItemSpec()
        {
            BuildItem item = new BuildItem("x", "");
            Assertion.AssertEquals(String.Empty, item.EvaluatedItemSpec);
        }

        /// <summary>
        /// Verify metadata cannot be created with null name
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CreateNullNamedMetadata()
        {
            TaskItem item = new TaskItem("foo");
            item.SetMetadata(null, "x");
        }

        /// <summary>
        /// Verify metadata cannot be created with empty name
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void CreateEmptyNamedMetadata()
        {
            TaskItem item = new TaskItem("foo");
            item.SetMetadata("", "x");
        }

    }
}
