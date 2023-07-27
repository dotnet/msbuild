// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.Cli
{
    /// <summary>
    /// Converts the given arguments to vstest parsable arguments.
    /// </summary>
    /// <remarks>
    /// This converter is only used when running <c>dotnet test</c> with a dll/exe,
    /// not when called for project, solution, directory, or with no path.
    /// </remarks>
    public class VSTestArgumentConverter
    {
        private const string VerbosityString = "--logger:console;verbosity=";

        private static readonly ImmutableDictionary<string, string> s_argumentMapping
            = new Dictionary<string, string>()
            {
                ["-h"] = "--help",
                ["-s"] = "--settings",
                ["-t"] = "--listtests",
                // .NET 7 breaking change, before we had ["-a"] = "--testadapterpath",
                ["-a"] = "--platform",
                ["-l"] = "--logger",
                ["-f"] = "--framework",
                ["-d"] = "--diag",
                ["--filter"] = "--testcasefilter",
                ["--list-tests"] = "--listtests",
                ["--test-adapter-path"] = "--testadapterpath",
                // .NET 7 breaking change, before we had ["-r"] = "--resultsdirectory",
                ["--results-directory"] = "--resultsdirectory",
                ["--arch"] = "--platform"
            }.ToImmutableDictionary();

        private static readonly ImmutableDictionary<string, string> s_verbosityMapping
            = new Dictionary<string, string>()
            {
                ["q"] = "quiet",
                ["m"] = "minimal",
                ["n"] = "normal",
                ["d"] = "detailed",
                ["diag"] = "diagnostic"
            }.ToImmutableDictionary();

        private static readonly ImmutableHashSet<string> s_ignoredArguments
            = ImmutableHashSet.Create(
                StringComparer.OrdinalIgnoreCase,
                "-c",
                "--configuration",
                "-r",
                "--runtime",
                "-o",
                "--output",
                "--no-build",
                "--no-restore",
                "--interactive",
                "--testSessionCorrelationId",
                "--artifactsProcessingMode-collect"
            );

        /// <summary>
        /// The expanded (after applying argument mapping) list of arguments to consider
        /// as switches (i.e., for which we don't expect a non dashed arg). This also
        /// includes ignored arguments.
        /// </summary>
        private static readonly ImmutableHashSet<string> s_switchArguments
            = ImmutableHashSet.Create(
                StringComparer.OrdinalIgnoreCase,
                // Arguments
                "--listtests",
                "--nologo",

                // Ignored arguments
                "--no-build",
                "--no-restore",
                "--interactive",
                "--artifactsProcessingMode-collect"
            );

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
            BlameArgs blame = new();

            foreach (string arg in args)
            {
                if (arg == "--")
                {
                    throw new ArgumentException("Inline settings should not be passed to Convert.");
                }

                if (arg.StartsWith('-'))
                {
                    if (!string.IsNullOrEmpty(activeArgument))
                    {
                        if (s_ignoredArguments.Contains(activeArgument))
                        {
                            ignoredArgs.Add(activeArgument);
                        }
                        else if (BlameArgs.IsBlameArg(activeArgument))
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
                    if (arg.Contains(':'))
                    {
                        string[] argValues = arg.Split(':');

                        if (s_ignoredArguments.Contains(argValues[0]))
                        {
                            ignoredArgs.Add(arg);
                            continue;
                        }

                        if (IsVerbosityArg(argValues[0]))
                        {
                            UpdateVerbosity(argValues[1], newArgList);
                            continue;
                        }

                        if (BlameArgs.IsBlameArg(argValues[0]))
                        {
                            blame.UpdateBlame(argValues[0], argValues[1]);
                            continue;
                        }

                        // Check if the argument is shortname
                        if (s_argumentMapping.TryGetValue(argValues[0].ToLower(), out string longName))
                        {
                            argValues[0] = longName;
                        }

                        newArgList.Add(string.Join(':', argValues));
                    }
                    else
                    {
                        if (BlameArgs.IsBlameSwitch(arg))
                        {
                            blame.UpdateBlame(arg, null);
                            activeArgument = arg;
                        }
                        else
                        {
                            activeArgument = arg.ToLower();
                            if (s_argumentMapping.TryGetValue(activeArgument, out string value))
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
                    // It's possible to have activeArgument that is both ignored and considered as
                    // a switch. Order of the two contains clauses is important because the current
                    // implementation wants to check first if activeArgument is a switch first so
                    // that we don't consume the arg, and the inner if will take care of deciding
                    // if active argument should be ignored or handled.
                    else if (s_switchArguments.Contains(activeArgument))
                    {
                        if (s_ignoredArguments.Contains(activeArgument))
                        {
                            ignoredArgs.Add(activeArgument);
                        }
                        else
                        {
                            newArgList.Add(activeArgument);
                        }

                        // The only real case where we would end-up here is when the item under
                        // test (.dll or .exe) is passed as last argument so we could potentially
                        // check the extension and for invalid ones, either add it to the ignored
                        // args or have a specific failure.
                        newArgList.Add(arg);
                    }
                    // When reaching this point, we are sure that activeArgument is not a switch
                    // so we can add it and the arg to the list of ignored arguments.
                    else if (s_ignoredArguments.Contains(activeArgument))
                    {
                        ignoredArgs.Add(activeArgument);
                        ignoredArgs.Add(arg);
                    }
                    // When entering this condition, we know that the activeArgument is either a
                    // blame switch or a blame argument. Blame args have to be handled differently
                    // because they are cumulative and so need to be combined. The inner logic will
                    // decide how to handle it.
                    else if (BlameArgs.IsBlameArg(activeArgument))
                    {
                        // We know that activeArgument is a blame kind, we now want to see if arg
                        // is linked to the blame or not. If activeArgument is a not blame switch
                        // (e.g., --blame-crash-dump-type full) or if it is the legacy blame syntax
                        // (e.g., --blame CollectDump;DumpType=full) then we do want to update the
                        // blame combination based both on activeArgument and arg. Otherwise, we
                        // have a simple blame switch (e.g., --blame some.dll) so we can add it to
                        // the args list.
                        if (!BlameArgs.IsBlameSwitch(activeArgument)
                            || BlameArgs.IsLegacyBlame(activeArgument, arg))
                        {
                            blame.UpdateBlame(activeArgument, arg);
                        }
                        else
                        {
                            newArgList.Add(arg);
                        }
                    }
                    else
                    {
                        newArgList.Add(string.Concat(activeArgument, ":", arg));
                    }

                    activeArgument = null;
                }
                else
                {
                    if (BlameArgs.IsBlameArg(arg))
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
                if (s_ignoredArguments.Contains(activeArgument))
                {
                    ignoredArgs.Add(activeArgument);
                }
                else if (BlameArgs.IsBlameArg(activeArgument))
                {
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

        private static bool IsVerbosityArg(string arg)
            => string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "--verbosity", StringComparison.OrdinalIgnoreCase);

        private static void UpdateVerbosity(string verbosity, List<string> newArgList)
        {
            if (s_verbosityMapping.TryGetValue(verbosity.ToLower(), out string longValue))
            {
                newArgList.Add(VerbosityString + longValue);
                return;
            }
            newArgList.Add(VerbosityString + verbosity);
        }
    }

    class BlameArgs
    {
        public bool Blame;
        private string _legacyBlame;

        private bool _collectCrashDump;
        private string _collectCrashDumpType;
        private bool _collectCrashDumpAlways;

        private bool _collectHangDump;
        private string _collectHangDumpType;
        private string _collectHangDumpTimeout;

        private const string BlameParam = "--blame";
        private const string BlameCrashParam = "--blame-crash";
        private const string BlameCrashDumpTypeParam = "--blame-crash-dump-type";
        private const string BlameCrashCollectAlwaysParam = "--blame-crash-collect-always";
        private const string BlameHangParam = "--blame-hang";
        private const string BlameHangDumpTypeParam = "--blame-hang-dump-type";
        private const string BlameHangTimeoutParam = "--blame-hang-timeout";

        private const string LegacyBlameCollectDump = "CollectDump";
        private const string LegacyBlameCollectHangDump = "CollectHangDump";

        // parameters that expect arguments
        private static readonly ImmutableArray<string> s_blameArgList
            = ImmutableArray.Create(
                BlameCrashDumpTypeParam,

                BlameHangDumpTypeParam,
                BlameHangTimeoutParam
            );

        // parameters that don't expect any arguments
        private static readonly ImmutableArray<string> s_blameSwitchList
            = ImmutableArray.Create(
                BlameParam,

                BlameCrashParam,
                BlameCrashCollectAlwaysParam,

                BlameHangParam
            );


        internal static bool IsBlameArg(string parameter)
        {
            return s_blameArgList.Any(p => Eq(p, parameter)) || s_blameSwitchList.Any(p => Eq(p, parameter));
        }

        internal static bool IsLegacyBlame(string parameter, string value)
        {
            // when provided --blame <value>, we do not want to process it any further
            // most likely a legacy call, and the param is already in the format that vstest.console expects
            return Eq(BlameParam, parameter)
                && value != null
                && (value.StartsWith(LegacyBlameCollectDump, StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith(LegacyBlameCollectHangDump, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsBlame(string parameter)
        {
            return Eq(BlameParam, parameter);
        }

        internal static bool IsBlameSwitch(string parameter)
        {
            return s_blameSwitchList.Any(p => Eq(p, parameter));
        }

        internal void UpdateBlame(string parameter, string argument)
        {
            if (IsLegacyBlame(parameter, argument))
            {
                Blame = true;
                _legacyBlame = argument;
            }

            if (Eq(parameter, BlameParam))
            {
                Blame = true;
            }

            // Any blame-crash param implies that we collect crash dump
            if (Eq(parameter, BlameCrashParam))
            {
                Blame = true;
                _collectCrashDump = true;
            }

            if (Eq(parameter, BlameCrashCollectAlwaysParam))
            {
                Blame = true;
                _collectCrashDump = true;
                _collectCrashDumpAlways = true;
            }

            if (Eq(parameter, BlameCrashDumpTypeParam))
            {
                Blame = true;
                _collectCrashDump = true;
                _collectCrashDumpType = argument;
            }

            // Any Blame-hang param implies that we collect hang dump
            if (Eq(parameter, BlameHangParam))
            {
                Blame = true;
                _collectHangDump = true;
            }

            if (Eq(parameter, BlameHangDumpTypeParam))
            {
                Blame = true;
                _collectHangDump = true;
                _collectHangDumpType = argument;
            }

            if (Eq(parameter, BlameHangTimeoutParam))
            {
                Blame = true;
                _collectHangDump = true;
                _collectHangDumpTimeout = argument;
            }
        }

        private static bool Eq(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        internal void AddCombinedBlameArgs(List<string> newArgList)
        {
            if (!Blame)
                return;

            if (!string.IsNullOrWhiteSpace(_legacyBlame))
            {
                // when legacy call is detected don't process
                // any more parameters
                newArgList.Add($"--blame:{_legacyBlame}");
                return;
            }

            string crashDumpArgs = null;
            string hangDumpArgs = null;

            if (_collectCrashDump)
            {
                crashDumpArgs = "CollectDump";
                if (_collectCrashDumpAlways)
                {
                    crashDumpArgs += ";CollectAlways=true";
                }

                if (!string.IsNullOrWhiteSpace(_collectCrashDumpType))
                {
                    crashDumpArgs += $";DumpType={_collectCrashDumpType}";
                }
            }

            if (_collectHangDump)
            {
                hangDumpArgs = "CollectHangDump";
                if (!string.IsNullOrWhiteSpace(_collectHangDumpType))
                {
                    hangDumpArgs += $";DumpType={_collectHangDumpType}";
                }

                if (!string.IsNullOrWhiteSpace(_collectHangDumpTimeout))
                {
                    hangDumpArgs += $";TestTimeout={_collectHangDumpTimeout}";
                }
            }

            if (_collectCrashDump || _collectHangDump)
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
