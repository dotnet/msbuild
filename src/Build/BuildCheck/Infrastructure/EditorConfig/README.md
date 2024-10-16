# EditorConfigParser

Logic of parsing and matching copied from Roslyn implementation.
To track the request on sharing the code: https://github.com/dotnet/roslyn/issues/72324


In current implementation the usage of the editorconfig is internal only and exposed via ConfigurationProvider functionality. 

Configuration divided into two categories: 
- Infra related configuration. IsEnabled, Severity, EvaluationCheckScope
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
Remove non-msbuild-check related key-values (keys not starting with msbuild_check.RULEID)

The implementation differs depending on category: 
 - Infra related config: Merges the configuration retrieved from configuration module with default values (respecting the specified configs in editorconfig) 
 - Custom configuration: Remove all infra related keys from dictionary

Four levels of cache introduced: 
- When retrieving and parsing the editor config -> Parsed results are saved into dictionary: editorconfigPath = ParsedEditorConfig
- When retrieving and merging the editor config data for project -> Parsed and merged results are saved into dictionary: projectFilePath = MargedData of ParsedEditorConfig
- When retrieving Infra related config: ruleId-projectPath = BuildConfigInstance
- CustomConfigurationData: In order to verify that the config data is the same between projects

Usage examples (API)

```
var editorConfigParser = new EditorConfigParser();
editorConfigParser.Parse("path/to/the/file")
```

The snippet above will return all applied key-value Dictionary<string, string> pairs collected from .editorconfig files

Currently EditorConfigParser is used by [ConfigurationProvider](https://github.com/dotnet/msbuild/blob/e0dfb8d1ce5fc1de5153e65ea04c66a6dcac6279/src/Build/BuildCheck/Infrastructure/ConfigurationProvider.cs#L129).

#### Cache lifetime
The lifetime of the cached configuration is defined by the usage of the instance of ConfigurationProvider. The instance of the ConfigurationProvider is created per BuildCheckManager.
Lifecycle of BuildCheckManager could be found [here](https://github.com/dotnet/msbuild/blob/main/documentation/specs/proposed/BuildCheck-Architecture.md#handling-the-distributed-model)


#### Custom configuration data
CustomConfigurationData is propogated to the BuildCheck Check instance by passing the instance of [ConfigurationContext](https://github.com/dotnet/msbuild/blob/393c2fea652873416c8a2028810932a4fa94403f/src/Build/BuildCheck/API/ConfigurationContext.cs#L14)
during the initialization of the [BuildExecutionCheck](https://github.com/dotnet/msbuild/blob/393c2fea652873416c8a2028810932a4fa94403f/src/Build/BuildCheck/API/BuildExecutionCheck.cs#L36).


#### Example of consuming the CustomConfigurationData
The `Initialize` method of BuildCheck Check:
```C#
public override void Initialize(ConfigurationContext configurationContext)
{
    Console.WriteLine(configurationContext.CustomConfigurationData.Count);
    for (int i = 0; i < configurationContext.CustomConfigurationData.Count; i++)
    {
        var customConfigPerRule = configurationContext.CustomConfigurationData[i]; 
        Console.WriteLine(customConfigPerRule.RuleId); 

        if (customConfigPerRule.ConfigurationData is not null) // null when the configuration was not provided from editorconfig
        {
            foreach (var kv in customConfigPerRule.ConfigurationData)
            {
                Console.WriteLine($"{kv.Key}------{kv.Value}");
            }
        }
        else
        {
            Console.WriteLine($"The data is null for index: {i}");
        }
    }
}
```
