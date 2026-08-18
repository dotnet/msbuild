// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class EscapingUtilitiesBenchmark
{
    /// <summary>
    /// A typical file path with no special characters — the most common fast path.
    /// </summary>
    private const string NoSpecialChars = @"C:\repos\msbuild\src\Framework\EscapingUtilities.cs";

    /// <summary>
    /// A string with a few characters that need escaping (semicolons, parens, percent).
    /// Represents a realistic property or item value.
    /// </summary>
    private const string FewSpecialChars = @"Reference=$(PkgPath);Version=1.0.0";

    /// <summary>
    /// A string where most characters are escapable — worst case for escaping.
    /// </summary>
    private const string ManySpecialChars = @"%;*?@$();'%;*?@$();'%;*?@$();'";

    /// <summary>
    /// An already-escaped string with a few %XX sequences — common unescape input.
    /// </summary>
    private const string FewEscapeSequences = @"Reference%3d%24%28PkgPath%29%3BVersion%3d1.0.0";

    /// <summary>
    /// A heavily-escaped string — worst case for unescaping.
    /// </summary>
    private const string ManyEscapeSequences = @"%25%3b%2a%3f%40%24%28%29%3b%27%25%3b%2a%3f%40%24%28%29%3b%27";

    /// <summary>
    /// A string with partial/invalid escape sequences that must be skipped.
    /// </summary>
    private const string InvalidEscapeSequences = @"100%done%Z%2%";

    /// <summary>
    /// An escaped string with leading/trailing whitespace for the trim path.
    /// </summary>
    private const string EscapedWithWhitespace = @"   foo%20bar%3Bbaz   ";

    /// <summary>
    /// An escaped string containing wildcard escape sequences (%2a, %3f).
    /// </summary>
    private const string EscapedWildcards = @"src\**\%2a.cs%3f";

    /// <summary>
    /// A long escaped string without wildcard sequences — worst case scan for ContainsEscapedWildcards.
    /// </summary>
    private const string LongNoWildcards = @"abcdefghijklmnopqrstuvwxyz%3babcdefghijklmnopqrstuvwxyz%3babcdefghijklmnopqrstuvwxyz%3babcdefghijklmnopqrstuvwxyz";

    // --- UnescapeAll ---

    [Benchmark]
    public string UnescapeAll_NoSpecialChars()
        => EscapingUtilities.UnescapeAll(NoSpecialChars);

    [Benchmark]
    public string UnescapeAll_FewEscapeSequences()
        => EscapingUtilities.UnescapeAll(FewEscapeSequences);

    [Benchmark]
    public string UnescapeAll_ManyEscapeSequences()
        => EscapingUtilities.UnescapeAll(ManyEscapeSequences);

    [Benchmark]
    public string UnescapeAll_InvalidEscapeSequences()
        => EscapingUtilities.UnescapeAll(InvalidEscapeSequences);

    [Benchmark]
    public string UnescapeAll_WithTrim()
        => EscapingUtilities.UnescapeAll(EscapedWithWhitespace, trim: true);

    // --- Escape ---

    [Benchmark]
    public string Escape_NoSpecialChars()
        => EscapingUtilities.Escape(NoSpecialChars);

    [Benchmark]
    public string Escape_FewSpecialChars()
        => EscapingUtilities.Escape(FewSpecialChars);

    [Benchmark]
    public string Escape_ManySpecialChars()
        => EscapingUtilities.Escape(ManySpecialChars);

    // --- EscapeWithCaching ---

    [Benchmark]
    public string EscapeWithCaching_FewSpecialChars()
        => EscapingUtilities.Escape(FewSpecialChars, cache: true);

    [Benchmark]
    public string EscapeWithCaching_ManySpecialChars()
        => EscapingUtilities.Escape(ManySpecialChars, cache: true);

    // --- ContainsEscapedWildcards ---

    [Benchmark]
    public bool ContainsEscapedWildcards_NoPercent()
        => EscapingUtilities.ContainsEscapedWildcards(NoSpecialChars);

    [Benchmark]
    public bool ContainsEscapedWildcards_HasWildcards()
        => EscapingUtilities.ContainsEscapedWildcards(EscapedWildcards);

    [Benchmark]
    public bool ContainsEscapedWildcards_LongNoWildcards()
        => EscapingUtilities.ContainsEscapedWildcards(LongNoWildcards);

    // --- Round-trip ---

    [Benchmark]
    public string RoundTrip_EscapeThenUnescape()
        => EscapingUtilities.UnescapeAll(EscapingUtilities.Escape(FewSpecialChars));
}
