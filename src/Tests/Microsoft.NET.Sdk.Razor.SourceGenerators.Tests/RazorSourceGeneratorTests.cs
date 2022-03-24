// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public class RazorSourceGeneratorTests
    {
        private static readonly Project _baseProject = CreateBaseProject();

        [Fact]
        public async Task SourceGenerator_RazorFiles_Works()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
            });

            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);
        }

        [Fact]
        public async Task SourceGeneratorEvents_RazorFiles_Works()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
                e => Assert.Equal("ComputeRazorSourceGeneratorOptions", e.EventName),
                e =>
                {
                    Assert.Equal("GenerateDeclarationCodeStart", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Index.razor", file);
                },
                e =>
                {
                    Assert.Equal("GenerateDeclarationCodeStop", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Index.razor", file);
                },
                e =>
                {
                    Assert.Equal("GenerateDeclarationCodeStart", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Counter.razor", file);
                },
                e =>
                {
                    Assert.Equal("GenerateDeclarationCodeStop", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Counter.razor", file);
                },
                e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromReferencesStart", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromReferencesStop", e.EventName),
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStart", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Index.razor", file);
                },
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStop", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Index.razor", file);
                },
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStart", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Counter.razor", file);
                },
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStop", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Counter.razor", file);
                },
                e =>
                {
                    Assert.Equal("AddSyntaxTrees", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("Pages_Index_razor.g.cs", file);
                },
                e =>
                {
                    Assert.Equal("AddSyntaxTrees", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("Pages_Counter_razor.g.cs", file);
                });
        }

        [Fact]
        public async Task IncrementalCompilation_DoesNotReexecuteSteps_WhenRazorFilesAreUnchanged()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Empty(eventListener.Events);
        }

        [Fact]
        public async Task IncrementalCompilation_WhenRazorFileMarkupChanges()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Empty(eventListener.Events);

            var updatedText = new TestAdditionalText("Pages/Counter.razor", SourceText.From("<h2>Counter</h2>", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e =>
               {
                   Assert.Equal("GenerateDeclarationCodeStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("GenerateDeclarationCodeStop", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStop", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("AddSyntaxTrees", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("Pages_Counter_razor.g.cs", file);
               });
        }

        [Fact]
        public async Task IncrementalCompilation_RazorFiles_WhenNewTypeIsAdded()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            project = project.AddDocument("Person.cs", SourceText.From(@"
public class Person
{
    public string Name { get; set; }
}", Encoding.UTF8)).Project;
            compilation = await project.GetCompilationAsync();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName));
        }

        [Fact]
        public async Task IncrementalCompilation_RazorFiles_WhenCSharpTypeChanges()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            },
            new()
            {
                ["Person.cs"] = @"
public class Person
{
    public string Name { get; set; }
}"
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            project = project.Documents.First().WithText(SourceText.From(@"
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}", Encoding.UTF8)).Project;
            compilation = await project.GetCompilationAsync();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName));
        }

        [Fact]
        public async Task IncrementalCompilation_RazorFiles_WhenChildComponentsAreAdded()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Empty(eventListener.Events);

            var updatedText = new TestAdditionalText("Pages/Counter.razor", SourceText.From(@"
<h2>Counter</h2>
<h3>Current count: @count</h3>
<button @onclick=""Click"">Click me</button>

@code
{
    private int count;

    public void Click() => count++;
}

", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e =>
               {
                   Assert.Equal("GenerateDeclarationCodeStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("GenerateDeclarationCodeStop", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStop", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("AddSyntaxTrees", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("Pages_Counter_razor.g.cs", file);
               });
        }

        [Fact]
        public async Task IncrementalCompilation_RazorFiles_WhenNewComponentParameterIsAdded()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Empty(eventListener.Events);

            var updatedText = new TestAdditionalText("Pages/Counter.razor", SourceText.From(@"
<h2>Counter</h2>
<h3>Current count: @count</h3>
<button @onclick=""Click"">Click me</button>

@code
{
    private int count;

    public void Click() => count++;

    [Parameter] public int IncrementAmount { get; set; }
}

", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e =>
               {
                   Assert.Equal("GenerateDeclarationCodeStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("GenerateDeclarationCodeStop", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Index.razor", file);
               },
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStop", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Index.razor", file);
               },
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStop", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("AddSyntaxTrees", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("Pages_Counter_razor.g.cs", file);
               });
        }

        [Fact]
        public async Task IncrementalCompilation_RazorFiles_WhenProjectReferencesChange()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] =
