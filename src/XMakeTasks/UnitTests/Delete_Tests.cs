// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class Delete_Tests
    {
        /*
         * Method:   AttributeForwarding
         *
         * Make sure that attributes set on input items are forwarded to ouput items.
         */
        [TestMethod]
        public void AttributeForwarding()
        {
            Delete t = new Delete();

            ITaskItem i = new TaskItem("MyFiles.nonexistent");
            i.SetMetadata("Locale", "en-GB");
            t.Files = new ITaskItem[] { i };
            t.BuildEngine = new MockEngine();

            t.Execute();

            Assert.AreEqual("en-GB", t.DeletedFiles[0].GetMetadata("Locale"));

            // Output ItemSpec should not be overwritten.
            Assert.AreEqual("MyFiles.nonexistent", t.DeletedFiles[0].ItemSpec);
        }
    }
}



