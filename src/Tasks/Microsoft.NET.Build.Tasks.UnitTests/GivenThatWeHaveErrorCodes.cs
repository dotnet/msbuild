// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using System.Linq;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenThatWeHaveErrorCodes
    {
        private const int _firstCode = 1001;

        private static readonly IReadOnlyList<int> _deletedCodes = new int[]
        {
            // Put deleted numeric error codes here. 
            // For example, if NETSDK1001 is deleted, add 1001 to this list.
            1066,
        };

        [Fact]
        public void ThereAreNoGapsDuplicatesOrIncorrectlyFormattedCodes()
        {
            var codes = new HashSet<int>(_deletedCodes);

            foreach (var (key, message) in GetMessages())
            {
                // NB: if we ever need strings that don't have error codes (say because they are not sent to MSBuild),
                //     we should use a separate .resx file so that we can preserve this enforcement.
                var match = Regex.Match(message, "^NETSDK([0-9]{4}): ");
                match.Success
                    .Should()
                    .BeTrue(because: $"all messages should have correctly formatted error codes ({key} does not)");

                int code = int.Parse(match.Groups[1].Value);
                codes.Add(code)
                    .Should()
                    .BeTrue(because: $"error codes should not be duplicated (NETSDK{code} is used more than once)");
            }

            for (int i = 0; i < codes.Count; i++)
            {
                int code = _firstCode + i;
                codes.Contains(code)
                     .Should()
                     .BeTrue(because: $"error codes should not be skipped (NETSDK{code} was not found; add to the deleted codes list if intentionally deleted)");
            }
        }

        private static IEnumerable<(string key, string message)> GetMessages()
        {
            var set = Strings.ResourceManager.GetResourceSet(
                CultureInfo.InvariantCulture,
                createIfNotExists: true,
                tryParents: false);

            return set.Cast<DictionaryEntry>().Select(e => ((string)e.Key, (string)e.Value));
        }
    }
}
