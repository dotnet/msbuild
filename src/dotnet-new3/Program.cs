// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using Microsoft.TemplateEngine.Cli;

namespace Dotnet_new3
{
    public static class Program
    {
        public static Task<int> Main(string[] args)
        {
            //setting output encoding is not available on those platforms
            if (!OperatingSystem.IsIOS() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsTvOS())
            {
                //if output is redirected, force encoding to utf-8;
                //otherwise the caller may not decode it correctly
                //see guideline in https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/4236/Character-Encoding-Issues?anchor=stdout
                if (Console.IsOutputRedirected)
                {
                    Console.OutputEncoding = Encoding.UTF8;
                }
            }

            Command newCommand = New3CommandFactory.Create();
            return ParserFactory.CreateParser(newCommand).Parse(args).InvokeAsync();
        }
    }
}
