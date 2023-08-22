// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class CustomSideNameTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new MembersMustExist(settings, context));

        [Fact]
        public void CustomSideNameAreNotSpecified()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  public class Second { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            string expectedLeftName = "left";
            string expectedRightName = "right";
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            Assert.Single(differences);
            AssertNames(differences.First(), expectedLeftName, expectedRightName);
        }

        [Fact]
        public void CustomSideNamesAreUsed()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First
  {
    public string Method1() => string.Empty;
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            bool enableNullable = false;
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable),
                new MetadataInformation("a.dll", "ref/net6.0/a.dll"));
            ElementContainer<IAssemblySymbol> right = new(SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable),
                new MetadataInformation("a.dll", "lib/net6.0/a.dll"));
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            Assert.Single(differences);
            AssertNames(differences.First(), left.MetadataInformation.DisplayString, right.MetadataInformation.DisplayString);

            // Use the single assembly override
            differences = differ.GetDifferences(left, right);
            Assert.Single(differences);
            AssertNames(differences.First(), left.MetadataInformation.DisplayString, right.MetadataInformation.DisplayString);
        }

        [Fact]
        public void CustomSideNamesAreUsedStrictMode()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Method1() => string.Empty;
  }
}
";

            bool enableNullable = false;
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable),
                new MetadataInformation("a.dll", "ref/net6.0/a.dll"));
            ElementContainer<IAssemblySymbol> right = new(SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable),
                new MetadataInformation("a.dll", "lib/net6.0/a.dll"));
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            Assert.Single(differences);
            AssertNames(differences.First(), left.MetadataInformation.DisplayString, right.MetadataInformation.DisplayString, leftFirst: false);
        }

        [Fact]
        public void MultipleRightsMetadataInformationIsUsedAsName()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
          public string MyField;
        }
      }
    }
  }
}
";
            string[] rightSyntaxes = new[]
            { @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
        }
      }
    }
  }
}
",
            @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
        }
      }
    }
  }
}
",
            @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
        }
      }
    }
  }
}
"};
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation("a.dll", "ref/net6.0/a.dll"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Equal(right.Count, differences.Count());
            foreach (CompatDifference difference in differences)
            {
                AssertNames(difference, difference.Left.AssemblyId, difference.Right.AssemblyId);
            }
        }

        private void AssertNames(CompatDifference difference, string expectedLeftName, string expectedRightName, bool leftFirst = true)
        {
            string message = difference.Message;

            // make sure it is separated by a space and is not a substring of a word.
            string left = " " + expectedLeftName;
            string right = " " + expectedRightName;
            if (leftFirst)
            {
                Assert.Contains(left + " ", message);
                Assert.EndsWith(right, message);
            }
            else
            {
                Assert.Contains(right + " ", message);
                Assert.EndsWith(left, message);
            }
        }
    }
}
