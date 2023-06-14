// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class RemoveDir_Tests
    {
        private ITestOutputHelper _output;
        public RemoveDir_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

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
            t.BuildEngine = new MockEngine(_output);

            t.Execute();

            t.RemovedDirectories[0].GetMetadata("Locale").ShouldBe("en-GB");
            t.RemovedDirectories[0].ItemSpec.ShouldBe("MyNonExistentDirectory");
            Directory.Exists(t.RemovedDirectories[0].ItemSpec).ShouldBeFalse();
        }

        [Fact]
        public void SimpleDelete()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                List<TaskItem> list = new List<TaskItem>();

                for (int i = 0; i < 20; i++)
                {
                    list.Add(new TaskItem(env.CreateFolder().Path));
                }

                // Question RemoveDir when files exists.
                RemoveDir t = new RemoveDir()
                {
                    Directories = list.ToArray(),
                    BuildEngine = new MockEngine(_output),
                    FailIfNotIncremental = true,
                };
                t.Execute().ShouldBeFalse();

                RemoveDir t2 = new RemoveDir()
                {
                    Directories = list.ToArray(),
                    BuildEngine = new MockEngine(_output),
                };
                t2.Execute().ShouldBeTrue();
                t2.RemovedDirectories.Length.ShouldBe(list.Count);

                for (int i = 0; i < 20; i++)
                {
                    Directory.Exists(list[i].ItemSpec).ShouldBeFalse();
                }

                // Question again to make sure all files were deleted.
                RemoveDir t3 = new RemoveDir()
                {
                    Directories = list.ToArray(),
                    BuildEngine = new MockEngine(_output),
                    FailIfNotIncremental = true,
                };
                t3.Execute().ShouldBeTrue();
            }
        }

        /// <summary>
        /// Regression test: https://github.com/dotnet/msbuild/issues/7563
        /// </summary>
        [Fact]
        public void DeleteEmptyDirectory_WarnsAndContinues()
        {

            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                List<TaskItem> list = new List<TaskItem>();

                for (int i = 0; i < 20; i++)
                {
                    list.Add(new TaskItem(""));
                }

                RemoveDir t = new RemoveDir();
                t.Directories = list.ToArray();
                t.BuildEngine = new MockEngine(_output);
                t.Execute().ShouldBeTrue();

                t.RemovedDirectories.Length.ShouldBe(0);
                ((MockEngine)t.BuildEngine).Warnings.ShouldBe(20);
                ((MockEngine)t.BuildEngine).AssertLogContains("MSB3232");
            }
        }
    }
}
