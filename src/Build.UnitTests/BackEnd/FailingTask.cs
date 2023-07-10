// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class FailingTask : Task
    {
        public override bool Execute()
        {
            if (!AllowFailureWithoutError.Equals("Default"))
            {
                BuildEngine.GetType().GetProperty("AllowFailureWithoutError").SetValue(BuildEngine, AllowFailureWithoutError.Equals("True"));
            }
            return false;
        }

        [Required]
        public string AllowFailureWithoutError { get; set; }
    }
}
