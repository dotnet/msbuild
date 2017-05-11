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
    sealed public class RemoveDir_Tests
    {
        /*
         * Method:   AttributeForwarding
         *
         * Make sure that attributes set on input items are forwarded to output items.
         */
        [Fact]
        public void AttributeForwarding()
        {
            RemoveDir t = new RemoveDir();

            ITaskItem i = new TaskItem("MyNonExistentDirectory");
            i.SetMetadata("Locale", "en-GB");
            t.Directories = new ITaskItem[] { i };
            t.BuildEngine = new MockEngine();

            t.Execute();

            Assert.Equal("en-GB", t.RemovedDirectories[0].GetMetadata("Locale"));

            // Output ItemSpec should not be overwritten.
            Assert.Equal("MyNonExistentDirectory", t.RemovedDirectories[0].ItemSpec);
        }
    }
}



