// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class MessageArgs_Tests
{
    [Fact]
    public void FactoriesCreateExpectedArgs()
    {
        Verify(default(MessageArgs), []);
        Verify(MessageArgs.Create(1), [1]);
        Verify(MessageArgs.Create(1, 2), [1, 2]);
        Verify(MessageArgs.Create(1, 2, 3), [1, 2, 3]);
        Verify(MessageArgs.Create(1, 2, 3, 4), [1, 2, 3, 4]);
        Verify(MessageArgs.Create(1, 2, 3, 4, 5), [1, 2, 3, 4, 5]);
        Verify(MessageArgs.Create(1, 2, 3, 4, 5, 6), [1, 2, 3, 4, 5, 6]);
        Verify(MessageArgs.Create(1, 2, 3, 4, 5, 6, 7), [1, 2, 3, 4, 5, 6, 7]);
        Verify(MessageArgs.Create(1, 2, 3, 4, 5, 6, 7, 8), [1, 2, 3, 4, 5, 6, 7, 8]);
    }

    [Fact]
    public void FormatExceptionPropagates()
        => Should.Throw<FormatException>(() =>
            MessageArgs.Create(42).Format("Value {1}"));

    private static void Verify<TArgs>(TArgs args, object?[] expectedArgs)
        where TArgs : struct, IMessageArgs
    {
        args.Count.ShouldBe(expectedArgs.Length);
        for (int i = 0; i < expectedArgs.Length; i++)
        {
            args[i].ShouldBe(expectedArgs[i]);
        }

        Should.Throw<InternalErrorException>(() => _ = args[-1]);
        Should.Throw<InternalErrorException>(() => _ = args[args.Count]);
        string format = string.Join(" ", Enumerable.Range(0, args.Count).Select(i => $"{{{i}}}"));
        args.Format(format).ShouldBe(string.Join(" ", expectedArgs));
    }
}
