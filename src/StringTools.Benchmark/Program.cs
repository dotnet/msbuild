// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Running;

#nullable disable

namespace Microsoft.NET.StringTools.Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<SpanBasedStringBuilder_Benchmark>();
        }
    }
}
