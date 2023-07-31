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
    public class TypeMapperTests
    {
        [Fact]
        public void TypeMapper_Ctor_PropertiesSet()
        {
            IRuleRunner ruleRunner = Mock.Of<IRuleRunner>();
            IMapperSettings mapperSettings = Mock.Of<IMapperSettings>();
            int rightSetSize = 5;
            INamespaceMapper containingNamespace = Mock.Of<INamespaceMapper>();
            ITypeMapper containingType = Mock.Of<ITypeMapper>();


            TypeMapper typeMapper = new(ruleRunner, mapperSettings, rightSetSize, containingNamespace, containingType);

            Assert.Null(typeMapper.Left);
            Assert.Equal(mapperSettings, typeMapper.Settings);
            Assert.Equal(rightSetSize, typeMapper.Right.Length);
            Assert.Equal(containingNamespace, typeMapper.ContainingNamespace);
            Assert.Equal(containingType, typeMapper.ContainingType);
        }

        [Fact]
        public void TypeMapper_GetNestedTypesWithoutLeftAndRight_EmptyResult()
        {
            TypeMapper typeMapper = new(Mock.Of<IRuleRunner>(), Mock.Of<IMapperSettings>(), rightSetSize: 1, Mock.Of<INamespaceMapper>());
            Assert.Empty(typeMapper.GetNestedTypes());
        }

        [Fact]
        public void TypeMapper_GetMembersWithoutLeftAndRight_EmptyResult()
        {
            TypeMapper typeMapper = new(Mock.Of<IRuleRunner>(), Mock.Of<IMapperSettings>(), rightSetSize: 1, Mock.Of<INamespaceMapper>());
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
            AssemblyMapper assemblyMapper = new(Mock.Of<IRuleRunner>(), new ApiComparerSettings(), rightSetSize: 1);
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
            AssemblyMapper assemblyMapper = new(Mock.Of<IRuleRunner>(), new ApiComparerSettings(), rightSetSize: 1);
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
            AssemblyMapper assemblyMapper = new(Mock.Of<IRuleRunner>(), new ApiComparerSettings(), rightSetSize: 1);
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
