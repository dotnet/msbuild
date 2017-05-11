// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class Delete_Tests
    {
        /*
         * Method:   AttributeForwarding
         *
         * Make sure that attributes set on input items are forwarded to output items.
         */
        [Fact]
        public void AttributeForwarding()
        {
            Delete t = new Delete();

            ITaskItem i = new TaskItem("MyFiles.nonexistent");
            i.SetMetadata("Locale", "en-GB");
            t.Files = new ITaskItem[] { i };
            t.BuildEngine = new MockEngine();

            t.Execute();

            Assert.Equal("en-GB", t.DeletedFiles[0].GetMetadata("Locale"));

            // Output ItemSpec should not be overwritten.
            Assert.Equal("MyFiles.nonexistent", t.DeletedFiles[0].ItemSpec);
        }
    }
}



