// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable SA1205 // Partial elements should declare access
partial class Program
#pragma warning restore SA1205 // Partial elements should declare access
{
    public static int Main(string[] args)
    {
        var testCommandLine = TestCommandLine.HandleCommandLine(args);
        var newArgs = testCommandLine.RemainingArgs.ToList();

        // Help argument needs to be the first one to xunit, so don't insert assembly location in that case
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
            newArgs.AddRange(testCommandLine.GetXunitArgsFromTestConfig());
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
        var log = new StringTestLogger();
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
}
