// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental;

internal sealed class BuildCopCentralContext
{
    private EvaluatedPropertiesAction? _evaluatedPropertiesActions;
    private ParsedItemsAction? _parsedItemsActions;

    // This we can potentially use to subscribe for receiving evaluated props in the
    //  build event args. However - this needs to be done early on, when analyzers might not be known yet
    internal bool HasEvaluatedPropertiesActions => _evaluatedPropertiesActions != null;
    internal bool HasParsedItemsActions => _parsedItemsActions != null;

    internal void RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction evaluatedPropertiesAction)
    {
        // Here we might want to communicate to node that props need to be sent.
        //  (it was being communicated via MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION)
        _evaluatedPropertiesActions += evaluatedPropertiesAction;
    }

    internal void RegisterParsedItemsAction(ParsedItemsAction parsedItemsAction)
    {
        _parsedItemsActions += parsedItemsAction;
    }

    internal void RunEvaluatedPropertiesActions(EvaluatedPropertiesContext evaluatedPropertiesContext)
    {
        _evaluatedPropertiesActions?.Invoke(evaluatedPropertiesContext);
    }

    internal void RunParsedItemsActions(ParsedItemsContext parsedItemsContext)
    {
        _parsedItemsActions?.Invoke(parsedItemsContext);
    }
}
