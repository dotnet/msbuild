using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

partial class Program
{
    public static int Main(string[] args)
    {
        var testCommandLine = TestCommandLine.HandleCommandLine(args);
        var newArgs = testCommandLine.RemainingArgs.ToList();

        //  Help argument needs to be the first one to xunit, so don't insert assembly location in that case
        if (testCommandLine.ShouldShowHelp)
        {
            newArgs.Insert(0, "-?");
        }
        else
        {
            newArgs.Insert(0, typeof(Program).Assembly.Location);
        }

        if (!testCommandLine.ShouldShowHelp)
        {
            BeforeTestRun(newArgs);
        }

        int returnCode;

        if (testCommandLine.ShowSdkInfo)
        {
            returnCode = ShowSdkInfo();
        }
        else
        {
            returnCode = Xunit.ConsoleClient.Program.Main(newArgs.ToArray());
        }

        if (testCommandLine.ShouldShowHelp)
        {
            TestCommandLine.ShowHelp();
            ShowAdditionalHelp();
        }
        else
        {
            AfterTestRun();
        }

        return returnCode;
    }

    private static int ShowSdkInfo()
    {
        var log = new OutputLogger();
        var command = new DotnetCommand(log, "--info");
        var testDirectory = TestDirectory.Create(Path.Combine(TestContext.Current.TestExecutionDirectory, "sdkinfo"));

        command.WorkingDirectory = testDirectory.Path;

        var result = command.Execute();

        Console.WriteLine(result.StdOut);

        if (result.ExitCode != 0)
        {
            Console.WriteLine(log.ToString());
        }

        return result.ExitCode;
    }

    static partial void BeforeTestRun(List<string> args);
    static partial void AfterTestRun();

    static partial void ShowAdditionalHelp();

    private class OutputLogger : ITestOutputHelper
    {
        StringBuilder _stringBuilder = new StringBuilder();

        public void WriteLine(string message)
        {
            _stringBuilder.AppendLine(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            _stringBuilder.AppendLine(string.Format(format, args));
        }

        public override string ToString()
        {
            return _stringBuilder.ToString();
        }
    }
}
