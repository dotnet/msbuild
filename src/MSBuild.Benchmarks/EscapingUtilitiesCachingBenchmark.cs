// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures cache-hit and cache-miss paths when escaping strings.
/// </summary>
/// <remarks>
///  Cache-hit inputs are explicitly primed during global setup. Iteration setup creates unseen
///  fixed-shape inputs for cache misses, and each benchmark batches them into one measured invocation.
/// </remarks>
[MemoryDiagnoser]
[RunOncePerIteration]
public class EscapingUtilitiesCachingBenchmark
{
    private const int InputIndexBits = 6;
    private const int InputCount = 1 << InputIndexBits;

    /// <summary>
    ///  A typical property or item value containing a few characters that require escaping.
    /// </summary>
    private const string FewSpecialChars = @"Reference=$(PkgPath);Version=1.0.0";

    /// <summary>
    ///  A string in which every character requires escaping.
    /// </summary>
    private const string ManySpecialChars = @"%;*?@$();'%;*?@$();'%;*?@$();'";

    /// <summary>
    ///  Characters used to vary non-escapable positions in <see cref="FewSpecialChars"/>.
    /// </summary>
    private const string NonEscapableVariantCharacters = "0123456789abcdef";

    /// <summary>
    ///  Characters used to vary escapable positions in <see cref="ManySpecialChars"/>.
    /// </summary>
    private const string EscapableVariantCharacters = "%*?@$();";

    // Shared across benchmark instances so setup never reuses cache keys created by an earlier instance.
    private static int s_generation;

    private string[] _fewSpecialCharsCacheMisses = null!;
    private string[] _manySpecialCharsCacheMisses = null!;

    [GlobalSetup]
    public void PrimeCache()
    {
        EscapingUtilities.Escape(FewSpecialChars, cache: true);
        EscapingUtilities.Escape(ManySpecialChars, cache: true);
    }

    [IterationSetup]
    public void CreateCacheMissInputs()
    {
        int generation = Interlocked.Increment(ref s_generation);

        _fewSpecialCharsCacheMisses = CreateInputs(
            FewSpecialChars,
            NonEscapableVariantCharacters,
            bitsPerCharacter: 4,
            variantCharacterCount: 10,
            generation);
        _manySpecialCharsCacheMisses = CreateInputs(
            ManySpecialChars,
            EscapableVariantCharacters,
            bitsPerCharacter: 3,
            variantCharacterCount: 13,
            generation);
    }

    [Benchmark(OperationsPerInvoke = InputCount)]
    public string EscapeWithCaching_FewSpecialChars_CacheHit()
        => EscapeRepeatedlyWithCaching(FewSpecialChars);

    [Benchmark(OperationsPerInvoke = InputCount)]
    public string EscapeWithCaching_ManySpecialChars_CacheHit()
        => EscapeRepeatedlyWithCaching(ManySpecialChars);

    [Benchmark(OperationsPerInvoke = InputCount)]
    public string EscapeWithCaching_FewSpecialChars_CacheMiss()
        => EscapeAllWithCaching(_fewSpecialCharsCacheMisses);

    [Benchmark(OperationsPerInvoke = InputCount)]
    public string EscapeWithCaching_ManySpecialChars_CacheMiss()
        => EscapeAllWithCaching(_manySpecialCharsCacheMisses);

    private static string EscapeRepeatedlyWithCaching(string input)
    {
        string result = null!;

        for (int i = 0; i < InputCount; i++)
        {
            result = EscapingUtilities.Escape(input, cache: true);
        }

        return result;
    }

    private static string EscapeAllWithCaching(string[] inputs)
    {
        string result = null!;

        foreach (string input in inputs)
        {
            result = EscapingUtilities.Escape(input, cache: true);
        }

        return result;
    }

    private static string[] CreateInputs(
        string template,
        string variantCharacters,
        int bitsPerCharacter,
        int variantCharacterCount,
        int generation)
    {
        string[] inputs = new string[InputCount];
        int characterMask = variantCharacters.Length - 1;

        for (int i = 0; i < inputs.Length; i++)
        {
            char[] characters = template.ToCharArray();
            ulong variant = ((ulong)(uint)generation << InputIndexBits) | (uint)i;

            // Preserve the template's length and escaping profile while making every key unique.
            for (int j = 0; j < variantCharacterCount; j++)
            {
                characters[j] = variantCharacters[(int)(variant & (uint)characterMask)];
                variant >>= bitsPerCharacter;
            }

            inputs[i] = new string(characters);
        }

        return inputs;
    }
}
