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
    public class BinaryLoggerConfiguration
    {
        public string blArguments { get; set; }

        public string blpArguments { get; set; }

        public bool IsBinaryLoggerSet { get; set; }

        public string InitProjectFile { get; set; } = string.Empty;

        public BinaryLoggerConfiguration(string blArguments, string blpArguments)
        {
            this.blArguments = blArguments;
            this.blpArguments = blpArguments;
        }

        public BinaryLoggerConfiguration(string blArguments)
        {
            this.blArguments = blArguments;
            blpArguments = string.Empty;
        }


        /// <summary>
        /// Generates the stringified representation of current instance
        /// </summary>
        /// <returns></returns>
        public string GetStringifiedParameters()
        {
            var parameters = new StringBuilder();
            parameters.AppendLine($"initProjectFile={InitProjectFile}");
            parameters.AppendLine($"isBinaryLoggerSet={IsBinaryLoggerSet}");
            parameters.AppendLine($"blArguments={blArguments}");
            parameters.AppendLine($"blpArguments={blpArguments}");

            return parameters.ToString();
        }


        /// <summary>
        /// Generates the BinaryLoggerConfiguration instance based on the parameters provided
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static BinaryLoggerConfiguration? GenerateInstanceFromParameters(string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                return null;
            }

            var data = parameters.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            var initProjectFile = data[0].Replace("initProjectFile=","");
            var isBinaryLoggerSet = bool.Parse(data[1].Replace("isBinaryLoggerSet=", ""));
            var blArguments = data[2].Replace("blArguments=", "");
            var blParameters = data[3].Replace("blpArguments=", "");

            return new BinaryLoggerConfiguration(blArguments, blParameters)
            {
                InitProjectFile = initProjectFile,
                IsBinaryLoggerSet = isBinaryLoggerSet
            };
        }
    }
}
