// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Xunit;

namespace Microsoft.NET.StringTools.Tests;

public class StringPool_Tests
{
    [Fact]
    public void BasicUsage()
    {
        StringPool pool = new();

        string str = "Hello, Hello!";

        ReadOnlySpan<char> span1 = str.AsSpan(0, 5);
        ReadOnlySpan<char> span2 = str.AsSpan(7, 5);

        Assert.Equal("Hello", span1.ToString());
        Assert.Equal("Hello", span2.ToString());

        string result1 = pool.Intern(span1);
        string result2 = pool.Intern(span2);

        Assert.Equal("Hello", result1);
        Assert.Equal("Hello", result2);

        Assert.Same(result1, result2);
    }

    [Fact]
    public void EmptyString()
    {
        StringPool pool = new();

        Assert.Equal("", pool.Intern("hello".AsSpan(0, 0)));
        Assert.Equal("", pool.Intern("hello".AsSpan(1, 0)));
    }

    [Fact]
    public void InternalEquals()
    {
        Assert.True(StringPool.InternalEquals("Help", "ZHelpZ".AsSpan(1, 4)));
        Assert.True(StringPool.InternalEquals("Help", "HelpZ".AsSpan(0, 4)));
        Assert.True(StringPool.InternalEquals("Help", "ZHelp".AsSpan(1, 4)));

        Assert.True(StringPool.InternalEquals("Hello!", "ZHello!Z".AsSpan(1, 6)));
        Assert.True(StringPool.InternalEquals("Hello!!", "ZHello!!Z".AsSpan(1, 7)));
        Assert.True(StringPool.InternalEquals("Hello", "ZHelloZ".AsSpan(1, 5)));

        Assert.False(StringPool.InternalEquals("Hello", "Hello".AsSpan(0, 4)));
        Assert.False(StringPool.InternalEquals("Hello", "HELLO".AsSpan(0, 5)));
        Assert.False(StringPool.InternalEquals("Hello", "ZHell0Z".AsSpan(1, 5)));
        Assert.False(StringPool.InternalEquals("Hello", "ZHel1oZ".AsSpan(1, 5)));

        const string str = "ABCDEFGHIJKLMNOP";

        //// Because our implementation does some loop unrolling, it's good to test a variety of lengths
        //// and starting offsets.

        for (int start = 0; start <= str.Length; start++)
        {
            for (int length = 0; length <= str.Length - start; length++)
            {
                Assert.True(
                    StringPool.InternalEquals(
                        str.Substring(start, length),
                        str.AsSpan(start, length)),
                    $"Different hash codes at start={start}, length={length}");
            }
        }
    }

    [Fact]
    public void InternalGetHashCode()
    {
        Assert.Equal(0, StringPool.InternalGetHashCode(ReadOnlySpan<char>.Empty));

        ReadOnlySpan<char> span = "Hello, Hello!".AsSpan();

        for (int length = 0; length <= 5; length++)
        {
            if (StringPool.InternalGetHashCode(span.Slice(0, length)) !=
                StringPool.InternalGetHashCode(span.Slice(7, length)))
            {
                Assert.True(false, $"Different hash codes at length={length}");
            }
        }

        Assert.NotEqual(
            StringPool.InternalGetHashCode(span.Slice(0, 5)),
            StringPool.InternalGetHashCode(span.Slice(0, 4)));
        Assert.NotEqual(
            StringPool.InternalGetHashCode(span.Slice(0, 5)),
            StringPool.InternalGetHashCode(span.Slice(1, 5)));

        //// Because our implementation does some loop unrolling, it's good to test a variety of lengths
        //// and starting offsets.

        const string str = "ABCDEFGHIJKLMNOP";

        for (int start = 0; start <= str.Length; start++)
        {
            for (int length = 0; length <= str.Length - start; length++)
            {
                if (StringPool.InternalGetHashCode(str.Substring(start, length).AsSpan()) !=
                    StringPool.InternalGetHashCode(str.AsSpan(start, length)))
                {
                    Assert.True(false, $"Different hash codes at start={start}, length={length}");
                }
            }
        }
    }
}
