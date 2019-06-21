namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    // Indicates the matched-ness of a parameter value.
    // Choice params are the only type that can be anything except valid.
    internal enum ParameterValueDisposition
    {
        None,   // if the param name is invalid, the value is irrelevant
        Valid,
        Ambiguous,
        Mismatch
    };
}
