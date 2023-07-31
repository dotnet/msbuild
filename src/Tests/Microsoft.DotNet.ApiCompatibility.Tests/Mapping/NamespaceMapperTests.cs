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
    public class NamespaceMapperTests
    {
        [Fact]
        public void NamespaceMapper_Ctor_PropertiesSet()
        {
            IRuleRunner ruleRunner = Mock.Of<IRuleRunner>();
            IMapperSettings mapperSettings = Mock.Of<IMapperSettings>();
            int rightSetSize = 5;
            IAssemblyMapper assemblyMapper = Mock.Of<IAssemblyMapper>();

            NamespaceMapper namespaceMapper = new(ruleRunner, mapperSettings, rightSetSize, assemblyMapper);

            Assert.Null(namespaceMapper.Left);
            Assert.Equal(mapperSettings, namespaceMapper.Settings);
            Assert.Equal(rightSetSize, namespaceMapper.Right.Length);
            Assert.Equal(assemblyMapper, namespaceMapper.ContainingAssembly);
        }

        [Fact]
        public void NamespaceMapper_GetTypesWithoutLeftAndRight_EmptyResult()
        {
            NamespaceMapper namespaceMapper = new(Mock.Of<IRuleRunner>(), Mock.Of<IMapperSettings>(), rightSetSize: 1, Mock.Of<IAssemblyMapper>());
            Assert.Empty(namespaceMapper.GetTypes());
        }

        [Fact]
        public void NamespaceMapper_GetTypes_ReturnsExpected()
        {
            string leftSyntax = @"
namespace NamespaceMapper
{
    public class A { }
}
";
            string rightSyntax = @"
namespace NamespaceMapper
{
    public class A { }
    public class B { }
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
            Assert.Single(namespaceMappers);

            IEnumerable<ITypeMapper> typeMappers = namespaceMappers.Single().GetTypes();
            Assert.Equal(2, typeMappers.Count());

            Assert.Equal(new string?[] { "A", null }, typeMappers.Select(n => n.Left?.Name));
            Assert.Equal(new string[] { "A", "B" }, typeMappers.SelectMany(n => n.Right).Select(r => r?.Name));
        }
    }
}
