// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class CommandLineBuilderExtensionTest
    {
        /*
        * Method:   AppendItemWithInvalidBooleanAttribute
        *
        * When appending an ITaskItem[] where some of the flags are 'bool', it's possible that 
        * the boolean flag has a string value that cannot be converted to a boolean. In this
        * case we expect an exception.
        */
        [Fact]
        public void AppendItemWithInvalidBooleanAttribute()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                // Construct the task item.
                TaskItem i = new TaskItem();
                i.ItemSpec = "MyResource.bmp";
                i.SetMetadata("Name", "Kenny");
                i.SetMetadata("Private", "Yes");       // This is our flag.

                CommandLineBuilderExtension c = new CommandLineBuilderExtension();

                // Validate that a legitimate bool works first.
                try
                {
                    c.AppendSwitchIfNotNull
                    (
                        "/myswitch:",
                        new ITaskItem[] { i },
                        new string[] { "Name", "Private" },
                        new bool[] { false, true }
                    );
                    Assert.Equal(@"/myswitch:MyResource.bmp,Kenny,Private", c.ToString());
                }
                catch (ArgumentException e)
                {
                    Assert.True(false, "Got an unexpected exception:" + e.Message);
                }

                // Now try a bogus boolean.
                i.SetMetadata("Private", "Maybe");       // This is our flag.
                c.AppendSwitchIfNotNull
                (
                    "/myswitch:",
                    new ITaskItem[] { i },
                    new string[] { "Name", "Private" },
                    new bool[] { false, true }
                );  // <-- Expect an ArgumentException here.
            }
           );
        }
        /// <summary>
        /// When appending an ITaskItem[] where some of the optional attributes are
        /// present, but others aren't.  We can't be emitted attributes in the wrong
        /// order on the command-line, so we skip all subsequent attributes as soon
        /// as we find one missing.
        /// </summary>
        [Fact]
        public void AppendItemWithMissingAttribute()
        {
            // Construct the task items.
            TaskItem i = new TaskItem();
            i.ItemSpec = "MySoundEffect.wav";
            i.SetMetadata("Name", "Kenny");
            i.SetMetadata("Access", "Private");

            TaskItem j = new TaskItem();
            j.ItemSpec = "MySplashScreen.bmp";
            j.SetMetadata("Name", "Cartman");
            j.SetMetadata("HintPath", @"c:\foo");
            j.SetMetadata("Access", "Public");

            CommandLineBuilderExtension c = new CommandLineBuilderExtension();

            c.AppendSwitchIfNotNull
            (
                "/myswitch:",
                new ITaskItem[] { i, j },
                new string[] { "Name", "HintPath", "Access" },
                null
            );

            Assert.Equal(
               @"/myswitch:MySoundEffect.wav,Kenny "
               + @"/myswitch:MySplashScreen.bmp,Cartman,c:\foo,Public",
               c.ToString());
        }
    }
}
