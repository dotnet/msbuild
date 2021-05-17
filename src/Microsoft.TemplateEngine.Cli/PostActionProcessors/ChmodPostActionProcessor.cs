// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class ChmodPostActionProcessor : IPostActionProcessor
    {
        private static readonly Guid ActionProcessorId = new Guid("cb9a6cf3-4f5c-4860-b9d2-03a574959774");

        public Guid Id => ActionProcessorId;

        public bool Process(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath)
        {
            bool allSucceeded = true;
            foreach (KeyValuePair<string, string> entry in actionConfig.Args)
            {
                string[] values;
                try
                {
                    JArray valueArray = JArray.Parse(entry.Value);
                    values = new string[valueArray.Count];

                    for (int i = 0; i < valueArray.Count; ++i)
                    {
                        values[i] = valueArray[i].ToString();
                    }
                }
                catch
                {
                    values = new[] { entry.Value };
                }

                foreach (string file in values)
                {
                    Process commandResult = System.Diagnostics.Process.Start(new ProcessStartInfo
                    {
                        RedirectStandardError = false,
                        RedirectStandardOutput = false,
                        UseShellExecute = false,
                        CreateNoWindow = false,
                        WorkingDirectory = outputBasePath,
                        FileName = "/bin/sh",
                        Arguments = $"-c \"chmod {entry.Key} {file}\""
                    });

                    commandResult.WaitForExit();

                    if (commandResult.ExitCode != 0)
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.UnableToSetPermissions, entry.Key, file));
                        allSucceeded = false;
                    }
                }
            }

            return allSucceeded;
        }
    }
}
