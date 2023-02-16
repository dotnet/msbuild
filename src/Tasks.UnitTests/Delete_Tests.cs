// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class Delete_Tests
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

        /// <summary>
        /// Retry Delete. Specify windows since readonly not working on others
        /// </summary>
        [WindowsOnlyFact]
        public void DeleteWithRetries()
        {
            string source = FileUtilities.GetTemporaryFile();
            try
            {
                using (StreamWriter sw = FileUtilities.OpenWrite(source, true))
                {
                    sw.Write("This is a source file.");
                }

                File.SetAttributes(source, FileAttributes.ReadOnly);

                ITaskItem sourceItem = new TaskItem(source);
                ITaskItem[] sourceFiles = { sourceItem };

                var t = new Delete
                {
                    RetryDelayMilliseconds = 1,  // speed up tests!
                    BuildEngine = new MockEngine(),
                    Files = sourceFiles,
                    Retries = 1,
                };

                // Should fail since file is readonly
                t.Execute().ShouldBe(false);

                // Do retries
                ((MockEngine)t.BuildEngine).AssertLogContains("MSB3062");

                File.SetAttributes(source, FileAttributes.Normal);
                ITaskItem[] duplicateSourceFiles = { sourceItem, sourceItem };
                t = new Delete
                {
                    RetryDelayMilliseconds = 1,  // speed up tests!
                    BuildEngine = new MockEngine(),
                    Files = duplicateSourceFiles,
                    Retries = 1,
                };
                t.Execute().ShouldBe(true);
                ((MockEngine)t.BuildEngine).AssertLogDoesntContain("MSB3062");
            }
            finally
            {
                File.Delete(source);
            }
        }
    }
}
