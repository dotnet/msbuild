// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Mapping;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Moq;

namespace Microsoft.DotNet.ApiCompatibility.Tests.Mapping
{
    public class AssemblySetMapperTests
    {
        [Fact]
        public void AssemblySetMapper_Ctor_PropertiesSet()
        {
            IRuleRunner ruleRunner = Mock.Of<IRuleRunner>();
            IMapperSettings mapperSettings = Mock.Of<IMapperSettings>();
            int rightSetSize = 5;

            AssemblySetMapper assemblySetMapper = new(ruleRunner, mapperSettings, rightSetSize);

            Assert.Null(assemblySetMapper.Left);
            Assert.Equal(mapperSettings, assemblySetMapper.Settings);
            Assert.Equal(rightSetSize, assemblySetMapper.Right.Length);
            Assert.Equal(0, assemblySetMapper.AssemblyCount);
        }

        [Fact]
        public void AssemblySetMapper_GetAssembliesWithoutLeftAndRight_EmptyResult()
        {
            AssemblySetMapper assemblySetMapper = new(Mock.Of<IRuleRunner>(), Mock.Of<IMapperSettings>(), rightSetSize: 1);
            Assert.Empty(assemblySetMapper.GetAssemblies());
            Assert.Equal(0, assemblySetMapper.AssemblyCount);
        }

        [Fact]
        public void AssemblySetMapper_GetAssemblies_ReturnsExpected()
        {
            string[] leftSyntaxes = new[]
            {
                @"
namespace NamespaceInAssemblyA
{
  public class First { }
}
",
                @"
namespace NamespaceInAssemblyB
{
  public class First { }
}
",
                @"
namespace NamespaceInAssemblyC
{
  public class First { }
}
"
            };
            string[] rightSyntaxes1 = new[]
{
                @"
namespace NamespaceInAssemblyA
{
  public class First { }
}
",
                @"
namespace NamespaceInAssemblyB
{
  public class First { }
}
",
                @"
namespace NamespaceInAssemblyC
{
  public class First { }
}
",
                @"
namespace NamespaceInAssemblyD
{
  public class First { }
}
"
            };
            string[] rightSyntaxes2 = new[]
{
                @"
namespace NamespaceInAssemblyA
{
  public class First { }
}
",
                @"
namespace NamespaceInAssemblyB
{
  public class First { }
}
",
                @"
namespace NamespaceInAssemblyC
{
  public class First { }
}
",
                @"
namespace NamespaceInAssemblyD
{
  public class First { }
}
"
            };
            IReadOnlyList<ElementContainer<IAssemblySymbol>> left = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(leftSyntaxes);
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right1 = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes1);
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right2 = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes2);
            AssemblySetMapper assemblySetMapper = new(Mock.Of<IRuleRunner>(), new ApiComparerSettings(), rightSetSize: 2);
            assemblySetMapper.AddElement(left, ElementSide.Left);
            assemblySetMapper.AddElement(right1, ElementSide.Right);
            assemblySetMapper.AddElement(right2, ElementSide.Right, 1);

            Assert.Equal(0, assemblySetMapper.AssemblyCount);
            IEnumerable<IAssemblyMapper> assemblyMappers = assemblySetMapper.GetAssemblies();
            Assert.Equal(4, assemblySetMapper.AssemblyCount);

            Assert.Equal(4, assemblyMappers.Count());
            Assert.Equal(new string?[] {
                    nameof(AssemblySetMapper_GetAssemblies_ReturnsExpected) + "-0",
                    nameof(AssemblySetMapper_GetAssemblies_ReturnsExpected) + "-1",
                    nameof(AssemblySetMapper_GetAssemblies_ReturnsExpected) + "-2",
                    null
                },
                assemblyMappers.Select(asm => asm.Left?.Element.Name));



            // Verify names
            int counter = 0;
            foreach (IAssemblyMapper assemblyMapper in assemblyMappers)
            {
                string expectedAssemblyName = nameof(AssemblySetMapper_GetAssemblies_ReturnsExpected) + $"-{counter}";

                Assert.Equal(2, assemblyMapper.Right.Length);
                Assert.True(assemblyMapper.Right.All(r => r?.Element.Name == expectedAssemblyName));

                counter++;
            }
        }
    }
}
