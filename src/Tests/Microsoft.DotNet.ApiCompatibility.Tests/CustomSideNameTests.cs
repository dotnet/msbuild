// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class CustomSideNameTests
    {
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

            ApiComparer differ = new();
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            string expectedLeftName = "left";
            string expectedRightName = "right";
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

            ApiComparer differ = new();
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            string expectedLeftName = "ref/net6.0/a.dll";
            string expectedRightName = "lib/net6.0/a.dll";
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right }, leftName: expectedLeftName, rightName: expectedRightName);
            Assert.Single(differences);
            AssertNames(differences.First(), expectedLeftName, expectedRightName);

            // Use the single assembly override
            differences = differ.GetDifferences(left, right, leftName: expectedLeftName, rightName: expectedRightName);
            Assert.Single(differences);
            AssertNames(differences.First(), expectedLeftName, expectedRightName);
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

            ApiComparer differ = new();
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            string expectedLeftName = "ref/net6.0/a.dll";
            string expectedRightName = "lib/net6.0/a.dll";
            differ.StrictMode = true;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right }, leftName: expectedLeftName, rightName: expectedRightName);
            Assert.Single(differences);
            AssertNames(differences.First(), expectedLeftName, expectedRightName, leftFirst: false);
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

            ApiComparer differ = new();
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref/net6.0/a.dll"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            int i = 0;
            foreach ((MetadataInformation, MetadataInformation, IEnumerable<CompatDifference> differences) diff in differences)
            {
                Assert.Single(diff.differences);
                AssertNames(diff.differences.First(), left.MetadataInformation.AssemblyId, right[i++].MetadataInformation.AssemblyId);
            }
        }

        private void AssertNames(CompatDifference difference, string expectedLeftName, string expectedRightName, bool leftFirst = true)
        {
            string message = difference.Message;

            // make sure it is separater by a space and it is not a substr of a word.
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
