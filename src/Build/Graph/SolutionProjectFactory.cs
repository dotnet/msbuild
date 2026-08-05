// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Construction;

#nullable disable

namespace Microsoft.Build.Graph{
    internal delegate SolutionProjectGenerationResult SolutionProjectFactory(SolutionFile solution, IDictionary<string, string> globalProperties);
}