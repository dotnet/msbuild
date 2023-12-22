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

        public bool IsBinaryLoggerSet { get; set; }

        public string InitProjectFile { get; set; } = string.Empty;


        /// <summary>
        /// Generates the stringified representation of current instance
        /// </summary>
        /// <returns></returns>
        public string GetStringifiedParameters()
        {
            var builtParameters = new StringBuilder();
            // common configuration
            builtParameters.Append("commonConfig=[");
            builtParameters.Append($"InitProjectFile={InitProjectFile};");
            builtParameters.Append($"IsBinaryLoggerSet={IsBinaryLoggerSet};");
            builtParameters.Append(']');

            builtParameters.Append($"blArguments=[binaryLoggerArguments={binaryLoggerArguments}]");
            builtParameters.Append($"blParameters=[binaryLoggerParameters={binaryLoggerParameters}]");

            return builtParameters.ToString();
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
