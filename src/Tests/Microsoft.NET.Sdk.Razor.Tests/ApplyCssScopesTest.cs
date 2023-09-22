// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class ApplyAllCssScopesTest
    {
        [Fact]
        public void ApplyAllCssScopes_AppliesScopesToRazorComponentFiles()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes()
            {
                RazorComponents = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor"),
                    new TaskItem("TestFiles/Pages/Index.razor"),
                },
                RazorGenerate = Array.Empty<ITaskItem>(),
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.razor.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.razor.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" }),
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.RazorComponentsWithScopes.Should().HaveCount(2);
            taskInstance.RazorComponentsWithScopes.Should().ContainSingle(rcws => rcws.ItemSpec == "TestFiles/Pages/Index.razor" && rcws.GetMetadata("CssScope") == "index-scope");
            taskInstance.RazorComponentsWithScopes.Should().ContainSingle(rcws => rcws.ItemSpec == "TestFiles/Pages/Counter.razor" && rcws.GetMetadata("CssScope") == "counter-scope");
        }

        [Fact]
        public void ApplyAllCssScopes_AppliesScopesToRazorViewFiles()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes()
            {
                RazorGenerate = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.cshtml"),
                    new TaskItem("TestFiles/Pages/Index.cshtml"),
                },
                RazorComponents = Array.Empty<ITaskItem>(),
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" }),
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.RazorGenerateWithScopes.Should().HaveCount(2);
            taskInstance.RazorGenerateWithScopes.Should().ContainSingle(rcws => rcws.ItemSpec == "TestFiles/Pages/Index.cshtml" && rcws.GetMetadata("CssScope") == "index-scope");
            taskInstance.RazorGenerateWithScopes.Should().ContainSingle(rcws => rcws.ItemSpec == "TestFiles/Pages/Counter.cshtml" && rcws.GetMetadata("CssScope") == "counter-scope");
        }

        [Fact]
        public void DoesNotApplyCssScopes_ToRazorComponentsWithoutAssociatedFiles()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes()
            {
                RazorComponents = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor"),
                    new TaskItem("TestFiles/Pages/Index.razor"),
                    new TaskItem("TestFiles/Pages/FetchData.razor"),
                },
                RazorGenerate = Array.Empty<ITaskItem>(),
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.razor.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.razor.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" })
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            Assert.True(result);
            result.Should().BeTrue();
            taskInstance.RazorComponentsWithScopes.Should().NotContain(rcws => rcws.ItemSpec == "TestFiles/Pages/Fetchdata.razor");
        }

        [Fact]
        public void DoesNotApplyCssScopes_ToRazorViewsWithoutAssociatedFiles()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes()
            {
                RazorGenerate = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.cshtml"),
                    new TaskItem("TestFiles/Pages/Index.cshtml"),
                    new TaskItem("TestFiles/Pages/FetchData.cshtml"),
                },
                RazorComponents = Array.Empty<ITaskItem>(),
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" })
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            Assert.True(result);
            result.Should().BeTrue();
            taskInstance.RazorGenerateWithScopes.Should().NotContain(rcws => rcws.ItemSpec == "TestFiles/Pages/Fetchdata.razor");
        }

        [Fact]
        public void ApplyAllCssScopes_FailsWhenTheScopedCss_DoesNotMatchTheRazorComponent()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes
            {
                RazorComponents = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor"),
                    new TaskItem("TestFiles/Pages/Index.razor"),
                },
                RazorGenerate = Array.Empty<ITaskItem>(),
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.razor.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.razor.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" }),
                    new TaskItem("TestFiles/Pages/Profile.razor.css", new Dictionary<string, string> { ["CssScope"] = "profile-scope" }),
                },
                BuildEngine = Mock.Of<IBuildEngine>()
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ApplyAllCssScopes_FailsWhenTheScopedCss_DoesNotMatchTheRazorView()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes
            {
                RazorGenerate = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.cshtml"),
                    new TaskItem("TestFiles/Pages/Index.cshtml"),
                },
                RazorComponents = Array.Empty<ITaskItem>(),
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" }),
                    new TaskItem("TestFiles/Pages/Profile.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "profile-scope" }),
                },
                BuildEngine = Mock.Of<IBuildEngine>()
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ScopedCssCanDefineAssociatedRazorComponentFile()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes()
            {
                RazorComponents = new[]
                {
                    new TaskItem("TestFiles/Pages/FetchData.razor")
                },
                RazorGenerate = Array.Empty<ITaskItem>(),
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Profile.razor.css", new Dictionary<string, string>
                    {
                        ["CssScope"] = "fetchdata-scope",
                        ["RazorComponent"] = "TestFiles/Pages/FetchData.razor"
                    })
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.RazorComponentsWithScopes.Should().ContainSingle(rcws => rcws.ItemSpec == "TestFiles/Pages/FetchData.razor" && rcws.GetMetadata("CssScope") == "fetchdata-scope");
        }

        [Fact]
        public void ScopedCssCanDefineAssociatedRazorGenerateFile()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes()
            {
                RazorGenerate = new[]
                {
                    new TaskItem("TestFiles/Pages/FetchData.cshtml")
                },
                RazorComponents = Array.Empty<ITaskItem>(),
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Profile.cshtml.css", new Dictionary<string, string>
                    {
                        ["CssScope"] = "fetchdata-scope",
                        ["View"] = "TestFiles/Pages/FetchData.cshtml"
                    })
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.RazorGenerateWithScopes.Should().ContainSingle(rcws => rcws.ItemSpec == "TestFiles/Pages/FetchData.cshtml" && rcws.GetMetadata("CssScope") == "fetchdata-scope");
        }

        [Fact]
        public void ApplyAllCssScopes_FailsWhenMultipleScopedCssFiles_MatchTheSameRazorComponent()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes
            {
                RazorComponents = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor"),
                    new TaskItem("TestFiles/Pages/Index.razor"),
                },
                RazorGenerate = Array.Empty<ITaskItem>(),
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.razor.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.razor.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" }),
                    new TaskItem("TestFiles/Pages/Profile.razor.css", new Dictionary<string, string>
                    {
                        ["CssScope"] = "conflict-scope",
                        ["RazorComponent"] = "TestFiles/Pages/Index.razor"
                    }),
                },
                BuildEngine = Mock.Of<IBuildEngine>()
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ApplyAllCssScopes_FailsWhenMultipleScopedCssFiles_MatchTheSameRazorView()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes
            {
                RazorGenerate = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.cshtml"),
                    new TaskItem("TestFiles/Pages/Index.cshtml"),
                },
                RazorComponents = Array.Empty<ITaskItem>(),
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" }),
                    new TaskItem("TestFiles/Pages/Profile.cshtml.css", new Dictionary<string, string>
                    {
                        ["CssScope"] = "conflict-scope",
                        ["View"] = "TestFiles/Pages/Index.cshtml"
                    }),
                },
                BuildEngine = Mock.Of<IBuildEngine>()
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ApplyAllCssScopes_AppliesScopesToRazorComponentAndViewFiles()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes()
            {
                RazorComponents = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor"),
                    new TaskItem("TestFiles/Pages/Index.razor"),
                },
                RazorGenerate = new[]
                {
                    new TaskItem("TestFiles/Pages/Home.cshtml"),
                    new TaskItem("TestFiles/Pages/_Host.cshtml"),
                },
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Home.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "home-scope" }),
                    new TaskItem("TestFiles/Pages/_Host.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "_host-scope" }),
                    new TaskItem("TestFiles/Pages/Index.razor.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.razor.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" }),
                }
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.RazorComponentsWithScopes.Should().HaveCount(2);
            taskInstance.RazorComponentsWithScopes.Should().ContainSingle(rcws => rcws.ItemSpec == "TestFiles/Pages/Index.razor" && rcws.GetMetadata("CssScope") == "index-scope");
            taskInstance.RazorComponentsWithScopes.Should().ContainSingle(rcws => rcws.ItemSpec == "TestFiles/Pages/Counter.razor" && rcws.GetMetadata("CssScope") == "counter-scope");

            taskInstance.RazorGenerateWithScopes.Should().HaveCount(2);
            taskInstance.RazorGenerateWithScopes.Should().ContainSingle(rcws => rcws.ItemSpec == "TestFiles/Pages/Home.cshtml" && rcws.GetMetadata("CssScope") == "home-scope");
            taskInstance.RazorGenerateWithScopes.Should().ContainSingle(rcws => rcws.ItemSpec == "TestFiles/Pages/_Host.cshtml" && rcws.GetMetadata("CssScope") == "_host-scope");
        }

        [Fact]
        public void ApplyAllCssScopes_ScopedCssComponentsDontMatchWithScopedCssViewStylesAndViceversa()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes
            {
                RazorComponents = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor"),
                    new TaskItem("TestFiles/Pages/Index.razor"),
                },
                RazorGenerate = new[]
                {
                    new TaskItem("TestFiles/Pages/Home.cshtml"),
                    new TaskItem("TestFiles/Pages/_Host.cshtml"),
                },
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Home.razor.css", new Dictionary<string, string> { ["CssScope"] = "home-scope" }),
                    new TaskItem("TestFiles/Pages/_Host.razor.css", new Dictionary<string, string> { ["CssScope"] = "_host-scope" }),
                    new TaskItem("TestFiles/Pages/Index.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.cshtml.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" }),
                },
                BuildEngine = Mock.Of<IBuildEngine>()
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeFalse();
        }
    }
}
