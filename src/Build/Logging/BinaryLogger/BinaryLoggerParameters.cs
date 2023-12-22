// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging
{
    public class BinaryLoggerParameters
    {
        public string binaryLoggerArguments { get; set; }

        public string binaryLoggerParameters { get; set; }

        public bool IsBinaryLoggerSet { get; set; }

        public string InitProjectFile { get; set; } = string.Empty;

        public BinaryLoggerParameters(string binaryLoggerArguments, string binaryLoggerParameters)
        {
            this.binaryLoggerArguments = binaryLoggerArguments;
            this.binaryLoggerParameters = binaryLoggerParameters;
        }

        public BinaryLoggerParameters(string binaryLoggerArguments)
        {
            this.binaryLoggerArguments = binaryLoggerArguments;
            binaryLoggerParameters = string.Empty;
        }


        /// <summary>
        /// Generates the stringified representation of current instance
        /// </summary>
        /// <returns></returns>
        public string GetStringifiedParameters()
        {
            // tmp
            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                { "initProjectFile", InitProjectFile },
                { "isBinaryLoggerSet", IsBinaryLoggerSet.ToString() },
                { "blArguments", binaryLoggerArguments },
                { "blParameters", binaryLoggerParameters }
            };

            return string.Join(Environment.NewLine, parameters);
        }


        /// <summary>
        /// Generates the BinaryLoggerParameters instance based on the parameters provided
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public BinaryLoggerParameters? GenerateInstanceFromParameters(string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                return null;
            }
            // TODO: parsing logic
            return new BinaryLoggerParameters(string.Empty, string.Empty);
        }
    }
}
