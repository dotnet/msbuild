// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using System.Text;

namespace Microsoft.NET.StringTools.Benchmark
{
    [MemoryDiagnoser]
    public class SpanBasedStringBuilder_Benchmark
    {
        [Params(1, 2, 4, 8, 16, 256)]
        public int NumSubstrings { get; set; }

        [Params(1, 8, 32, 128, 512)]
        public int SubstringLengths { get; set; }

        private string[] _subStrings;

        private static SpanBasedStringBuilder _pooledSpanBasedStringBuilder = new SpanBasedStringBuilder();
        private static StringBuilder _pooledStringBuilder = new StringBuilder();

        private static int _uniqueStringCounter = 0;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _subStrings = new string[NumSubstrings];
            for (int i = 0; i < _subStrings.Length; i++)
            {
                _subStrings[i] = new string('a', SubstringLengths);
            }
        }

        [Benchmark]
        public void SpanBasedOperations_CacheHit()
        {
            SpanBasedStringBuilder sbsb = _pooledSpanBasedStringBuilder;
            sbsb.Clear();
            foreach (string subString in _subStrings)
            {
                sbsb.Append(subString);
            }
            sbsb.ToString();
        }

        [Benchmark]
        public void RegularOperations_CacheHit()
        {
            StringBuilder sb = _pooledStringBuilder;
            sb.Clear();
            foreach (string subString in _subStrings)
            {
                sb.Append(subString);
            }
            Strings.WeakIntern(sb.ToString());
        }

        [Benchmark]
        public void SpanBasedOperations_CacheMiss()
        {
            SpanBasedStringBuilder sbsb = _pooledSpanBasedStringBuilder;
            sbsb.Clear();
            foreach (string subString in _subStrings)
            {
                sbsb.Append(subString);
            }
            sbsb.Append(_uniqueStringCounter++.ToString("X8"));
            sbsb.ToString();
        }

        [Benchmark]
        public void RegularOperations_CacheMiss()
        {
            StringBuilder sb = _pooledStringBuilder;
            sb.Clear();
            foreach (string subString in _subStrings)
            {
                sb.Append(subString);
            }
            sb.Append(_uniqueStringCounter++.ToString("X8"));
            Strings.WeakIntern(sb.ToString());
        }
    }
}
