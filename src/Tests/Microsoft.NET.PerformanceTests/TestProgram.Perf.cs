using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.NET.Perf.Tests;
using Microsoft.NET.TestFramework;

partial class Program
{
    static partial void BeforeTestRun(List<string> args)
    {
        HandlePerfArgs(args);
    }

    public static void HandlePerfArgs(List<string> args)
    {
        List<string> newArgs = new List<string>();
        List<string> perfArgs = new List<string>();
        Stack<string> argStack = new Stack<string>(Enumerable.Reverse(args));

        bool needsOutputDir = true;
        bool specifiedPerfCollect = false;

        while (argStack.Any())
        {
            string arg = argStack.Pop();

            if (arg.StartsWith("--perf:", StringComparison.OrdinalIgnoreCase) && argStack.Any())
            {
                if (arg.Equals("--perf:iterations", StringComparison.OrdinalIgnoreCase))
                {
                    PerfTest.DefaultIterations = int.Parse(argStack.Pop());
                }
                else
                {
                    if (arg.Equals("--perf:outputdir", StringComparison.OrdinalIgnoreCase))
                    {
                        needsOutputDir = false;
                    }
                    else if (arg.Equals("--perf:collect", StringComparison.OrdinalIgnoreCase))
                    {
                        specifiedPerfCollect = true;
                    }

                    perfArgs.Add(arg);
                    perfArgs.Add(argStack.Pop());
                }
            }
            else
            {
                newArgs.Add(arg);
            }
        }

        if (needsOutputDir)
        {
            perfArgs.Add("--perf:outputdir");
            perfArgs.Add(Path.Combine(TestContext.Current.TestExecutionDirectory, "PerfResults"));
        }
        if (!specifiedPerfCollect)
        {
            //  By default, just collect "stopwatch", in order to avoid (large) .etl files from being created
            //  Other collect options include: BranchMispredictions+CacheMisses+InstructionRetired
            perfArgs.Add("--perf:collect");
            perfArgs.Add("stopwatch");
        }

        PerfTest.InitializeHarness(perfArgs.ToArray());

        args.Clear();
        args.AddRange(newArgs);
    }
    static partial void AfterTestRun()
    {
        PerfTest.DisposeHarness();
    }

    static partial void ShowAdditionalHelp()
    {
        Console.WriteLine(@"
Perf test options:
  --perf:iterations       : Number of iterations
  --perf:outputdir        : Output directory for perf results
  --perf:collect <types>  : Type of perf info to collect.  Default is ""stopwatch"".  Other options include:
                            BranchMispredictions+CacheMisses+InstructionRetired
");
    }
}
