// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class ApplyAllCssScopesTest
    {
        [Fact]
        public void ApplyAllCssScopes_AppliesScopesToRazorFiles()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes()
            {
                RazorComponents = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor"),
                    new TaskItem("TestFiles/Pages/Index.razor"),
                },
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
        public void ApplyAllCssScopes_FailsWhenTheScopedCss_DoesNotMatchTheRazorComponent()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes()
            {
                RazorComponents = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor"),
                    new TaskItem("TestFiles/Pages/Index.razor"),
                },
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.razor.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.razor.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" }),
                    new TaskItem("TestFiles/Pages/Profile.razor.css", new Dictionary<string, string> { ["CssScope"] = "profile-scope" }),
                }
            };

            taskInstance.BuildEngine = Mock.Of<IBuildEngine>();

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
        public void ApplyAllCssScopes_FailsWhenMultipleScopedCssFiles_MatchTheSameRazorComponent()
        {
            // Arrange
            var taskInstance = new ApplyCssScopes()
            {
                RazorComponents = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor"),
                    new TaskItem("TestFiles/Pages/Index.razor"),
                },
                ScopedCss = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.razor.css", new Dictionary<string, string> { ["CssScope"] = "index-scope" }),
                    new TaskItem("TestFiles/Pages/Counter.razor.css", new Dictionary<string, string> { ["CssScope"] = "counter-scope" }),
                    new TaskItem("TestFiles/Pages/Profile.razor.css", new Dictionary<string, string>
                    {
                        ["CssScope"] = "conflict-scope",
                        ["RazorComponent"] = "TestFiles/Pages/Index.razor"
                    }),
                }
            };

            taskInstance.BuildEngine = Mock.Of<IBuildEngine>();

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeFalse();
        }
    }
}
