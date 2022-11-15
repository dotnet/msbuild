// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Moq;
using Xunit;
using System.Linq;

namespace Microsoft.DotNet.ApiCompatibility.Tests.Mappers
{
    public class TypeMapperTests
    {
        [Fact]
        public void TypeMapper_Ctor_PropertiesSet()
        {
            IRuleRunner ruleRunner = Mock.Of<IRuleRunner>();
            MapperSettings mapperSettings = new();
            int rightSetSize = 5;
            INamespaceMapper containingNamespace = Mock.Of<INamespaceMapper>();
            ITypeMapper containingType = Mock.Of<ITypeMapper>();

            TypeMapper assemblyMapper = new(ruleRunner, mapperSettings, rightSetSize, containingNamespace, containingType);

            Assert.Equal(mapperSettings, assemblyMapper.Settings);
            Assert.Equal(rightSetSize, assemblyMapper.Right.Length);
            Assert.Equal(containingNamespace, assemblyMapper.ContainingNamespace);
            Assert.Equal(containingType, assemblyMapper.ContainingType);
        }

        [Fact]
        public void TypeMapper_GetNestedTypesWithoutLeftAndRight_EmptyResult()
        {
            TypeMapper typeMapper = new(Mock.Of<IRuleRunner>(), new MapperSettings(), rightSetSize: 1, Mock.Of<INamespaceMapper>());
            Assert.Empty(typeMapper.GetNestedTypes());
        }

        [Fact]
        public void TypeMapper_GetMembersWithoutLeftAndRight_EmptyResult()
        {
            TypeMapper typeMapper = new(Mock.Of<IRuleRunner>(), new MapperSettings(), rightSetSize: 1, Mock.Of<INamespaceMapper>());
            Assert.Empty(typeMapper.GetMembers());
        }

        [Fact]
        public void TypeMapper_GetNestedTypes_ReturnsExpected()
        {
            string leftSyntax = @"
public class A
{
    protected class B { }
    protected class C { }
}
";
            string rightSyntax = @"
public class A
{
    public class B { }
    public class C { }
    protected internal class D { }
}
";
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                MetadataInformation.DefaultLeft);
            ElementContainer<IAssemblySymbol> right = new(SymbolFactory.GetAssemblyFromSyntax(rightSyntax),
                MetadataInformation.DefaultRight);
            AssemblyMapper assemblyMapper = new(Mock.Of<IRuleRunner>(), new MapperSettings(), rightSetSize: 1);
            assemblyMapper.AddElement(left, ElementSide.Left);
            assemblyMapper.AddElement(right, ElementSide.Right);

            IEnumerable<INamespaceMapper> namespaceMappers = assemblyMapper.GetNamespaces();
            Assert.Single(namespaceMappers);

            IEnumerable<ITypeMapper> typeMappers = namespaceMappers.Single().GetTypes();
            Assert.Single(typeMappers);

            IEnumerable<ITypeMapper> nestedTypeMappers = typeMappers.Single().GetNestedTypes();

            Assert.Equal(3, nestedTypeMappers.Count());
            Assert.Equal(new string?[] { "B", "C", null }, nestedTypeMappers.Select(n => n.Left?.Name));
            Assert.Equal(new string[] { "B", "C", "D" }, nestedTypeMappers.SelectMany(n => n.Right).Select(r => r?.Name));
        }

        [Fact]
        public void TypeMapper_GetMembers_ReturnsExpected()
        {
            string leftSyntax = @"
public class A
{
    public A() { }
    public string B;
    public string C;
}
";
            string rightSyntax = @"
public class A
{
    public A() { }
    public string B;
    public string C;
    protected internal string D;
}
";
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                MetadataInformation.DefaultLeft);
            ElementContainer<IAssemblySymbol> right = new(SymbolFactory.GetAssemblyFromSyntax(rightSyntax),
                MetadataInformation.DefaultRight);
            AssemblyMapper assemblyMapper = new(Mock.Of<IRuleRunner>(), new MapperSettings(), rightSetSize: 1);
            assemblyMapper.AddElement(left, ElementSide.Left);
            assemblyMapper.AddElement(right, ElementSide.Right);

            IEnumerable<INamespaceMapper> namespaceMappers = assemblyMapper.GetNamespaces();
            Assert.Single(namespaceMappers);

            IEnumerable<ITypeMapper> typeMappers = namespaceMappers.Single().GetTypes();
            Assert.Single(typeMappers);

            IEnumerable<IMemberMapper> memberMappers = typeMappers.Single().GetMembers();

            Assert.Equal(4, memberMappers.Count());
            Assert.Equal(new string?[] { ".ctor", "B", "C", null }, memberMappers.Select(n => n.Left?.Name));
            Assert.Equal(new string[] { ".ctor", "B", "C", "D" }, memberMappers.SelectMany(n => n.Right).Select(r => r?.Name));
        }

        [Fact]
        public void TypeMapper_GetMembersAndGetNestedTypesWithOnlyEffectivelySealedMembersAndTypes_ReturnsEmpty()
        {
            string leftSyntax = @"
public class A
{
    private A() { }
    protected class B { }
    protected internal class C { }
}
";
            string rightSyntax = @"
public class A
{
    private A() { }
    protected class B { }
    protected internal class C { }
}
";
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                MetadataInformation.DefaultLeft);
            ElementContainer<IAssemblySymbol> right = new(SymbolFactory.GetAssemblyFromSyntax(rightSyntax),
                MetadataInformation.DefaultRight);
            AssemblyMapper assemblyMapper = new(Mock.Of<IRuleRunner>(), new MapperSettings(), rightSetSize: 1);
            assemblyMapper.AddElement(left, ElementSide.Left);
            assemblyMapper.AddElement(right, ElementSide.Right);

            IEnumerable<INamespaceMapper> namespaceMappers = assemblyMapper.GetNamespaces();
            Assert.Single(namespaceMappers);

            IEnumerable<ITypeMapper> typeMappers = namespaceMappers.Single().GetTypes();
            Assert.Single(typeMappers);

            IEnumerable<IMemberMapper> memberMappers = typeMappers.Single().GetMembers();
            Assert.Empty(memberMappers);

            IEnumerable<ITypeMapper> nestedTypeMappers = typeMappers.Single().GetNestedTypes();
            Assert.Empty(nestedTypeMappers);
        }
    }
}
