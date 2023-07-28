// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.BinlogRedactor.Reporting
{
    internal class BinlogRedactorException : Exception
    {
        public BinlogRedactorException(string message, BinlogRedactorErrorCode binlogRedactorErrorCode) : base(message)
        {
            BinlogRedactorErrorCode = binlogRedactorErrorCode;
        }

        public BinlogRedactorException(string message, BinlogRedactorErrorCode binlogRedactorErrorCode, Exception inner) : base(message, inner)
        {
            BinlogRedactorErrorCode = binlogRedactorErrorCode;
        }

        public BinlogRedactorErrorCode BinlogRedactorErrorCode { get; init; }
    }
}
