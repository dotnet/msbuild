// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable


namespace Microsoft.DotNet.Cli.Utils
{
    public static class TypoCorrection
    {
        /// <summary>
        /// Gets the list of tokens similar to <paramref name="currentToken"/>
        /// based on priority search:
        /// 1. Starts with
        /// 2. Contains - the call is restricted with <paramref name="currentToken"/> length check, minLength:3 and <param name="maxLevenshteinDistance">
        /// 3. Levenshtein algorithm with <param name="maxLevenshteinDistance"> restriction
        /// max number of suggestion is restricted to 10 entries
        /// </summary>
        /// <param name="possibleTokens">List of tokens to select from.</param>
        /// <param name="currentToken">The token that is being compared.</param>
        /// <param name="maxLevenshteinDistance">the difference between two strings, default: 3.</param>
        /// <returns>The enumerator to tokens similar to <paramref name="currentToken"/>.</returns>
        public static IEnumerable<string> GetSimilarTokens(IEnumerable<string> possibleTokens, string currentToken, int maxLevenshteinDistance = 3)
        {
            var minCurrentTokenLength = 3;
            var maxNumberOfSuggestions = 10;

            var numberOfSuggestions = 0;
            var currentTokenLength = currentToken.Length;
            var possibleSuggestions = possibleTokens.Select((string possibleMatch) => new Suggestion(possibleMatch, possibleMatch.Length)).ToArray();

            var matchByStartsWith = possibleSuggestions
               .Where(s => s.PossibleMatch.StartsWith(currentToken))
               .Select(SetSelection)
               .Take(maxNumberOfSuggestions)
               .OrderBy(s => s.Distance)
               .ToList();

            numberOfSuggestions += matchByStartsWith.Count;
            if (numberOfSuggestions >= maxNumberOfSuggestions)
            {
                return matchByStartsWith.Select(s => s.PossibleMatch);
            }

            var matchByContains = new List<Suggestion>();
            if (currentToken.Length >= minCurrentTokenLength)
            {
                matchByContains = possibleSuggestions
                    .Where(s =>
                        !s.IsSelected
                        && s.PossibleMatch.Contains(currentToken)
                        && s.Distance - currentTokenLength <= maxLevenshteinDistance)
                    .OrderBy(s => s.Distance)
                    .Take(maxNumberOfSuggestions - numberOfSuggestions)
                    .Select(SetSelection)
                    .ToList();

                numberOfSuggestions += matchByContains.Count;
                if (numberOfSuggestions >= maxNumberOfSuggestions)
                {
                    return matchByStartsWith
                        .Concat(matchByContains)
                        .Select(s => s.PossibleMatch);
                }
            }

            var matchByLevenshteinDistance = possibleSuggestions
                .Where(s => !s.IsSelected)
                .Select(s => new Suggestion(s.PossibleMatch, GetDistance(s.PossibleMatch, currentToken)))
                .Where(s => s.Distance <= maxLevenshteinDistance)
                .OrderBy(s => s.Distance)
                .ThenByDescending(s => GetStartsWithDistance(currentToken, s.PossibleMatch))
                .FilterByShortestDistance()
                .Take(maxNumberOfSuggestions - numberOfSuggestions);

            return matchByStartsWith
                .Concat(matchByContains
                .Concat(matchByLevenshteinDistance))
                .Select(s => s.PossibleMatch);
        }

        // The method takes the matches with the shortest distance
        // e.g. (razor, 2), (pazor, 2), (pazors, 3) => (razor, 2), (pazor, 2)
        private static IEnumerable<Suggestion> FilterByShortestDistance(this IEnumerable<Suggestion> possibleMatches)
        {
            int? bestDistance = null;

            return possibleMatches.TakeWhile(s =>
            {
                int distance = s.Distance;
                bestDistance ??= distance;
                return distance == bestDistance;
            });
        }

        // The method finds the distance to the first mismatch between two strings
        // e.g. (cat, cap) => 2
        private static int GetStartsWithDistance(string first, string second)
        {
            int i;
            for (i = 0; i < first.Length && i < second.Length && first[i] == second[i]; i++) ;

            return i;
        }

        //Based on https://blogs.msdn.microsoft.com/toub/2006/05/05/generic-levenshtein-edit-distance-with-c/
        private static int GetDistance(string first, string second)
        {
            // Validate parameters
            if (first is null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second is null)
            {
                throw new ArgumentNullException(nameof(second));
            }

            // Get the length of both.  If either is 0, return
            // the length of the other, since that number of insertions
            // would be required.

            int n = first.Length, m = second.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            // Rather than maintain an entire matrix (which would require O(n*m) space),
            // just store the current row and the next row, each of which has a length m+1,
            // so just O(m) space. Initialize the current row.

            int curRow = 0, nextRow = 1;
            int[][] rows = { new int[m + 1], new int[m + 1] };

            for (int j = 0; j <= m; ++j)
            {
                rows[curRow][j] = j;
            }

            // For each virtual row (since we only have physical storage for two)
            for (int i = 1; i <= n; ++i)
            {
                // Fill in the values in the row
                rows[nextRow][0] = i;
                for (int j = 1; j <= m; ++j)
                {
                    int dist1 = rows[curRow][j] + 1;
                    int dist2 = rows[nextRow][j - 1] + 1;
                    int dist3 = rows[curRow][j - 1] + (first[i - 1].Equals(second[j - 1]) ? 0 : 1);

                    rows[nextRow][j] = Math.Min(dist1, Math.Min(dist2, dist3));
                }

                // Swap the current and next rows
                if (curRow == 0)
                {
                    curRow = 1;
                    nextRow = 0;
                }
                else
                {
                    curRow = 0;
                    nextRow = 1;
                }
            }

            // Return the computed edit distance
            return rows[curRow][m];
        }

        private static Suggestion SetSelection(Suggestion s)
        {
            s.IsSelected = true;

            return s;
        }

        // The class describes properties of a possible match token
        // and based on these the decision for the token selection is made
        internal sealed class Suggestion
        {
            public Suggestion(string possibleMatch, int distance)
            {
                PossibleMatch = possibleMatch;
                Distance = distance;
            }

            public bool IsSelected { get; set; }

            public string PossibleMatch { get; }

            public int Distance { get; set; }
        }
    }
}
