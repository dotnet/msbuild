// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Internal
{
    public struct PatternTestResult
    {
        public static readonly PatternTestResult Failed = new PatternTestResult(isSuccessful: false, stem: null);

        public bool IsSuccessful { get; }
        public string Stem { get; }

        private PatternTestResult(bool isSuccessful, string stem)
        {
            IsSuccessful = isSuccessful;
            Stem = stem;
        }

        public static PatternTestResult Success(string stem)
        {
            return new PatternTestResult(isSuccessful: true, stem: stem);
        }
    }
}