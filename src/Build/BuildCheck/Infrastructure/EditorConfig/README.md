﻿# EditorConfigParser

Logic of parsing and matching copied from Roslyn implementation.
To track the request on sharing the code: https://github.com/dotnet/roslyn/issues/72324


In current implementation the usage of the editorconfig is internal only and exposed via ConfigurationProvider functionality. 

Configuration divided into two categories: 
- Infra related configuration. IsEnabled, Severity, EvaluationAnalysisScope
- Custom configuration, any other config specified by user for this particular rule

### Example 
For the file/folder structure: 
```
├── folder1/
│   └── .editorconfig
│   └── folder2/
        ├── folder3/
        │   └── .editorconfig
        │   └── test.proj
        └── .editorconfig
```

we want to fetch configuration for the project: /full/path/folder1/folder2/folder3/test.proj 

Infra related and custom configuration flows have one common logic: Fetching the configs from editorconfig

```
while(editorConfig is not root && parent directory exists){
        collect, parse editorconfigs 
}

list<editorConfig>{
    folder1/folder2/folder3/.editorconfig
    folder1/folder2/.editorconfig
    folder1/.editorconfig
}
```
Reverse the order and collect all matching section key-value pairs into new dictionary
Remove non-msbuild-analyzer related key-values (keys not starting with msbuild_analyzer.RULEID)

The implementation differs depending on category: 
 - Infra related config: Merges the configuration retrieved from configuration module with default values (respecting the specified configs in editorconfig) 
 - Custom configuration: Remove all infra related keys from dictionary

Two levels of cache introduced: 
- When retrieving and parsing the editor config -> Parsed results are saved into dictionary: editorconfigPath = ParsedEditorConfig
- When retrieving Infra related config: ruleId-projectPath = BuildConfigInstance

Usage examples (API)

```
var editorConfigParser = new EditorConfigParser();
editorConfigParser.Parse("path/to/the/file")
```

The snippet above will return all applied key-value Dictionary<string, string> pairs collected from .editorconfig files

Currently EditorConfigParser is used by [ConfigurationProvider](https://github.com/dotnet/msbuild/blob/e0dfb8d1ce5fc1de5153e65ea04c66a6dcac6279/src/Build/BuildCheck/Infrastructure/ConfigurationProvider.cs#L129). 