// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Mapping;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;
using Moq;

namespace Microsoft.DotNet.ApiCompatibility.Tests.Mapping
{
    public class AssemblyMapperTests
    {
        [Fact]
        public void AssemblyMapper_Ctor_PropertiesSet()
        {
            IRuleRunner ruleRunner = Mock.Of<IRuleRunner>();
            IMapperSettings mapperSettings = Mock.Of<IMapperSettings>();
            int rightSetSize = 5;
            IAssemblySetMapper assemblySetMapper = Mock.Of<IAssemblySetMapper>();

            AssemblyMapper assemblyMapper = new(ruleRunner, mapperSettings, rightSetSize, assemblySetMapper);

            Assert.Null(assemblyMapper.Left);
            Assert.Equal(mapperSettings, assemblyMapper.Settings);
            Assert.Equal(rightSetSize, assemblyMapper.Right.Length);
            Assert.Equal(assemblySetMapper, assemblyMapper.ContainingAssemblySet);
        }

        [Fact]
        public void AssemblyMapper_GetNamespacesWithoutLeftAndRight_EmptyResult()
        {
            AssemblyMapper assemblyMapper = new(Mock.Of<IRuleRunner>(), Mock.Of<IMapperSettings>(), rightSetSize: 1);
            Assert.Empty(assemblyMapper.GetNamespaces());
        }

        [Fact]
        public void AssemblyMapper_GetNamespaces_ReturnsExpected()
        {
            string leftSyntax = @"
namespace AssemblyMapperTestNamespace1
{
    public class A { }
}
namespace AssemblyMapperTestNamespace2
{
    public class A { }
}
";
            string rightSyntax = @"
namespace AssemblyMapperTestNamespace1
{
    public class A { }
}
namespace AssemblyMapperTestNamespace2
{
    public class A { }
}
namespace AssemblyMapperTestNamespace3
{
    public class A { }
}
";
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                MetadataInformation.DefaultLeft);
            ElementContainer<IAssemblySymbol> right = new(SymbolFactory.GetAssemblyFromSyntax(rightSyntax),
                MetadataInformation.DefaultRight);
            AssemblyMapper assemblyMapper = new(Mock.Of<IRuleRunner>(), new ApiComparerSettings(), rightSetSize: 1);
            assemblyMapper.AddElement(left, ElementSide.Left);
            assemblyMapper.AddElement(right, ElementSide.Right);

            IEnumerable<INamespaceMapper> namespaceMappers = assemblyMapper.GetNamespaces();

            Assert.Equal(3, namespaceMappers.Count());
            Assert.Equal(new string?[] { "AssemblyMapperTestNamespace1", "AssemblyMapperTestNamespace2", null }, namespaceMappers.Select(n => n.Left?.Name));
            Assert.Equal(new string[] { "AssemblyMapperTestNamespace1", "AssemblyMapperTestNamespace2", "AssemblyMapperTestNamespace3" }, namespaceMappers.SelectMany(n => n.Right).Select(r => r?.Name));
        }
    }
}
