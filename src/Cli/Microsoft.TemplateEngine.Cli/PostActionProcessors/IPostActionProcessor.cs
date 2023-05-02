// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    /// <summary>
    /// The interface defining the post action processor supported by dotnet CLI.
    /// </summary>
    public interface IPostActionProcessor : IIdentifiedComponent
    {
        /// <summary>
        /// Processes the post action.
        /// </summary>
        /// <param name="environment">template engine environment settings.</param>
        /// <param name="action">the post action to process as returned by generator.</param>
        /// <param name="creationEffects">the results of the template dry run.</param>
        /// <param name="templateCreationResult">the results of the template instantiation.</param>
        /// <param name="outputBasePath">the output directory the template was instantiated to.</param>
        /// <returns>true if the post action is executed successfully, false otherwise.</returns>
        bool Process(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath);
    }
}
