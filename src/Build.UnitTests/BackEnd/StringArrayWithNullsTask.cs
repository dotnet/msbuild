// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A task that returns a string array containing null elements.
    /// Used to test the fix for https://github.com/dotnet/msbuild/issues/13174
    /// </summary>
    public class StringArrayWithNullsTask : Task
    {
        [Output]
        public string[] OutputArray { get; set; }

        public override bool Execute()
        {
            // Return an array with nulls - this pattern occurs in real tasks like GenerateGlobalUsings
            OutputArray = new string[] { "first", null, "third", null, "fifth" };
            return true;
        }
    }
}
