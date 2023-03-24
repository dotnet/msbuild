
# Security Metadata

The feature is meant to improve the security of builds executed via MSBuild, by reducing the chances of spilling secrets (and possibly other sensitive data) from otherwise secured or/and inaccessible build environments.

It builds upon the other efforts reducing the cases accidentaly logging secrets - ['not logging unused environemnt variables'](https://github.com/dotnet/msbuild/pull/7484), 'redacting known secret patterns' (internal). Distinction here is that we want to give users option how to configure their build data so that they can indicate what contains secret/sensitive data and shouldn't get output into logs.

The feature is envisioned to be delivered in multiple interations, while first itearation will be facilitated via global items and/or properties that will be indicating masking logging of specific types of data in log entries.

# North Star / Longer-term vision

We envision MSBuild to have a first-class-citisen type system for it's data and tasks. 'Secret' would be one of the data types - allowable to be passed only to other variables or task inputs denoted as 'secret' (so e.g. it would not be possible to pass secrets to [`WriteLinesToFile` task](https://learn.microsoft.com/en-us/visualstudio/msbuild/writelinestofile-task)) and vice versa 'secret' task input or data type could be initialized/evaluated only from other 'secrets' or predefined external sources of data - environment variables, commandline arguments, files, apropriately denoted task output parameters.

Such a strong typing would allow to hold to stronger guarantees of not spilling properly denoted sensitive data and redact them with minimal impact on build performance (as opposed to intermediate attempts that will need to perform string inspections).

**Ilustrative sample:**

```xml
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Secrets>
    <!-- initialize from command line -->
    <GH_token />
    <!-- initialize from env -->
    <ACR_login>$(ACR_login)</ACR_login>
    <!-- initialize by task -->
    <ACR_password />
	</Secrets>

  <UsingTask TaskName="ReadCreadentialFromValut" AssemblyFile="$(MSBuildProjectDirectory)/Tasks/ACR-Tasks.dll" />
  <UsingTask TaskName="PushImageToACR" AssemblyFile="$(MSBuildProjectDirectory)/Tasks/ACR-Tasks.dll" />
	
  <Target Name='PushImage'> 
    <Message Text="Pushin image to ACR" />
    <ReadCreadentialFromValut 
      Key="$(ACR_password_key)"
    >
      <Output TaskParameter="Value" PropertyName="ACR_password"/>
    </ReadCreadentialFromValut>
    <PushImageToACR 
      Login="$(ACR_login)"
      Password="$(ACR_password)"
    />
  </Target>
</Project>
```

```cs
ReadCreadentialFromValut : Task
{
  /// <summary>
  /// Key to be fetched
  /// </summary>
  public string Key { get; set; }

  /// <summary>
  /// Fetched value
  /// </summary>
  [Output]
  [Secret]
  public string Value { get; set; }

  // ... Execute() ...
}
```

```cs
PushImageToACR : Task
{
  /// <summary>
  /// Azure Container Registry Login
  /// </summary>
  public Secret Login { get; set; }

  /// <summary>
  /// Azure Container Registry Password
  /// </summary>
  public Secret Password { get; set; }

  // ... Execute() ...
}
```

An opt-out mechanism would allow usage of properly denoted tasks with plain string input data (and vice versa) - to allow smoother gradual onboarding to the new type system, without the need to rework the entire build script suite at one shot.


# Scope of initial iteration

## In scope
 * Following data can be opted-in for redacting:
    * property values
    * item values
    * item metadata values
    * all item metadata
    * any combination of above
    * task input parameters (to denote that task is requiring sensitive data and only such can be passed in)
    * task OutputItems (? do we want to support this as additional data type? Can be handy in cases like [`ReadLinesFromFile` task](https://learn.microsoft.com/en-us/visualstudio/msbuild/readlinesfromfile-task))
 * Redacting the above will happen in all log events before they are being sent to subscribed loggers. 
 * Redacting will apply to data initializations and passing:
    * task input parameters
    * task OutputItems
    * transfering value to other properties/items via evaluation, transforms, flattening, [Property functions](https://learn.microsoft.com/en-us/visualstudio/msbuild/property-functions), [Item functions](https://learn.microsoft.com/en-us/visualstudio/msbuild/item-functions)
    * initialization from environemnt variables or command line
 * Redacting **will NOT** occure on:
    * log events emited from tasks (this might be added as extra opt-in option - but would lead to significant build performance degradation).
    * any other alternative output of tasks (direct writes to file system, network connections etc.)

## Out of scope
  * Redacting **will NOT** occure on:
    * log events emited from tasks (this might be added as extra opt-in option - but would lead to significant build performance degradation).
    * any other alternative output of tasks (direct writes to file system, network connections etc.)
    * passing values to task and there embedding into additional text and passing out as output parameter - unless such is explicitly marked as containing sensitive data
 

# User interaction

There needs to be a way how user specifies which data should be redacted from logs. We have several options:

 * New data type - this is part of the [North Star vision](#north-star--longer-term-vision), but is out of scope for the initial iteration.
 * [Not recomended] Denoting those via some metadata on a definition of the data to be redacted - this has two main drawbacks - a) For some data types (properties, metadata) we'd need new constructs how to attach additional info (property metadata; item meta-metadata). b) some data can be defined implicitly or dynamicaly
 * Property with global scope - e.g. 
   ```xml
   <DataToRedactFromLogs>Foo;Bar;Baz->SomeMetadata;MyItem->*</DataToRedactFromLogs>
   ```
   single property might look bit cryptic for denoting different data types

 * Item with global scope - e.g. 
   ```xml
   <ItemGroup>
     <!-- Redacting property content based on the name of the property (or environment variable) -->
     <DataToRedactFromLogs Include="Foo" Type="Property">
     </DataToRedactFromLogs>
     <!-- Redacting item content based on the name of the item. Metadat are not redacted -->
     <DataToRedactFromLogs Include="Bar" Type="Item" RedactValue=True>
     </DataToRedactFromLogs>
     <!-- Redacting item metadata content based on the name. -->
     <DataToRedactFromLogs Include="Baz" Type="Item" RedactValue=False Metadata="SomeMetadata">
     </DataToRedactFromLogs>
     <!-- Redacting all metadata content of specific item based on the name of the item. -->
     <DataToRedactFromLogs Include="MyItem" Type="Item" RedactValue=False Metadata="*" />
     <!-- Redacting property content passed from the task. At the same time requiring that the data receiving the output of the task are denoted as secret as well. -->
     <DataToRedactFromLogs Include="OutputA" Type="TaskOutput" TaskName="TaskX" />
     <!-- Redacting task parameter value. At the same time requiring that the data passed to the parameter of the task are denoted as secret as well. -->
     <DataToRedactFromLogs Include="ParamA" Type="TaskParameter" TaskName="TaskX" />
     </DataToRedactFromLogs>
   </ItemGroup>
   ```
   This can offer a more chatty, but better understandable (and possibly beter script generatable) way of denoting the redacting intent.
 * A regex on *value* to redact above discused data types based on their content - e.g.:
    ```xml
   <ItemGroup>
     <!-- Redact GH tokens based on https://github.blog/changelog/2021-03-31-authentication-token-format-updates-are-generally-available -->
     <DataToRedactFromLogs Include="ghp_[A-Za-z0-9_]" Type="ValueRegex">
     </DataToRedactFromLogs>
   </ItemGroup>
   ```
   This way we can give build architects a tool to define common `.props` files opting-in for redacting specific types strings known to be tokens/secrets/sensitive data, without the need to guess under which properties or items they would show within the build
* A custom plugin flagging values for redaction. e.g.:
    ```xml
   <ItemGroup>
     <DataToRedactFromLogs Include="MySecretsClassifier.dll,Contoso.Secrets.Classifier.ClassifySecrets" Type="ValueClassifierPlugin">
     </DataToRedactFromLogs>
   </ItemGroup>
   ```

   where:

   ```csharp
   Contoso.Secrets;

   public class Classifier: IValueClassifier
   {
      public bool NeedsRedaction(string value) {/* Logic goes here */}
   }
   ```
   This option has additional security considerations, but allows most versatile secrets redaction.

   The last option can possibly be allowed to be injected via other means, that MSBuild currently uses for injecting pluggable fnctionality (command line argument; environment variable; binary placed in a specific search location)


First two presented option are not to be used. All the other options will likely be supported.

# Special considerations

* There should be no (or very minimal) performance impact to cases where redacting is not opted-in and/or to cases where there is lower/minimal level of logging. In another words - we should not spend cycles detecting and redacting secrets on log events that are not going to be loged (todo: second case might be more problematic - as loggers can decide their level of logging).
* Order of processing and imports is important here - if we indicate secret metadata in items, the properties are processed first and hence we can miss preanalyzing (or even redacting) some data. Same applies for order of processing of the properties.
* Considering above two facts - we need a opt-in commandline switch or environemnt variable (or combination) to indicate that secrets metadata might be used - in which case we'll need to buffer build/log events before we have processed all the metadata indicating what needs to be redacted.
* There are no global items today - this can be simulated by putting those to directory.props
* Even seemingly innocent tasks with seemingly innocent logging can spill possibly sensitive data (e.g. think the RAR task, logging all the inputs, while those are just reference related info - those can contain paths that might already by itself be sensitive info). Related: [#8493](https://github.com/dotnet/msbuild/issues/8493) 
* `MSBuild` task can pose a boundary for some context passing (e.g. properties/items).
* Task authors and consumers are posibly different personas with disconected codebases. For this reason we want to support ability to indicate that task input/output is meant to be a secret. A user of the task should follow the contract and denote the data to be mounted to the task appropriately (otherwise a build warning/error will be issued).

# Suggested Implementation

[TBD]
* For dynamic functionality we need to scan the data only on it's creation - so need to identify all ways of creating properties, items, metadata (including dynamic creation - e.g. [`CreateProperty task`](https://learn.microsoft.com/en-us/visualstudio/msbuild/createproperty-task))
* We need to have a global dictionary per data type, that will be used prior passing identified messages to loggers. For this reason we might choose not to support flattened/transformed/concatenated values (would this be acceptable?)
* Attempt to scan textual data for presence of the flagged values (e.g. to attempt to handle cases where data is passed into task and there possibly appended with other data and passed as output, or intercepting all log events produced by a task consuming a sensitive value) might get perf expensive and as well can get confused easily - e.g.:

```xml
<ItemGroup>
  <DataToRedactFromLogs>MySecret</DataToRedactFromLogs>
  <MySecret>a</MySecret>
  <MyInnocentData>hahaha</MyInnocentData>
  <SomeProp></SomeProp>
</ItemGroup>

<Target Name="Test">
  <MyTask FirstInput="MySecret" SecondInput="MyInnocentData">
    <Output PropertyName="SomeProp" TaskParameter="Result">
  </MyTask>
  <!-- Might log: 
       Result from task: h<redacted>h<redacted>h<redacted>
  -->
  <Message Text="Result from task: $(SomeProp)">
</Target>
```

Should we redact all occurences of value of `MySecret` from the task result? We might get a lot of false positives and very confusing results.

# Open questions
 * What to use as a replacement of the data to be redacted? (Randomized hash, fixed token, etc.) - *very likely just a static pattern ('******').*
 * Do we want to allow to supply custom replacement value for injectable redaction functionality? There would need to be very strong compeling reason, as this is easily suspectible to [log forging attack](https://owasp.org/www-community/attacks/Log_Injection) - *most likely no.*
 * Balancing performance and accuracy - can we afford to not support arbitrary output of tasks? Otherwise we'd need to process all log events (similar experiments indicate 4 times slowdown of the build of mid-size project (Orchard)). On the other with explicit 'secret metadata' feature users might expect 100% correctness. Should we make this configurable as well (input data only vs all log entries)? Plus this might be suspectible to false positives (see above).


# Links
 * Nightfall data redaction syntax: https://docs.nightfall.ai/docs/redacting-sensitive-data-in-4-lines-of-code
 * `spark.redaction.regex`: https://people.apache.org/~pwendell/spark-releases/latest/configuration.html
 * Redacting secrets in k8s logs in ops tool `Komodor`: https://docs.komodor.com/Learn/Sensitive-Information-Redaction.html
 * MSBuild opt-in functionality for properties/items/metadata logging disabling: https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs#L1199

