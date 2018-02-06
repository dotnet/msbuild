// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class MapSourceRoots_Tests
    {
        [Fact]
        public void BasicMapping()
        {
            var task = new MapSourceRoots
            {
                SourceRoots = new[]
                {
                    new TaskItem(@"c:\packages\SourcePackage1\"),
                    new TaskItem(@"/packages/SourcePackage2/"),
                    new TaskItem(@"c:\MyProjects\MyProject\", new Dictionary<string, string>
                    {
                        { "SourceControl", "Git" },
                    }),
                    new TaskItem(@"c:\MyProjects\MyProject\a\b\", new Dictionary<string, string>
                    {
                        { "SourceControl", "Git" },
                        { "NestedRoot", "a/b" },
                        { "ContainingRoot", @"c:\MyProjects\MyProject\" },
                        { "some metadata", "some value" },
                    }),
                }
            };

            task.Execute();

            Assert.Equal(4, task.MappedSourceRoots.Length);

            Assert.Equal(FileUtilities.FixFilePath(@"c:\packages\SourcePackage1\"), task.MappedSourceRoots[0].ItemSpec);
            Assert.Equal(@"/_1/", task.MappedSourceRoots[0].GetMetadata("MappedPath"));

            Assert.Equal(@"/packages/SourcePackage2/", task.MappedSourceRoots[1].ItemSpec);
            Assert.Equal(@"/_2/", task.MappedSourceRoots[1].GetMetadata("MappedPath"));

            Assert.Equal(FileUtilities.FixFilePath(@"c:\MyProjects\MyProject\"), task.MappedSourceRoots[2].ItemSpec);
            Assert.Equal(@"/_/", task.MappedSourceRoots[2].GetMetadata("MappedPath"));
            Assert.Equal(@"Git", task.MappedSourceRoots[2].GetMetadata("SourceControl"));

            Assert.Equal(FileUtilities.FixFilePath(@"c:\MyProjects\MyProject\a\b\"), task.MappedSourceRoots[3].ItemSpec);
            Assert.Equal(@"/_/a/b/", task.MappedSourceRoots[3].GetMetadata("MappedPath"));
            Assert.Equal(@"Git", task.MappedSourceRoots[3].GetMetadata("SourceControl"));
            Assert.Equal(@"some value", task.MappedSourceRoots[3].GetMetadata("some metadata"));
        }

        [Fact]
        public void InvalidChars()
        {
            var task = new MapSourceRoots
            {
                SourceRoots = new[]
                {
                    new TaskItem(@"!@#:;$%^&*()_+|{}"),
                    new TaskItem(@"****", new Dictionary<string, string>
                    {
                        { "SourceControl", "Git" },
                    }),
                    new TaskItem(@"****\|||:;\", new Dictionary<string, string>
                    {
                        { "SourceControl", "Git" },
                        { "NestedRoot", "|||:;" },
                        { "ContainingRoot", @"****" },
                    }),
                }
            };

            task.Execute();

            Assert.Equal(3, task.MappedSourceRoots.Length);

            Assert.Equal(@"!@#:;$%^&*()_+|{}", task.MappedSourceRoots[0].ItemSpec);
            Assert.Equal(@"/_1/", task.MappedSourceRoots[0].GetMetadata("MappedPath"));

            Assert.Equal("****", task.MappedSourceRoots[1].ItemSpec);
            Assert.Equal(@"/_/", task.MappedSourceRoots[1].GetMetadata("MappedPath"));
            Assert.Equal(@"Git", task.MappedSourceRoots[1].GetMetadata("SourceControl"));

            Assert.Equal(@"****\|||:;\", task.MappedSourceRoots[2].ItemSpec);
            Assert.Equal(@"/_/|||:;/", task.MappedSourceRoots[2].GetMetadata("MappedPath"));
            Assert.Equal(@"Git", task.MappedSourceRoots[2].GetMetadata("SourceControl"));
        }

        [Fact]
        public void NestedRoots_Separators()
        {
            var task = new MapSourceRoots
            {
                SourceRoots = new[]
                {
                    new TaskItem(@"c:\MyProjects\MyProject\"),
                    new TaskItem(@"c:\MyProjects\MyProject\a\a\", new Dictionary<string, string>
                    {
                        { "NestedRoot", @"a/a/" },
                        { "ContainingRoot", @"c:\MyProjects\MyProject\" },
                    }),
                    new TaskItem(@"c:\MyProjects\MyProject\a\b\", new Dictionary<string, string>
                    {
                        { "NestedRoot", @"a/b\" },
                        { "ContainingRoot", @"c:\MyProjects\MyProject\" },
                    }),
                    new TaskItem(@"c:\MyProjects\MyProject\a\c\", new Dictionary<string, string>
                    {
                        { "NestedRoot", @"a\c" },
                        { "ContainingRoot", @"c:\MyProjects\MyProject\" },
                    }),
                }
            };

            task.Execute();

            Assert.Equal(4, task.MappedSourceRoots.Length);

            Assert.Equal(FileUtilities.FixFilePath(@"c:\MyProjects\MyProject\"), task.MappedSourceRoots[0].ItemSpec);
            Assert.Equal(@"/_/", task.MappedSourceRoots[0].GetMetadata("MappedPath"));

            Assert.Equal(FileUtilities.FixFilePath(@"c:\MyProjects\MyProject\a\a\"), task.MappedSourceRoots[1].ItemSpec);
            Assert.Equal(@"/_/a/a/", task.MappedSourceRoots[1].GetMetadata("MappedPath"));

            Assert.Equal(FileUtilities.FixFilePath(@"c:\MyProjects\MyProject\a\b\"), task.MappedSourceRoots[2].ItemSpec);
            Assert.Equal(@"/_/a/b/", task.MappedSourceRoots[2].GetMetadata("MappedPath"));

            Assert.Equal(FileUtilities.FixFilePath(@"c:\MyProjects\MyProject\a\c\"), task.MappedSourceRoots[3].ItemSpec);
            Assert.Equal(@"/_/a/c/", task.MappedSourceRoots[3].GetMetadata("MappedPath"));
        }

        [Fact]
        public void SourceRootCaseSensitive()
        {
            var engine = new MockEngine();

            var task = new MapSourceRoots
            {
                BuildEngine = engine,
                SourceRoots = new[]
                {
                    new TaskItem(@"c:\packages\SourcePackage1\"),
                    new TaskItem(@"C:\packages\SourcePackage1\"),
                    new TaskItem(@"c:\packages\SourcePackage2\"),
                }
            };

            task.Execute();

            Assert.Equal(3, task.MappedSourceRoots.Length);

            Assert.Equal(FileUtilities.FixFilePath(@"c:\packages\SourcePackage1\"), task.MappedSourceRoots[0].ItemSpec);
            Assert.Equal(@"/_/", task.MappedSourceRoots[0].GetMetadata("MappedPath"));

            Assert.Equal(FileUtilities.FixFilePath(@"C:\packages\SourcePackage1\"), task.MappedSourceRoots[1].ItemSpec);
            Assert.Equal(@"/_1/", task.MappedSourceRoots[1].GetMetadata("MappedPath"));

            Assert.Equal(FileUtilities.FixFilePath(@"c:\packages\SourcePackage2\"), task.MappedSourceRoots[2].ItemSpec);
            Assert.Equal(@"/_2/", task.MappedSourceRoots[2].GetMetadata("MappedPath"));
        }

        [Fact]
        public void Error_DuplicateSourceRoot()
        {
            var engine = new MockEngine();

            var task = new MapSourceRoots
            {
                BuildEngine = engine,
                SourceRoots = new[]
                {
                    new TaskItem(@"c:\packages\SourcePackage1\"),
                    new TaskItem(@"c:\packages\SourcePackage1\"),
                    new TaskItem(@"c:\packages\SourcePackage2\"),
                }
            };

            task.Execute();
            
            Assert.Null(task.MappedSourceRoots);

            Assert.Equal("ERROR : " + string.Format(task.Log.FormatResourceString(
                "MapSourceRoots.ContainsDuplicate", "SourceRoot", FileUtilities.FixFilePath(@"c:\packages\SourcePackage1\"))) + Environment.NewLine, engine.Log);
        }

        [Fact]
        public void Error_MissingContainingRoot()
        {
            var engine = new MockEngine();

            var task = new MapSourceRoots
            {
                BuildEngine = engine,
                SourceRoots = new[]
                {
                    new TaskItem(@"c:\MyProjects\MYPROJECT\"),
                    new TaskItem(@"c:\MyProjects\MyProject\a\b\", new Dictionary<string, string>
                    {
                        { "SourceControl", "Git" },
                        { "NestedRoot", "a/b" },
                        { "ContainingRoot", @"c:\MyProjects\MyProject\" },
                    }),
                }
            };

            task.Execute();

            Assert.Null(task.MappedSourceRoots);

            Assert.Equal("ERROR : " + string.Format(task.Log.FormatResourceString(
                "MapSourceRoots.ValueOfNotFoundInItems", "SourceRoot.ContainingRoot", "SourceRoot", FileUtilities.FixFilePath(@"c:\MyProjects\MyProject\"))) + Environment.NewLine, engine.Log);
        }

        [Fact]
        public void Error_NoContainingRootSpecified()
        {
            var engine = new MockEngine();

            var task = new MapSourceRoots
            {
                BuildEngine = engine,
                SourceRoots = new[]
                {
                    new TaskItem(@"c:\MyProjects\MyProject\"),
                    new TaskItem(@"c:\MyProjects\MyProject\a\b\", new Dictionary<string, string>
                    {
                        { "SourceControl", "Git" },
                        { "NestedRoot", "a/b" },
                    }),
                }
            };

            task.Execute();

            Assert.Null(task.MappedSourceRoots);

            Assert.Equal("ERROR : " + string.Format(task.Log.FormatResourceString(
                "MapSourceRoots.ValueOfNotFoundInItems", "SourceRoot.ContainingRoot", "SourceRoot", @"")) + Environment.NewLine, engine.Log);
        }
    }
}



