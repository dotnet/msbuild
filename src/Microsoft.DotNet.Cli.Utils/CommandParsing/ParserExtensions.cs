// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Cli.Utils.CommandParsing
{
    internal static class ParserExtensions
    {
        public static Parser<Chain<T1, T2>> And<T1, T2>(this Parser<T1> parser1,
            Parser<T2> parser2)
        {
            return pos =>
            {
                var result1 = parser1(pos);
                if (result1.IsEmpty) return Result<Chain<T1, T2>>.Empty;
                var result2 = parser2(result1.Remainder);
                if (result2.IsEmpty) return Result<Chain<T1, T2>>.Empty;
                return result2.AsValue(new Chain<T1, T2>(result1.Value, result2.Value));
            };
        }

        public static Parser<T1> Or<T1>(this Parser<T1> parser1, Parser<T1> parser2)
        {
            return pos =>
            {
                var result1 = parser1(pos);
                if (!result1.IsEmpty) return result1;
                var result2 = parser2(pos);
                if (!result2.IsEmpty) return result2;
                return Result<T1>.Empty;
            };
        }

        public static Parser<T1> Not<T1, T2>(this Parser<T1> parser1, Parser<T2> parser2)
        {
            return pos =>
            {
                var result2 = parser2(pos);
                if (!result2.IsEmpty) return Result<T1>.Empty;
                return parser1(pos);
            };
        }

        public static Parser<T1> Left<T1, T2>(this Parser<Chain<T1, T2>> parser)
        {
            return pos =>
            {
                var result = parser(pos);
                return result.IsEmpty ? Result<T1>.Empty : result.AsValue(result.Value.Left);
            };
        }

        public static Parser<T2> Down<T1, T2>(this Parser<Chain<T1, T2>> parser)
        {
            return pos =>
            {
                var result = parser(pos);
                return result.IsEmpty ? Result<T2>.Empty : result.AsValue(result.Value.Down);
            };
        }

        public static Parser<T2> Build<T1, T2>(this Parser<T1> parser, Func<T1, T2> builder)
        {
            return pos =>
            {
                var result = parser(pos);
                if (result.IsEmpty) return Result<T2>.Empty;
                return result.AsValue(builder(result.Value));
            };
        }

        public static Parser<string> Str(this Parser<IList<char>> parser)
        {
            return parser.Build(x => new string(x.ToArray()));
        }

        public static Parser<string> Str(this Parser<IList<string>> parser)
        {
            return parser.Build(x => String.Concat(x.ToArray()));
        }
    }
}