# MSBuild environment variables

- [MsBuildSkipEagerWildCardEvaluationRegexes](#msbuildskipeagerwildcardevaluationregexes)


### MsBuildSkipEagerWildCardEvaluationRegexes

If specified, overrides the default behavior of glob expansion. 

During glob expansion, if the path with wildcards that is being processed matches one of the regular expressions provided in the [environment variable](#msbuildskipeagerwildcardevaluationregexes), the path is not processed (expanded). 

The value of the envvironment variable is a list of regular expressions, separated by semilcon (;).