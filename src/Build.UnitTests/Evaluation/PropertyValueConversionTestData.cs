// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Engine.UnitTests.Evaluation;

/// <summary>
///  Provides values used to verify conversion of property-function results.
/// </summary>
/// <remarks>
///  This type and its callable method are public so that property-function reflection can invoke them by
///  assembly-qualified name when unrestricted property functions are enabled for a test.
/// </remarks>
public static class PropertyValueConversionTestData
{
    /// <summary>
    ///  Creates the value for a property-value conversion scenario.
    /// </summary>
    /// <param name="scenario">The scenario identifying the value shape to create.</param>
    /// <returns>
    ///  The value to return from the test property function.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scenario"/> is not recognized.</exception>
    public static object? GetValue(string scenario)
    {
        // Keep these shapes aligned with the direct coverage in PropertyValueConverter_Tests.
        return scenario switch
        {
            "Null" => null,
            "EmptyString" => string.Empty,
            "String" => "a;b",
            "Scalar" => 42,
            "EmptyDictionary" => new Dictionary<string, object?>(),
            "EmptyArray" => Array.Empty<object>(),
            "EmptyEnumerable" => new List<object?>(),
            "Dictionary" => new Dictionary<string, object?> { ["a;b"] = "c%d" },
            "EnumerableWithEmptyElements" => new List<object?>
            {
                null,
                string.Empty,
                Array.Empty<object>(),
                "a",
                null,
                "b;c",
            },
            "NonzeroLowerBoundArray" => CreateNonzeroLowerBoundArray(),
            "MultidimensionalArray" => new[,] { { "a", "b" }, { "c", "d" } },
            "NestedEnumerable" => new List<object?>
            {
                new List<object?>
                {
                    new List<object?> { "a;b", "c" },
                },
            },
            "NestedDictionary" => new Dictionary<string, object?>
            {
                ["key"] = new List<object?> { "a;b", "c" },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        static Array CreateNonzeroLowerBoundArray()
        {
            // C# array syntax cannot express a nonzero lower bound.
            Array values = Array.CreateInstance(typeof(string), lengths: [2], lowerBounds: [1]);
            values.SetValue("a", 1);
            values.SetValue("b", 2);
            return values;
        }
    }
}
