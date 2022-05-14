// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static BlameArgs;

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
            ["--results-directory"] = "--resultsdirectory",
            ["--arch"] = "--platform"
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
            BlameArgs blame = new BlameArgs();

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
                        else if (blame.IsBlameArg(activeArgument, null))
                        {
                            // do nothing, we process remaining arguments ourselves
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

                        if (blame.IsBlameArg(argValues[0], argValues[1]))
                        {
                            blame.UpdateBlame(argValues[0], argValues[1]);
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
                        if (blame.IsBlameSwitch(arg))
                        {
                            blame.UpdateBlame(arg, null);
                            activeArgument = arg;
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
                    else if (blame.IsBlameArg(activeArgument, arg))
                    {
                        blame.UpdateBlame(activeArgument, arg);
                    }
                    else
                    {
                        newArgList.Add(string.Concat(activeArgument, ":", arg));
                    }

                    activeArgument = null;
                }
                else
                {
                    if (blame.IsBlameArg(arg, null))
                    {
                        blame.UpdateBlame(arg, null);
                    }
                    else
                    {
                        newArgList.Add(arg);
                    }
                }
            }

            if (!string.IsNullOrEmpty(activeArgument))
            {
                if (IgnoredArguments.Contains(activeArgument))
                {
                    ignoredArgs.Add(activeArgument);
                }
                else if( blame.IsBlameArg(activeArgument, null)) {
                    // do nothing, we process remaining arguments ourselves
                }
                else
                {
                    newArgList.Add(activeArgument);
                }
            }

            if (blame.Blame)
            {
                blame.AddCombinedBlameArgs(newArgList);
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

    class BlameArgs
    {
        public bool Blame = false;
        public string LegacyBlame = null;

        public bool CollectCrashDump = false;
        public string CollectCrashDumpType = null;
        public bool CollectCrashDumpAlways = false;

        public bool CollectHangDump = false;
        public string CollectHangDumpType = null;
        public string CollectHangDumpTimeout = null;

        public static string BlameParam = "--blame";
        public static string BlameCrashParam = "--blame-crash";
        public static string BlameCrashDumpTypeParam = "--blame-crash-dump-type";
        public static string BlameCrashCollectAlwaysParam = "--blame-crash-collect-always";
        public static string BlameHangParam = "--blame-hang";
        public static string BlameHangDumpTypeParam = "--blame-hang-dump-type";
        public static string BlameHangTimeoutParam = "--blame-hang-timeout";

        // parameters that expect arguments
        private readonly string[] _blameArgList = new string[]{
            BlameCrashDumpTypeParam,

            BlameHangDumpTypeParam,
            BlameHangTimeoutParam
        };

        // parameters that don't expect any arguments
        private readonly string[] _blameSwitchList = new string[]{
            BlameParam,

            BlameCrashParam,
            BlameCrashCollectAlwaysParam,

            BlameHangParam,
        };


        internal bool IsBlameArg(string parameter, string value)
        {
            return _blameArgList.Any(p => Eq(p, parameter)) || _blameSwitchList.Any(p => Eq(p, parameter));
        }

        private bool IsLegacyBlame(string parameter, string value)
        {
            // when provided --blame <value>, we do not want to process it any further
            // most likely a legacy call, and the param is already in the format that vstest.console expects
            return Eq(BlameParam, parameter) && !string.IsNullOrWhiteSpace(value);
        }

        internal bool IsBlame(string parameter)
        {
            return Eq(BlameParam, parameter);

        }

        internal bool IsBlameSwitch(string parameter)
        {
            return _blameSwitchList.Any(p => Eq(p, parameter));

        }

        internal void UpdateBlame(string parameter, string argument)
        {
            if (IsLegacyBlame(parameter, argument))
            {
                Blame = true;
                LegacyBlame = argument;
            }

            if (Eq(parameter, BlameParam))
            {
                Blame = true;
            }

            // Any blame-crash param implies that we collect crash dump
            if (Eq(parameter, BlameCrashParam))
            {
                Blame = true;
                CollectCrashDump = true;
            }

            if (Eq(parameter, BlameCrashCollectAlwaysParam))
            {
                Blame = true;
                CollectCrashDump = true;
                CollectCrashDumpAlways = true;
            }

            if (Eq(parameter, BlameCrashDumpTypeParam))
            {
                Blame = true;
                CollectCrashDump = true;
                CollectCrashDumpType = argument;
            }

            // Any Blame-hang param implies that we collect hang dump
            if (Eq(parameter, BlameHangParam))
            {
                Blame = true;
                CollectHangDump = true;
            }

            if (Eq(parameter, BlameHangDumpTypeParam))
            {
                Blame = true;
                CollectHangDump = true;
                CollectHangDumpType = argument;
            }

            if (Eq(parameter, BlameHangTimeoutParam))
            {
                Blame = true;
                CollectHangDump = true;
                CollectHangDumpTimeout = argument;
            }
        }

        private bool Eq(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        internal void AddCombinedBlameArgs(List<string> newArgList)
        {
            if (!Blame)
                return;

            if (!string.IsNullOrWhiteSpace(LegacyBlame))
            {
                // when legacy call is detected don't process
                // any more parameters
                newArgList.Add($"--blame:{LegacyBlame}");
                return;
            }

            string crashDumpArgs = null;
            string hangDumpArgs = null;

            if (CollectCrashDump)
            {
                crashDumpArgs = "CollectDump";
                if (CollectCrashDumpAlways)
                {
                    crashDumpArgs += ";CollectAlways=true";
                }

                if (!string.IsNullOrWhiteSpace(CollectCrashDumpType))
                {
                    crashDumpArgs += $";DumpType={CollectCrashDumpType}";
                }
            }

            if (CollectHangDump)
            {
                hangDumpArgs = "CollectHangDump";
                if (!string.IsNullOrWhiteSpace(CollectHangDumpType))
                {
                    hangDumpArgs += $";DumpType={CollectHangDumpType}";
                }

                if (!string.IsNullOrWhiteSpace(CollectHangDumpTimeout))
                {
                    hangDumpArgs += $";TestTimeout={CollectHangDumpTimeout}";
                }
            }

            if (CollectCrashDump || CollectHangDump)
            {
                newArgList.Add($@"--blame:{string.Join(";", crashDumpArgs, hangDumpArgs).Trim(';')}");
            }
            else
            {
                newArgList.Add("--blame");
            }
        }
    }
}
