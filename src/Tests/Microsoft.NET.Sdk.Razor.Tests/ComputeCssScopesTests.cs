// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.Razor.Test
{
    public class ComputeCssScopesTests
    {
        [Fact]
        public void ComputesScopes_ComputesUniqueScopes_ForCssFiles()
        {
            // Arrange
            var taskInstance = new ComputeCssScope()
            {
                ScopedCssInput = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor.css"),
                    new TaskItem("TestFiles/Pages/Index.razor.css"),
                    new TaskItem("TestFiles/Pages/Profile.razor.css"),
                },
                TargetName = "Test"
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().Be(true);
            taskInstance.ScopedCss.Select(s => s.GetMetadata("CssScope")).Should().OnlyContain(item => 
                !string.IsNullOrEmpty(item) && new Regex("b-[a-z0-9]+").IsMatch(item));

            taskInstance.ScopedCss.Select(s => s.GetMetadata("CssScope")).Should().HaveCount(3).And.OnlyHaveUniqueItems();
        }

        [Fact]
        public void ComputesScopes_ScopeVariesByTargetName()
        {
            // Arrange
            var taskInstance = new ComputeCssScope()
            {
                ScopedCssInput = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor.css"),
                    new TaskItem("TestFiles/Pages/Index.razor.css"),
                    new TaskItem("TestFiles/Pages/Profile.razor.css"),
                },
                TargetName = "Test"
            };

            // Act
            taskInstance.Execute();
            var existing = taskInstance.ScopedCss.Select(s => s.GetMetadata("CssScope")).ToArray();

            taskInstance.TargetName = "AnotherLibrary";
            var result = taskInstance.Execute();

            // Assert
            taskInstance.ScopedCss.Should().OnlyContain(newScoped => !existing.Contains(newScoped.GetMetadata("ScopedCss")));
        }

        [Fact]
        public void ComputesScopes_IsDeterministic()
        {
            // Arrange
            var taskInstance = new ComputeCssScope()
            {
                ScopedCssInput = new[]
                {
                    new TaskItem("TestFiles/Pages/Counter.razor.css"),
                    new TaskItem("TestFiles/Pages/Index.razor.css"),
                    new TaskItem("TestFiles/Pages/Profile.razor.css"),
                },
                TargetName = "Test"
            };

            // Act
            taskInstance.Execute();
            var existing = taskInstance.ScopedCss.Select(s => s.GetMetadata("CssScope")).OrderBy(id => id).ToArray();

            var result = taskInstance.Execute();

            // Assert
            var computed = taskInstance.ScopedCss.Select(newScoped => newScoped.GetMetadata("CssScope")).OrderBy(id => id).ToArray();
            computed.Should().Equal(existing);
        }

        [Fact]
        public void ComputesScopes_VariesByPath()
        {
            // Arrange
            var taskInstance = new ComputeCssScope()
            {
                ScopedCssInput = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.razor.css"),
                    new TaskItem("TestFiles/Index.razor.css"),
                },
                TargetName = "Test"
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.ScopedCss.Should().HaveCount(2);
            taskInstance.ScopedCss[0].GetMetadata("CssScope").Should().NotBe(taskInstance.ScopedCss[1].GetMetadata("CssScope"));
        }

        [Fact]
        public void ComputesScopes_PreservesUserDefinedScopes()
        {
            // Arrange
            var taskInstance = new ComputeCssScope()
            {
                ScopedCssInput = new[]
                {
                    new TaskItem("TestFiles/Pages/Index.razor.css", new Dictionary<string,string>{ ["CssScope"] = "b-predefined" }),                },
                TargetName = "Test"
            };

            // Act
            var result = taskInstance.Execute();

            // Assert
            result.Should().BeTrue();
            taskInstance.ScopedCss.Should().ContainSingle(scopedCss => scopedCss.GetMetadata("CssScope") == "b-predefined");
        }
    }
}
