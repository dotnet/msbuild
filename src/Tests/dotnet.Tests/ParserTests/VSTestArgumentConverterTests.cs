// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.NET.TestFramework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class VSTestArgumentConverterTests
    {
        [Theory]
        [MemberData(nameof(DataSource.GetArguments), MemberType = typeof(DataSource))]
        public void ConvertArgsShouldConvertValidArgsIntoVSTestParsableArgs(string input, string expectedString)
        {
            string[] args = input.Split(' ');
            string[] expectedArgs = expectedString.Split(' ');

            // Act
            List<string> convertedArgs = new VSTestArgumentConverter().Convert(args, out List<string> ignoredArgs);

            convertedArgs.Should().BeEquivalentTo(expectedArgs);
            ignoredArgs.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(DataSource.GetVerbosityArguments), MemberType = typeof(DataSource))]
        public void ConvertArgshouldConvertsVerbosityArgsIntoVSTestParsableArgs(string input, string expectedString)
        {
            string[] args = input.Split(' ');
            string[] expectedArgs = expectedString.Split(' ');

            // Act
            List<string> convertedArgs = new VSTestArgumentConverter().Convert(args, out List<string> ignoredArgs);

            convertedArgs.Should().BeEquivalentTo(expectedArgs);
            ignoredArgs.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(DataSource.GetIgnoredArguments), MemberType = typeof(DataSource))]
        public void ConvertArgsShouldIgnoreKnownArgsWhileConvertingArgsIntoVSTestParsableArgs(string input, string expectedArgString, string expIgnoredArgString)
        {
            string[] args = input.Split(' ');
            string[] expectedArgs = expectedArgString.Split(' ');
            string[] expIgnoredArgs = expIgnoredArgString.Split(' ');

            // Act
            List<string> convertedArgs = new VSTestArgumentConverter().Convert(args, out List<string> ignoredArgs);

            convertedArgs.Should().BeEquivalentTo(expectedArgs);
            ignoredArgs.Select(x => x.ToUpperInvariant()).Should().BeEquivalentTo(expIgnoredArgs.Select(x => x.ToUpperInvariant()));
        }

        [Fact]
        public void ConvertArgsThrowsWhenWeTryToParseInlineSettings()
        {
            string[] args = "sometest.dll -s test.settings -- inlineSetting=1".Split(" ");

            // Act
            new VSTestArgumentConverter().Invoking(i => i.Convert(args, out _))
                .ShouldThrow<ArgumentException>()
                .WithMessage("Inline settings should not be passed to Convert.");
        }

        public static class DataSource
        {
            private static readonly ImmutableArray<string> s_supportedPathKind
                = ImmutableArray.Create(
                    // Dll
                    "SomeProject.dll",
                    // Exe
                    "SomeProject.exe"
                // Do not return <PROJECT> | <SOLUTION> | <DIRECTORY> | <EMPTY> cases because they
                // won't be handled by the VSTestArgumentConverter.
                );

            private static readonly ImmutableArray<(string option, string vstestEquivalent)> s_supportedOptions
                = ImmutableArray.Create(
                    (@"--test-adapter-path c:\adapterpath\temp", @"--testadapterpath:c:\adapterpath\temp"),
                    (@"-a x86", "--platform:x86"),
                    ("--arch x86", "--platform:x86"),
                    ("--blame", "--blame"),
                    ("--blame-crash", "--blame:CollectDump"),
                    ("--blame-crash-dump-type full", "--blame:CollectDump;DumpType=full"),
                    ("--blame-crash-collect-always", "--blame:CollectDump;CollectAlways=true"),
                    ("--blame-hang", "--blame:CollectHangDump"),
                    ("--blame-hang-dump-type full", "--blame:CollectHangDump;DumpType=full"),
                    ("--blame-hang-timeout 10min", "--blame:CollectHangDump;TestTimeout=10min"),
                    ("--collect coverage", "--collect:coverage"),
                    (@"-d c:\temp\log.txt", @"--diag:c:\temp\log.txt"),
                    (@"--diag c:\temp\log.txt", @"--diag:c:\temp\log.txt"),
                    ("-f net451", "--framework:net451"),
                    ("--framework net451", "--framework:net451"),
                    ("--filter accceptance", "--testcasefilter:accceptance"),
                    ("-l trx", "--logger:trx"),
                    ("--logger trx", "--logger:trx"),
                    ("--nologo", "--nologo"),
                    ("--os linux", "--os:linux"),
                    (@"--results-directory c:\temp\", @"--resultsdirectory:c:\temp\"),
                    ("-s test.settings", "--settings:test.settings"),
                    ("--settings test.settings", "--settings:test.settings"),
                    ("-t", "--listtests"),
                    ("--list-tests", "--listtests")
                );

            private static readonly ImmutableDictionary<string, string> s_verbosityLevelMapping
                = new Dictionary<string, string>()
                {
                    ["q"] = "quiet",
                    ["m"] = "minimal",
                    ["n"] = "normal",
                    ["d"] = "detailed",
                    ["diag"] = "diagnostic"
                }.ToImmutableDictionary();

            private static readonly ImmutableArray<string> s_ignoredOptions
                = ImmutableArray.Create(
                    "-c Debug",
                    "--configuration Debug",
                    $"-r {ToolsetInfo.LatestWinRuntimeIdentifier}-x64",
                    $"--runtime {ToolsetInfo.LatestWinRuntimeIdentifier}-x64",
                    @"-o c:\temp2",
                    @"--output c:\temp2",
                    "--no-build",
                    "--no-restore",
                    "--interactive",
                    "--testSessionCorrelationId SomeId",
                    "--artifactsProcessingMode-collect"
                );

            private static readonly ImmutableArray<(string option, string vstestEquivalent)> s_specificOptionCombinations
                = ImmutableArray.Create(
                    // Blame combinations
                    (@"--blame --blame-crash-dump-type full --blame-crash-collect-always", @"--blame:CollectDump;CollectAlways=true;DumpType=full"),
                    (@"--blame-hang-dump-type full", @"--blame:CollectHangDump;DumpType=full"),
                    (@"--blame-hang-timeout 10min", @"--blame:CollectHangDump;TestTimeout=10min"),
                    (@"--blame --blame-hang-dump-type full --blame-hang-timeout 10min", @"--blame:CollectHangDump;DumpType=full;TestTimeout=10min"),
                    (@"--blame --blame-hang-dump-type full --blame-hang-timeout 10min --blame-crash-dump-type mini --blame-crash-collect-always", @"--blame:CollectDump;CollectAlways=true;DumpType=mini;CollectHangDump;DumpType=full;TestTimeout=10min"),
                    // using the legacy --blame syntax when we provide the parameter that are already in vstest.console format still work
                    (@"--blame CollectDump;DumpType=full", @"--blame:CollectDump;DumpType=full"),
                    (@"--blame:CollectDump;DumpType=full", @"--blame:CollectDump;DumpType=full"),
                    // Non-blame combinations
                    (@"-s testsettings -t -f net451 -d log.txt --results-directory c:\temp\", @"--settings:testsettings --listtests --framework:net451 --diag:log.txt --resultsdirectory:c:\temp\"),
                    (@"-s:testsettings -t -f:net451 -d:log.txt --results-directory:c:\temp\", @"--settings:testsettings --listtests --framework:net451 --diag:log.txt --resultsdirectory:c:\temp\"),
                    (@"--settings testsettings -t --test-adapter-path c:\path --framework net451 --diag log.txt --results-directory c:\temp\", @"--settings:testsettings --listtests --testadapterpath:c:\path --framework:net451 --diag:log.txt --resultsdirectory:c:\temp\")
                );

            public static IEnumerable<object[] /* input, expectedString */> GetArguments()
            {
                yield return new[] { @"-h", "--help" };

                // Returns the various combination of path and options
                foreach ((string option, string vstestEquivalent) in s_supportedOptions)
                {
                    foreach (string pathKind in s_supportedPathKind)
                    {
                        string pathKindPrefix = pathKind + " ";
                        string pathKindSuffix = " " + pathKind;
                        yield return new[] { pathKindPrefix + option, pathKindPrefix + vstestEquivalent };
                        yield return new[] { option + pathKindSuffix, vstestEquivalent + pathKindSuffix };

                        // It's also possible to call the option with a colon as separator rather than with space
                        // e.g., "--diag:log.txt" instead of "--diag log.txt"
                        string[] optionAndArg = option.Split(' ');
                        if (optionAndArg.Length == 2)
                        {
                            var optionWithColon = optionAndArg[0] + ":" + optionAndArg[1];
                            yield return new[] { pathKindPrefix + optionWithColon, pathKindPrefix + vstestEquivalent };
                            yield return new[] { optionWithColon + pathKindSuffix, vstestEquivalent + pathKindSuffix };
                        }
                    }
                }

                // Returns specific combinations to test
                foreach ((string option, string vstestEquivalent) in s_specificOptionCombinations)
                {
                    foreach (string pathKind in s_supportedPathKind)
                    {
                        string pathKindPrefix = pathKind + " ";
                        string pathKindSuffix = " " + pathKind;
                        yield return new[] { pathKindPrefix + option, pathKindPrefix + vstestEquivalent };
                        yield return new[] { option + pathKindSuffix, vstestEquivalent + pathKindSuffix };
                    }
                }
            }

            public static IEnumerable<object[] /* input, expectedString */> GetVerbosityArguments()
            {
                foreach ((string levelShort, string levelLong) in s_verbosityLevelMapping)
                {
                    foreach (string pathKind in s_supportedPathKind)
                    {
                        string pathKindPrefix = pathKind + " ";
                        string pathKindSuffix = " " + pathKind;
                        string vstestEquivalent = $"--logger:console;verbosity={levelLong}";

                        foreach (string argument in new[] { "-v", "--verbosity" })
                        {
                            foreach (string separator in new[] { " ", ":" })
                            {
                                yield return new[] { pathKindPrefix + argument + separator + levelShort, pathKindPrefix + vstestEquivalent };
                                yield return new[] { pathKindPrefix + argument + separator + levelLong, pathKindPrefix + vstestEquivalent };
                                yield return new[] { argument + separator + levelShort + pathKindSuffix, vstestEquivalent + pathKindSuffix };
                                yield return new[] { argument + separator + levelLong + pathKindSuffix, vstestEquivalent + pathKindSuffix };
                            }
                        }
                    }
                }
            }

            public static IEnumerable<object[] /* input, expectedString, expectedIgnoredString */> GetIgnoredArguments()
            {
                // Returns the various combination of path and options
                foreach (string ignoredOption in s_ignoredOptions)
                {
                    foreach (string pathKind in s_supportedPathKind)
                    {
                        string pathKindPrefix = pathKind + " ";
                        string pathKindSuffix = " " + pathKind;
                        yield return new[] { pathKindPrefix + ignoredOption, pathKind, ignoredOption };
                        yield return new[] { ignoredOption + pathKindSuffix, pathKind, ignoredOption };

                        // It's also possible to call the option with a colon as separator rather than with space
                        // e.g., "--diag:log.txt" instead of "--diag log.txt"
                        string[] optionAndArg = ignoredOption.Split(' ');
                        if (optionAndArg.Length == 2)
                        {
                            var optionWithColon = optionAndArg[0] + ":" + optionAndArg[1];
                            yield return new[] { pathKindPrefix + optionWithColon, pathKind, optionWithColon };
                            yield return new[] { optionWithColon + pathKindSuffix, pathKind, optionWithColon };
                        }
                    }
                }

                // Returns a combination of args and ignored args
                foreach ((string combinationOption, string vstestEquivalent) in s_specificOptionCombinations)
                {
                    foreach (string pathKind in s_supportedPathKind)
                    {
                        string pathKindPrefix = pathKind + " ";
                        string pathKindSuffix = " " + pathKind;
                        string ignoredOptions = string.Join(" ", s_ignoredOptions);

                        yield return new[] { pathKindPrefix + ignoredOptions + " " + combinationOption, pathKindPrefix + vstestEquivalent, ignoredOptions };
                        yield return new[] { pathKindPrefix + combinationOption + " " + ignoredOptions, pathKindPrefix + vstestEquivalent, ignoredOptions };

                        yield return new[] { ignoredOptions + " " + combinationOption + pathKindSuffix, vstestEquivalent + pathKindSuffix, ignoredOptions };
                        yield return new[] { combinationOption + " " + ignoredOptions + pathKindSuffix, vstestEquivalent + pathKindSuffix, ignoredOptions };

                        yield return new[] { ignoredOptions + " " + pathKindPrefix + combinationOption, pathKindPrefix + vstestEquivalent, ignoredOptions };
                        yield return new[] { combinationOption + " " + pathKindPrefix + ignoredOptions, pathKindPrefix + vstestEquivalent, ignoredOptions };
                    }
                }
            }
        }
    }
}
