// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Expansion;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public sealed class PropertyValueConverter_Tests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("a;b", "a;b")]
    [InlineData(42, "42")]
    public void ConvertsScalarValues(object? value, string expected)
        => PropertyValueConverter.ToString(value).ShouldBe(expected);

    [Fact]
    public void ConvertsEmptyCollectionsToEmptyString()
    {
        PropertyValueConverter.ToString(new Hashtable()).ShouldBeEmpty();
        PropertyValueConverter.ToString(Array.Empty<object>()).ShouldBeEmpty();
        PropertyValueConverter.ToString(new List<object>()).ShouldBeEmpty();
    }

    [Fact]
    public void ConvertsDictionary()
    {
        IDictionary dictionary = new Hashtable
        {
            ["a;b"] = "c%d",
        };

        PropertyValueConverter.ToString(dictionary).ShouldBe("a%3bb=c%25d");
    }

    [Fact]
    public void ConvertsEnumerableAndPreservesEmptyElementSeparators()
    {
        List<object?> values =
        [
            null,
            string.Empty,
            Array.Empty<object>(),
            "a",
            null,
            "b;c",
        ];

        PropertyValueConverter.ToString(values).ShouldBe("a;;b%3bc");
    }

    [Fact]
    public void ConvertsOneDimensionalArrayWithNonzeroLowerBound()
    {
        Array values = Array.CreateInstance(typeof(string), lengths: [2], lowerBounds: [1]);
        values.SetValue("a", 1);
        values.SetValue("b", 2);

        PropertyValueConverter.ToString(values).ShouldBe("a;b");
    }

    [Fact]
    public void ConvertsMultidimensionalArrayInRowMajorOrder()
    {
        string[,] values =
        {
            { "a", "b" },
            { "c", "d" },
        };

        PropertyValueConverter.ToString(values).ShouldBe("a;b;c;d");
    }

    [Fact]
    public void EscapesNestedEnumerableValuesAndSeparatorsAtEachDepth()
    {
        List<object?> values =
        [
            new List<object?>
            {
                new List<object?> { "a;b", "c" },
            },
        ];

        PropertyValueConverter.ToString(values).ShouldBe("a%25253bb%253bc");
    }

    [Fact]
    public void EscapesNestedDictionaryValuesAtEachDepth()
    {
        IDictionary dictionary = new Hashtable
        {
            ["key"] = new List<object?> { "a;b", "c" },
        };

        PropertyValueConverter.ToString(dictionary).ShouldBe("key=a%253bb%3bc");
    }
}