@"
@using SurveyPromptRootNamspace;
<h1>Hello world</h1>
<SurveyPrompt />
",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("RZ10012", diagnostic.Id);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            var surveyPromptAssembly = GetSurveyPromptMetadataReference(compilation!);
            compilation = compilation!.AddReferences(surveyPromptAssembly);

            result = RunGenerator(compilation, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromReferencesStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromReferencesStop", e.EventName),
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Index.razor", file);
               },
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStop", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Index.razor", file);
               },
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStop", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Counter.razor", file);
               },
               e =>
               {
                   Assert.Equal("AddSyntaxTrees", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("Pages_Index_razor.g.cs", file);
               });

            // Verify caching
            eventListener.Events.Clear();
            result = RunGenerator(compilation, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);
            Assert.Empty(eventListener.Events);

            static MetadataReference GetSurveyPromptMetadataReference(Compilation currentCompilation)
            {
                var updatedCompilation = currentCompilation.RemoveAllSyntaxTrees()
                    .WithAssemblyName("SurveyPromptAssembly")
                    .AddSyntaxTrees(CSharpSyntaxTree.ParseText(@"
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
namespace SurveyPromptRootNamspace;
public class SurveyPrompt : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder) {}
}"));
                var stream = new MemoryStream();
                var emitResult = updatedCompilation.Emit(stream);
                Assert.True(emitResult.Success);

                stream.Position = 0;
                return MetadataReference.CreateFromStream(stream);
            }
        }


        [Fact]
        public async Task SourceGenerator_CshtmlFiles_Works()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = "<h1>Hello world</h1>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
                e => Assert.Equal("ComputeRazorSourceGeneratorOptions", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromReferencesStart", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromReferencesStop", e.EventName),
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStart", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Index.cshtml", file);
                },
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStop", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Index.cshtml", file);
                },
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStart", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Views/Shared/_Layout.cshtml", file);
                },
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStop", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Views/Shared/_Layout.cshtml", file);
                },
                e =>
                {
                    Assert.Equal("AddSyntaxTrees", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("Pages_Index_cshtml.g.cs", file);
                },
                e =>
                {
                    Assert.Equal("AddSyntaxTrees", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("Views_Shared__Layout_cshtml.g.cs", file);
                });
        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_WhenMarkupChanges()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = "<h1>Hello world</h1>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Empty(eventListener.Events);

            var updatedText = new TestAdditionalText("Views/Shared/_Layout.cshtml", SourceText.From("<h2>Updated Layout</h2>", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Views/Shared/_Layout.cshtml", file);
               },
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStop", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Views/Shared/_Layout.cshtml", file);
                },
                e =>
                {
                    Assert.Equal("AddSyntaxTrees", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("Views_Shared__Layout_cshtml.g.cs", file);
                });
        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_CSharpTypeChanges()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = "<h1>Hello world</h1>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            },
            new()
            {
                ["Person.cs"] = @"
public class Person
{
    public string Name { get; set; }
}"
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            project = project.Documents.First().WithText(SourceText.From(@"
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}", Encoding.UTF8)).Project;
            compilation = await project.GetCompilationAsync();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName));
        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_NewTagHelper()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] =
@"
@addTagHelper *, TestProject
<h2>Hello world</h2>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            project = project.AddDocument("HeaderTagHelper.cs", SourceText.From(@"
using Microsoft.AspNetCore.Razor.TagHelpers;
namespace MyApp;

[HtmlTargetElement(""h2"")]
public class HeaderTagHelper : TagHelper
{
    public override int Order => 0;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.Add(""role"", ""heading"");
    }
}", Encoding.UTF8)).Project;
            compilation = await project.GetCompilationAsync();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Pages/Index.cshtml", file);
               },
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStop", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Pages/Index.cshtml", file);
                },
               e =>
               {
                   Assert.Equal("RazorCodeGenerateStart", e.EventName);
                   var file = Assert.Single(e.Payload);
                   Assert.Equal("/Views/Shared/_Layout.cshtml", file);
               },
                e =>
                {
                    Assert.Equal("RazorCodeGenerateStop", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("/Views/Shared/_Layout.cshtml", file);
                },
                e =>
                {
                    Assert.Equal("AddSyntaxTrees", e.EventName);
                    var file = Assert.Single(e.Payload);
                    Assert.Equal("Pages_Index_cshtml.g.cs", file);
                });
        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_RazorDiagnostics_Fixed()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] =
