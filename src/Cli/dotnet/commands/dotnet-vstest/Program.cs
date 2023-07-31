// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Tools.VSTest
{
    public class VSTestCommand
    {
        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            // We use also current process id for the correlation id for possible future usage in case we need to know the parent process
            // from the VSTest side.
            string testSessionCorrelationId = $"{Environment.ProcessId}_{Guid.NewGuid()}";

            var args = new List<string>();
            args.AddRange(GetArgs(parseResult));

            if (!FeatureFlag.Instance.IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
            {
                // Add artifacts processing mode and test session id for the artifact post-processing
                args.Add("--artifactsProcessingMode-collect");
                args.Add($"--testSessionCorrelationId:{testSessionCorrelationId}");
            }

            VSTestForwardingApp vsTestforwardingApp = new(args);

            int exitCode = vsTestforwardingApp.Execute();

            // We run post processing also if execution is failed for possible partial successful result to post process.
            exitCode |= TestCommand.RunArtifactPostProcessingIfNeeded(testSessionCorrelationId, parseResult, FeatureFlag.Instance);

            return exitCode;
        }

        private static string[] GetArgs(ParseResult parseResult)
        {
            IEnumerable<string> args = parseResult.GetArguments();

            if (parseResult.HasOption(CommonOptions.TestLoggerOption))
            {
                // System command line might have mutated the options, reformat test logger option so vstest recognizes it
                var loggerValues = parseResult.GetValue(CommonOptions.TestLoggerOption);
                var loggerArgs = loggerValues.Select(loggerValue => $"{CommonOptions.TestLoggerOption.Name}:{loggerValue}");
                args = args.Where(a => !loggerValues.Contains(a) && !CommonOptions.TestLoggerOption.Name.Equals(a) && !CommonOptions.TestLoggerOption.Aliases.Contains(a));
                args = loggerArgs.Concat(args);
            }

            return args.ToArray();
        }
    }
}
