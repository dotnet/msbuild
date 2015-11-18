// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Cli.Utils.CommandParsing
{
    internal class Grammar
    {
        protected static Parser<IList<TValue>> Rep1<TValue>(Parser<TValue> parser)
        {
            Parser<IList<TValue>> rep = Rep(parser);
            return pos =>
            {
                var result = rep(pos);
                return result.IsEmpty || !result.Value.Any() ? Result<IList<TValue>>.Empty : result;
            };
        }

        protected static Parser<IList<TValue>> Rep<TValue>(Parser<TValue> parser)
        {
            return pos =>
            {
                var data = new List<TValue>();
                for (; ; )
                {
                    var result = parser(pos);
                    if (result.IsEmpty) break;
                    data.Add(result.Value);
                    pos = result.Remainder;
                }
                return new Result<IList<TValue>>(data, pos);
            };
        }

        protected static Parser<char> Ch()
        {
            return pos => pos.IsEnd ? Result<char>.Empty : pos.Advance(pos.Peek(0), 1);
        }

        private static Parser<bool> IsEnd()
        {
            return pos => pos.IsEnd ? pos.Advance(true, 0) : Result<bool>.Empty;
        }

        protected static Parser<char> Ch(char ch)
        {
            return pos => pos.Peek(0) != ch ? Result<char>.Empty : pos.Advance(ch, 1);
        }
    }
}