@"
@{
<h1>Malformed h1
}
</h1>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("RZ1006", diagnostic.Id);
            Assert.Equal(2, result.GeneratedSources.Length);

            var updatedText = new TestAdditionalText("Pages/Index.cshtml", SourceText.From("<h1>Fixed header</h1>", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);
        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_RazorDiagnostics_Introduced()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = "<h1>Valid h1</h1>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            var updatedText = new TestAdditionalText("Pages/Index.cshtml", SourceText.From(@"
@{
<h1>Malformed h1
}
</h1>", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver);

            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("RZ1006", diagnostic.Id);
            Assert.Equal(2, result.GeneratedSources.Length);
        }

        [Fact]
        public async Task SourceGenerator_DoesNotAddAnyGeneratedSources_WhenSourceGeneratorIsSuppressed()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/36227
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });

            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project, optionsProvider =>
            {
                optionsProvider.TestGlobalOptions["build_property.SuppressRazorSourceGenerator"] = "true";
            });

            var result = RunGenerator(compilation!, ref driver);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.GeneratedSources);

            var updatedText = new TestAdditionalText("Pages/Index.razor", SourceText.From(@"<h1>Hello world 1</h1>", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            // Now run the source generator again with updated text that should result in a cache miss
            // and exercise comparers
            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.GeneratedSources);
        }

        [Fact]
        public async Task SourceGenerator_CorrectlyGeneratesSourcesOnceSuppressRazorSourceGeneratorIsUnset()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/36227
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] =
@"
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
<h1>Counter</h1>
<button @onclick=""@(() => {})"">Click me</button>",
            });

            var compilation = await project.GetCompilationAsync();
            TestAnalyzerConfigOptionsProvider? testOptionsProvider = null;
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project, optionsProvider =>
            {
                testOptionsProvider = optionsProvider;
                optionsProvider.TestGlobalOptions["build_property.SuppressRazorSourceGenerator"] = "true";
            });

            var result = RunGenerator(compilation!, ref driver);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.GeneratedSources);
            var updatedOptionsProvider = new TestAnalyzerConfigOptionsProvider();
            foreach (var option in testOptionsProvider!.AdditionalTextOptions)
            {
                updatedOptionsProvider.AdditionalTextOptions[option.Key] = option.Value;
            }

            foreach (var option in testOptionsProvider!.TestGlobalOptions.Options)
            {
                updatedOptionsProvider.TestGlobalOptions[option.Key] = option.Value;
            }

            updatedOptionsProvider.TestGlobalOptions["build_property.SuppressRazorSourceGenerator"] = "false";

            driver = driver.WithUpdatedAnalyzerConfigOptions(updatedOptionsProvider);
            result = RunGenerator(compilation!, ref driver);

            Assert.Collection(
                result.GeneratedSources,
                sourceResult =>
                {
                    Assert.Contains("public partial class Index", sourceResult.SourceText.ToString());
                },
                sourceResult =>
                {
                    var sourceText = sourceResult.SourceText.ToString();
                    Assert.Contains("public partial class Counter", sourceText);
                    // Regression test for https://github.com/dotnet/aspnetcore/issues/36116. Verify that @onclick is resolved as a component, and not as a regular attribute
                    Assert.Contains("__builder.AddAttribute(2, \"onclick\", global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create<global::Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this,", sourceText);
                });
        }

        private static async ValueTask<GeneratorDriver> GetDriverAsync(Project project)
        {
            var (driver, _) = await GetDriverWithAdditionalTextAsync(project);
            return driver;
        }

        private static async ValueTask<(GeneratorDriver, ImmutableArray<AdditionalText>)> GetDriverWithAdditionalTextAsync(Project project, Action<TestAnalyzerConfigOptionsProvider>? configureGlobalOptions = null)
        {
            var razorSourceGenerator = new RazorSourceGenerator().AsSourceGenerator();
            var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(new[] { razorSourceGenerator }, parseOptions: (CSharpParseOptions)project.ParseOptions!);

            var optionsProvider = new TestAnalyzerConfigOptionsProvider();
            optionsProvider.TestGlobalOptions["build_property.RazorConfiguration"] = "Default";
            optionsProvider.TestGlobalOptions["build_property.RootNamespace"] = "MyApp";
            optionsProvider.TestGlobalOptions["build_property.RazorLangVersion"] = "Latest";

            configureGlobalOptions?.Invoke(optionsProvider);

            var additionalTexts = ImmutableArray<AdditionalText>.Empty;

            foreach (var document in project.AdditionalDocuments)
            {
                var additionalText = new TestAdditionalText(document.Name, await document.GetTextAsync());
                additionalTexts = additionalTexts.Add(additionalText);

                var additionalTextOptions = new TestAnalyzerConfigOptions
                {
                    ["build_metadata.AdditionalFiles.TargetPath"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(additionalText.Path)),
                };

                optionsProvider.AdditionalTextOptions[additionalText.Path] = additionalTextOptions;
            }

            driver = driver
                .AddAdditionalTexts(additionalTexts)
                .WithUpdatedAnalyzerConfigOptions(optionsProvider);

            return (driver, additionalTexts);
        }

        private static GeneratorRunResult RunGenerator(Compilation compilation, ref GeneratorDriver driver)
        {
            driver = driver.RunGenerators(compilation);

            var result = driver.RunGenerators(compilation).GetRunResult();
            return result.Results[0];
        }

        private static Project CreateTestProject(
            Dictionary<string, string> additonalSources,
            Dictionary<string, string>? sources = null)
        {
            var project = _baseProject;

            if (sources is not null)
            {
                foreach (var (name, source) in sources)
                {
                    project = project.AddDocument(name, source).Project;
                }
            }

            foreach (var (name, source) in additonalSources)
            {
                project = project.AddAdditionalDocument(name, source).Project;
            }

            return project;
        }

        private class AppLocalResolver : ICompilationAssemblyResolver
        {
            public bool TryResolveAssemblyPaths(CompilationLibrary library, List<string>? assemblies)
            {
                foreach (var assembly in library.Assemblies)
                {
                    var dll = Path.Combine(Directory.GetCurrentDirectory(), "refs", Path.GetFileName(assembly));
                    if (File.Exists(dll))
                    {
                        assemblies!.Add(dll);
                        return true;
                    }

                    dll = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(assembly));
                    if (File.Exists(dll))
                    {
                        assemblies!.Add(dll);
                        return true;
                    }
                }

                return false;
            }
        }

        private static Project CreateBaseProject()
        {
            var projectId = ProjectId.CreateNewId(debugName: "TestProject");

            var solution = new AdhocWorkspace()
               .CurrentSolution
               .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp);

            var project = solution.Projects.Single()
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

            project = project.WithParseOptions(((CSharpParseOptions)project.ParseOptions!).WithLanguageVersion(LanguageVersion.Preview));


            foreach (var defaultCompileLibrary in DependencyContext.Load(typeof(RazorSourceGeneratorTests).Assembly)!.CompileLibraries)
            {
                foreach (var resolveReferencePath in defaultCompileLibrary.ResolveReferencePaths(new AppLocalResolver()))
                {
                    project = project.AddMetadataReference(MetadataReference.CreateFromFile(resolveReferencePath));
                }
            }

            // The deps file in the project is incorrect and does not contain "compile" nodes for some references.
            // However these binaries are always present in the bin output. As a "temporary" workaround, we'll add
            // every dll file that's present in the test's build output as a metadatareference.
            foreach (var assembly in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
            {
                if (!project.MetadataReferences.Any(c => string.Equals(Path.GetFileNameWithoutExtension(c.Display), Path.GetFileNameWithoutExtension(assembly), StringComparison.OrdinalIgnoreCase)))
                {
                    project = project.AddMetadataReference(MetadataReference.CreateFromFile(assembly));
                }
            }

            return project;
        }

        private class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
        {
            public override AnalyzerConfigOptions GlobalOptions => TestGlobalOptions;

            public TestAnalyzerConfigOptions TestGlobalOptions { get; } = new TestAnalyzerConfigOptions();

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => throw new NotImplementedException();

            public Dictionary<string, TestAnalyzerConfigOptions> AdditionalTextOptions { get; } = new();

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            {
                return AdditionalTextOptions.TryGetValue(textFile.Path, out var options) ? options : new TestAnalyzerConfigOptions();
            }
        }

        private class TestAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            public Dictionary<string, string> Options { get; } = new();

            public string this[string name]
            {
                get => Options[name];
                set => Options[name] = value;
            }

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
                => Options.TryGetValue(key, out value);
        }
    }
}
