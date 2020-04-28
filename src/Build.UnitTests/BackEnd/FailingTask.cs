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
            BuildEngine.GetType().GetProperty("AllowFailureWithoutError").SetValue(BuildEngine, EnableDefaultFailure);
            return false;
        }

        [Required]
        public bool EnableDefaultFailure { get; set; }
    }
}
