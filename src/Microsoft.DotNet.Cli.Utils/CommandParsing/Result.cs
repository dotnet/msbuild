// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.DotNet.Cli.Utils.CommandParsing
{
    internal struct Result<TValue>
    {
        public Result(TValue value, Cursor remainder)
            : this()
        {
            Value = value;
            Remainder = remainder;
        }

        public readonly TValue Value;
        public readonly Cursor Remainder;

        public bool IsEmpty
        {
            get { return Equals(this, default(Result<TValue>)); }
        }

        public static Result<TValue> Empty
        {
            get { return default(Result<TValue>); }
        }

        public Result<TValue2> AsValue<TValue2>(TValue2 value2)
        {
            return new Result<TValue2>(value2, Remainder);
        }
    }
}