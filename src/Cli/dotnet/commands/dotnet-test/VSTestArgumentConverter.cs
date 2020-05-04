// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Converts the given arguments to vstest parsable arguments
    /// </summary>
    public class VSTestArgumentConverter
    {
        private const string verbosityString = "--logger:console;verbosity=";

        private readonly Dictionary<string, string> ArgumentMapping = new Dictionary<string, string>
        {
            ["-h"] = "--help",
            ["-s"] = "--settings",
            ["-t"] = "--listtests",
            ["-a"] = "--testadapterpath",
            ["-l"] = "--logger",
            ["-f"] = "--framework",
            ["-d"] = "--diag",
            ["--filter"] = "--testcasefilter",
            ["--list-tests"] = "--listtests",
            ["--test-adapter-path"] = "--testadapterpath",
            ["--results-directory"] = "--resultsdirectory"
        };

        private readonly Dictionary<string, string> VerbosityMapping = new Dictionary<string, string>
        {
            ["q"] = "quiet",
            ["m"] = "minimal",
            ["n"] = "normal",
            ["d"] = "detailed",
            ["diag"] = "diagnostic"
        };

        private readonly string[] IgnoredArguments = new string[]
        {
            "-c",
            "--configuration",
            "--runtime",
            "-o",
            "--output",
            "--no-build",
            "--no-restore",
            "--interactive"
        };

        /// <summary>
        /// Converts the given arguments to vstest parsable arguments
        /// </summary>
        /// <param name="args">original arguments</param>
        /// <param name="ignoredArgs">arguments ignored by the converter</param>
        /// <returns>list of args which can be passsed to vstest</returns>
        public List<string> Convert(string[] args, out List<string> ignoredArgs)
        {
            var newArgList = new List<string>();
            ignoredArgs = new List<string>();

            string activeArgument = null;
            foreach (var arg in args)
            {
                if (arg == "--")
                {
                    throw new ArgumentException("Inline settings should not be passed to Convert.");
                }

                if (arg.StartsWith("-"))
                {
                    if (!string.IsNullOrEmpty(activeArgument))
                    {
                        if (IgnoredArguments.Contains(activeArgument))
                        {
                            ignoredArgs.Add(activeArgument);
                        }
                        else
                        {
                            newArgList.Add(activeArgument);
                        }
                        activeArgument = null;
                    }

                    // Check if the arg contains the value separated by colon
                    if (arg.Contains(":"))
                    {
                        var argValues = arg.Split(':');

                        if (IgnoredArguments.Contains(argValues[0]))
                        {
                            ignoredArgs.Add(arg);
                            continue;
                        }

                        if (this.IsVerbosityArg(argValues[0]))
                        {
                            UpdateVerbosity(argValues[1], newArgList);
                            continue;
                        }

                        // Check if the argument is shortname
                        if (ArgumentMapping.TryGetValue(argValues[0].ToLower(), out var longName))
                        {
                            argValues[0] = longName;
                        }

                        newArgList.Add(string.Join(":", argValues));
                    }
                    else
                    {
                        activeArgument = arg.ToLower();
                        if (ArgumentMapping.TryGetValue(activeArgument, out var value))
                        {
                            activeArgument = value;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(activeArgument))
                {
                    if (IsVerbosityArg(activeArgument))
                    {
                        UpdateVerbosity(arg, newArgList);
                    }
                    else if (IgnoredArguments.Contains(activeArgument))
                    {
                        ignoredArgs.Add(activeArgument);
                        ignoredArgs.Add(arg);
                    }
                    else
                    {
                        newArgList.Add(string.Concat(activeArgument, ":", arg));
                    }

                    activeArgument = null;
                }
                else
                {
                    newArgList.Add(arg);
                }
            }

            if (!string.IsNullOrEmpty(activeArgument))
            {
                if (IgnoredArguments.Contains(activeArgument))
                {
                    ignoredArgs.Add(activeArgument);
                }
                else
                {
                    newArgList.Add(activeArgument);
                }
            }

            return newArgList;
        }

        private bool IsVerbosityArg(string arg)
        {
            return string.Equals(arg, "-v", System.StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--verbosity", System.StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateVerbosity(string verbosity, List<string> newArgList)
        {
            if (VerbosityMapping.TryGetValue(verbosity.ToLower(), out string longValue))
            {
                newArgList.Add(verbosityString + longValue);
                return;
            }
            newArgList.Add(verbosityString + verbosity);
        }
    }
}
