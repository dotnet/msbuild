// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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
