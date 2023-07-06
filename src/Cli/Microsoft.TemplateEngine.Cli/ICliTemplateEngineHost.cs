// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    public interface ICliTemplateEngineHost : ITemplateEngineHost
    {
        /// <summary>
        /// True when output path was specified additionally (i.e. no fallback to default(current directory)).
        /// </summary>
        public bool IsCustomOutputPath { get; }

        /// <summary>
        /// Gets the output path for execution results.
        /// </summary>
        public string OutputPath { get; }
    }
}
