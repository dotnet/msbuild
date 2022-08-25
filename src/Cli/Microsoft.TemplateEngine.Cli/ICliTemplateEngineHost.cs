// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
