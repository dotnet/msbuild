// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging
{
    public class BinaryLoggerParameters
    {
        public string binaryLoggerArguments { get; private set; }

        public string binaryLoggerParameters { get; private set; }

        public BinaryLoggerParameters(string[] binaryLoggerArguments, string[] binaryLoggerParameters)
        {
            this.binaryLoggerArguments = GetLastArgumentPart(binaryLoggerArguments);
            this.binaryLoggerParameters = GetLastArgumentPart(binaryLoggerParameters);
        }

        public bool isBinaryLoggerSet { get; set; }

        public string InitProjectFile { get; set; } = string.Empty;


        /// <summary>
        /// Gets the last argument from the provided array.
        /// If the array is empty returns empty string
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private string GetLastArgumentPart(string[] arguments)
        {
            string result = string.Empty;
            if (arguments != null && arguments.Length > 0)
            {
                result = arguments[arguments.Length - 1];
            }
            return result;
        }
    }
}
