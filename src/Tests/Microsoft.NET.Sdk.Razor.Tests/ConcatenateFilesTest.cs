// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.Razor.Test
{
    public class ConcatenateCssFilesTest
    {
        private static readonly string BundleContent =
@"/* _content/Test/TestFiles/Generated/Counter.razor.rz.scp.css */
.counter {
    font-size: 2rem;
}
/* _content/Test/TestFiles/Generated/Index.razor.rz.scp.css */
.index {
    font-weight: bold;
}
";

        private static readonly string BundleWithImportsContent =
@"@import '_content/Test/TestFiles/Generated/lib.bundle.scp.css';
@import 'TestFiles/Generated/package.bundle.scp.css';

/* _content/Test/TestFiles/Generated/Counter.razor.rz.scp.css */
.counter {
    font-size: 2rem;
}
/* _content/Test/TestFiles/Generated/Index.razor.rz.scp.css */
.index {
    font-weight: bold;
}
";

        private static readonly string UpdatedBundleContent =
@"/* _content/Test/TestFiles/Generated/Counter.razor.rz.scp.css */
.counter {
    font-size: 2rem;
}
/* _content/Test/TestFiles/Generated/FetchData.razor.rz.scp.css */
.fetchData {
    font-family: Helvetica;
}
/* _content/Test/TestFiles/Generated/Index.razor.rz.scp.css */
.index {
    font-weight: bold;
}
";

        [Fact]
        public void BundlesScopedCssFiles_ProducesEmpyBundleIfNoFilesAvailable()
        {
            // Arrange
            var expectedFile = Path.Combine(Directory.GetCurrentDirectory(), $"{Guid.NewGuid():N}.css");
            var taskInstance = new ConcatenateCssFiles()
            {
                ScopedCssFiles = Array.Empty<ITaskItem>(),
                ProjectBundles = Array.Empty<ITaskItem>(),
                OutputFile = expectedFile
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            File.Exists(expectedFile).Should().BeTrue();
            File.ReadAllText(expectedFile).Should().BeEmpty();
        }

        [Fact]
        public void BundlesScopedCssFiles_ProducesBundle()
        {
            // Arrange
            var expectedFile = Path.Combine(Directory.GetCurrentDirectory(), $"{Guid.NewGuid():N}.css");
            var taskInstance = new ConcatenateCssFiles()
            {
                ScopedCssFiles = new[]
                {
                    new TaskItem(
                        "TestFiles/Generated/Counter.razor.rz.scp.css",
                        new Dictionary<string,string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Counter.razor.rz.scp.css",
                        }),
                    new TaskItem(
                        "TestFiles/Generated/Index.razor.rz.scp.css",
                        new Dictionary<string,string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Index.razor.rz.scp.css",
                        }),
                },
                ProjectBundles = Array.Empty<ITaskItem>(),
                OutputFile = expectedFile
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            File.Exists(expectedFile).Should().BeTrue();

            var actualContents = File.ReadAllText(expectedFile);
            actualContents.Should().Contain(BundleContent);
        }

        [Fact]
        public void BundlesScopedCssFiles_IncludesOtherBundles()
        {
            // Arrange
            var expectedFile = Path.Combine(Directory.GetCurrentDirectory(), $"{Guid.NewGuid():N}.css");
            var taskInstance = new ConcatenateCssFiles()
            {
                ScopedCssFiles = new[]
                {
                    new TaskItem(
                        "TestFiles/Generated/Counter.razor.rz.scp.css",
                        new Dictionary<string, string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Counter.razor.rz.scp.css",
                        }),
                    new TaskItem(
                        "TestFiles/Generated/Index.razor.rz.scp.css",
                        new Dictionary<string, string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Index.razor.rz.scp.css",
                        }),
                },
                ProjectBundles = new[]
                {
                    new TaskItem(
                        "TestFiles/Generated/lib.bundle.scp.css",
                        new Dictionary<string, string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/lib.bundle.scp.css",
                        }),
                    new TaskItem(
                        "TestFiles/Generated/package.bundle.scp.css",
                        new Dictionary<string, string>
                        {
                            ["BasePath"] = "",
                            ["RelativePath"] = "TestFiles/Generated/package.bundle.scp.css",
                        }),
                },
                ScopedCssBundleBasePath = "/",
                OutputFile = expectedFile
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            File.Exists(expectedFile).Should().BeTrue();

            var actualContents = File.ReadAllText(expectedFile);
            actualContents.Should().Contain(BundleWithImportsContent);
        }

        [Theory]
        [InlineData("", "", "TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("/", "/", "TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("app", "_content", "../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("app", "/_content", "../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("app", "/_content/", "../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("/app", "_content", "../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("/app", "/_content", "../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("/app", "/_content/", "../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("app/", "_content", "../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("app/", "/_content", "../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("app/", "/_content/", "../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("/company/app/", "_content", "../../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("/company/app/", "/_content", "../../_content/TestFiles/Generated/lib.bundle.scp.css")]
        [InlineData("/company/app/", "/_content/", "../../_content/TestFiles/Generated/lib.bundle.scp.css")]
        public void BundlesScopedCssFiles_HandlesBasePathCombinationsCorrectly(string finalBasePath, string libraryBasePath, string expectedImport)
        {
            // Arrange
            var expectedContent = BundleWithImportsContent
                .Replace("_content/Test/TestFiles/Generated/lib.bundle.scp.css", expectedImport)
                .Replace("@import 'TestFiles/Generated/package.bundle.scp.css';", "")
                .Replace("\r\n", "\n")
                .Replace("\n\n", "\n");

            var expectedFile = Path.Combine(Directory.GetCurrentDirectory(), $"{Guid.NewGuid():N}.css");
            var taskInstance = new ConcatenateCssFiles()
            {
                ScopedCssFiles = new[]
                {
                    new TaskItem(
                        "TestFiles/Generated/Counter.razor.rz.scp.css",
                        new Dictionary<string,string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Counter.razor.rz.scp.css",
                        }),
                    new TaskItem(
                        "TestFiles/Generated/Index.razor.rz.scp.css",
                        new Dictionary<string,string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Index.razor.rz.scp.css",
                        })
                },
                ProjectBundles = new[]
                {
                    new TaskItem(
                        "TestFiles/Generated/lib.bundle.scp.css",
                        new Dictionary<string, string>
                        {
                            ["BasePath"] = libraryBasePath,
                            ["RelativePath"] = "TestFiles/Generated/lib.bundle.scp.css",
                        }),
                },
                ScopedCssBundleBasePath = finalBasePath,
                OutputFile = expectedFile
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            File.Exists(expectedFile).Should().BeTrue();

            var actualContents = File.ReadAllText(expectedFile);
            actualContents.Should().BeVisuallyEquivalentTo(expectedContent);
        }

        [Fact]
        public void BundlesScopedCssFiles_BundlesFilesInOrder()
        {
            // Arrange
            var expectedFile = Path.Combine(Directory.GetCurrentDirectory(), $"{Guid.NewGuid():N}.css");
            var taskInstance = new ConcatenateCssFiles()
            {
                ScopedCssFiles = new[]
                {
                    new TaskItem(
                        "TestFiles/Generated/Index.razor.rz.scp.css",
                        new Dictionary<string,string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Index.razor.rz.scp.css",
                        }),
                    new TaskItem(
                        "TestFiles/Generated/Counter.razor.rz.scp.css",
                        new Dictionary<string,string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Counter.razor.rz.scp.css",
                        }),
                },
                ProjectBundles = Array.Empty<ITaskItem>(),
                OutputFile = expectedFile
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            File.Exists(expectedFile).Should().BeTrue();

            var actualContents = File.ReadAllText(expectedFile);
            actualContents.Should().Contain(BundleContent);
        }

        [Fact]
        public void BundlesScopedCssFiles_DoesNotOverrideBundleForSameContents()
        {
            // Arrange
            var expectedFile = Path.Combine(Directory.GetCurrentDirectory(), $"{Guid.NewGuid():N}.css");
            var taskInstance = new ConcatenateCssFiles()
            {
                ScopedCssFiles = new[]
                {
                    new TaskItem(
                        "TestFiles/Generated/Index.razor.rz.scp.css",
                        new Dictionary<string,string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Index.razor.rz.scp.css",
                        }),
                    new TaskItem(
                        "TestFiles/Generated/Counter.razor.rz.scp.css",
                        new Dictionary<string,string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Counter.razor.rz.scp.css",
                        }),
                },
                ProjectBundles = Array.Empty<ITaskItem>(),
                OutputFile = expectedFile
            };

            // Act
            var result = taskInstance.Execute();

            var lastModified = File.GetLastWriteTimeUtc(expectedFile);

            taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            File.Exists(expectedFile).Should().BeTrue();
            var actualContents = File.ReadAllText(expectedFile);
            actualContents.Should().Contain(BundleContent);

            lastModified.Should().BeSameDateAs(File.GetLastWriteTimeUtc(expectedFile));
        }

        [Fact]
        public async System.Threading.Tasks.Task BundlesScopedCssFiles_UpdatesBundleWhenContentsChange()
        {
            // Arrange
            var expectedFile = Path.Combine(Directory.GetCurrentDirectory(), $"{Guid.NewGuid():N}.css");
            var taskInstance = new ConcatenateCssFiles()
            {
                ScopedCssFiles = new[]
                {
                    new TaskItem(
                        "TestFiles/Generated/Index.razor.rz.scp.css",
                        new Dictionary<string,string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Index.razor.rz.scp.css",
                        }),
                    new TaskItem(
                        "TestFiles/Generated/Counter.razor.rz.scp.css",
                        new Dictionary<string,string>
                        {
                            ["BasePath"] = "_content/Test/",
                            ["RelativePath"] = "TestFiles/Generated/Counter.razor.rz.scp.css",
                        }),
                },
                ProjectBundles = Array.Empty<ITaskItem>(),
                OutputFile = expectedFile
            };

            // Act
            var result = taskInstance.Execute();

            var lastModified = File.GetLastWriteTimeUtc(expectedFile);

            taskInstance.ScopedCssFiles = new[]
            {
                new TaskItem(
                    "TestFiles/Generated/Index.razor.rz.scp.css",
                    new Dictionary<string,string>
                    {
                        ["BasePath"] = "_content/Test/",
                        ["RelativePath"] = "TestFiles/Generated/Index.razor.rz.scp.css",
                    }),
                new TaskItem(
                    "TestFiles/Generated/Counter.razor.rz.scp.css",
                    new Dictionary<string,string>
                    {
                        ["BasePath"] = "_content/Test/",
                        ["RelativePath"] = "TestFiles/Generated/Counter.razor.rz.scp.css",
                    }),
                new TaskItem(
                    "TestFiles/Generated/FetchData.razor.rz.scp.css",
                    new Dictionary<string,string>
                    {
                        ["BasePath"] = "_content/Test/",
                        ["RelativePath"] = "TestFiles/Generated/FetchData.razor.rz.scp.css",
                    }),
            };

            await System.Threading.Tasks.Task.Delay(1000);
            taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            File.Exists(expectedFile).Should().BeTrue();
            var actualContents = File.ReadAllText(expectedFile);

            actualContents.Should().Contain(UpdatedBundleContent);
            lastModified.Should().NotBe(File.GetLastWriteTimeUtc(expectedFile));
        }
    }
}
