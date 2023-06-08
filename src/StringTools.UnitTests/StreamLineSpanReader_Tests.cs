// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;

using Xunit;

namespace Microsoft.NET.StringTools.Tests;

public class StreamLineSpanReader_Tests
{
    [Fact]
    public void Basics()
    {
        MemoryStream stream = new(Encoding.UTF8.GetBytes("Hello world!\nAnother string over here...\nAnd one last one"));
        StreamLineSpanReader reader = new(stream, Encoding.UTF8, byteBufferSize: 10, charBufferSize: 100);

        Assert.True(reader.TryReadLine(out ReadOnlySpan<char> line));
        Assert.Equal("Hello world!", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("Another string over here...", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("And one last one", line.ToString());

        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void LineExtendsBeyondEndOfCharBuffer()
    {
        MemoryStream stream = new(Encoding.UTF8.GetBytes("12345678901234567890\n12345678901234567890"));
        StreamLineSpanReader reader = new(stream, Encoding.UTF8, byteBufferSize: 10, charBufferSize: 25);

        Assert.True(reader.TryReadLine(out ReadOnlySpan<char> line));
        Assert.Equal("12345678901234567890", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("12345678901234567890", line.ToString());

        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void LineLongerThanCharBuffer()
    {
        MemoryStream stream = new(Encoding.UTF8.GetBytes("12345678901234567890\n12345678901234567890"));
        StreamLineSpanReader reader = new(stream, Encoding.UTF8, byteBufferSize: 10, charBufferSize: 10);

        Assert.True(reader.TryReadLine(out ReadOnlySpan<char> line));
        Assert.Equal("12345678901234567890", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("12345678901234567890", line.ToString());

        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void MixedNewlineCharacters()
    {
        MemoryStream stream = new(Encoding.UTF8.GetBytes("Abra\r\nCadabra\r\r\nBanana\n\nApple!"));
        StreamLineSpanReader reader = new(stream, Encoding.UTF8, byteBufferSize: 10, charBufferSize: 10);

        Assert.True(reader.TryReadLine(out ReadOnlySpan<char> line));
        Assert.Equal("Abra", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("Cadabra", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("Banana", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("Apple!", line.ToString());

        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void NonLatin()
    {
        MemoryStream stream = new(Encoding.UTF8.GetBytes("걱정도 추억과 자랑처럼 아름다운 벌레는 강아지, 너무나 노새, 거외다.\n\n하나 잔디가 불러 이네들은 하나에 당신은 까닭입니다.\n"));
        StreamLineSpanReader reader = new(stream, Encoding.UTF8, byteBufferSize: 10, charBufferSize: 10);

        Assert.True(reader.TryReadLine(out ReadOnlySpan<char> line));
        Assert.Equal("걱정도 추억과 자랑처럼 아름다운 벌레는 강아지, 너무나 노새, 거외다.", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("하나 잔디가 불러 이네들은 하나에 당신은 까닭입니다.", line.ToString());

        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("", line.ToString());

        Assert.False(reader.TryReadLine(out _));
    }
}
