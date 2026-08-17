// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

internal sealed class Program
{
    public static int Main(string[] args)
    {
#if RUNTIME_TYPE_NETCORE
        var newArgs = new List<string>(args);
        newArgs.Insert(0, typeof(Program).Assembly.Location);

        int returnCode = Xunit.ConsoleClient.Program.Main(newArgs.ToArray());

        // if (showHelp)
        // {
        //    TestCommandLine.ShowHelp();
        // }

        return returnCode;
#else
        throw new NotImplementedException();
#endif
    }
}
