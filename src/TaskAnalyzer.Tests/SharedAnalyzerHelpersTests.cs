// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

public class SharedAnalyzerHelpersTests
{
    [Fact]
    public void GetMethodsIncludingBaseTypes_SkipsImplicitAccessors()
    {
        var compilation = CreateCompilation("""
            public abstract class TaskBase
            {
                public event System.EventHandler? ImplicitChanged;

                public event System.EventHandler ExplicitChanged
                {
                    add { }
                    remove { }
                }

                public void Run() { }
            }

            public class DerivedTask : TaskBase
            {
            }
            """);

        INamedTypeSymbol derivedTask = compilation.GetTypeByMetadataName("DerivedTask").ShouldNotBeNull();
        var methods = SharedAnalyzerHelpers.GetMethodsIncludingBaseTypes(derivedTask).ToArray();

        methods.ShouldContain(method => method.Name == "Run");
        methods.ShouldContain(method => method.Name == "add_ExplicitChanged");
        methods.ShouldContain(method => method.Name == "remove_ExplicitChanged");
        methods.ShouldNotContain(method => method.Name == "add_ImplicitChanged");
        methods.ShouldNotContain(method => method.Name == "remove_ImplicitChanged");
        methods.ShouldNotContain(method => method.IsImplicitlyDeclared);
    }
}
