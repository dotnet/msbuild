
# Security Metadata

The feature is meant to improve the security of builds executed via MSBuild, by reducing the chances of spilling secrets (and possibly other sensitive data) from otherwise secured or/and inaccessible build environments.

It builds upon the other efforts reducing the cases accidentaly logging secrets - ['not logging unused environemnt variables'](https://github.com/dotnet/msbuild/pull/7484), 'redacting known secret patterns' (internal, by @michaelcfanning). Distinction here is that we want to give users option how to configure their build scripts and build data so that they can indicate what contains secret/sensitive data and shouldn't get output into logs.

The feature is envisioned to be delivered in multiple interations, while first itearation will be facilitated via global items and/or properties that will be indicating masking logging of specific types of data in log entries (hence no syntactic changes will be imposed for now).

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
    * task OutputItems (This can be handy in cases similar to [`ReadLinesFromFile` task](https://learn.microsoft.com/en-us/visualstudio/msbuild/readlinesfromfile-task))
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
    * Log events emited from tasks (this might be added as extra opt-in option - but would lead to significant build performance degradation).
    * Any other alternative output of tasks (direct writes to file system, network connections etc.)
    * MSBuild xml nodes (elements/attributes) names. (Sensitive data within MSBuild script itself is strongly discouraged)
    * Passing values to task and there embedding into additional text and passing out as output parameter - unless such is explicitly marked as containing sensitive data.
    * Encrypting/securing data in memory during therun of the build.
 

# User interaction

There needs to be a way how user specifies which data should be redacted from logs. We have several options:

 * New data type - this is part of the [North Star vision](#north-star--longer-term-vision), but is out of scope for the initial iteration.
 * [Not recomended] Denoting those via some metadata on a definition of the data to be redacted - this has two main drawbacks - a) For some data types (properties, metadata) we'd need new constructs how to attach additional info (property metadata; item meta-metadata). b) some data can be defined implicitly or dynamicaly
 * Property with global scope - e.g. 
   ```xml
   <DataToRedactFromLogs>Foo;Bar;Baz->SomeMetadata;MyItem->*</DataToRedactFromLogs>
   ```
   single property might look bit cryptic for denoting different data types. On the other hand it might be more efficient in simple redacting scenarios (pointing to a set of regexes; single sustom redactor etc.) and would allow limiting the log events pre-buffering needs.

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
      public ISet<string>? GetPartsToRedact(string value) {/* Logic goes here */}
   }
   ```
   This option has additional security considerations, but allows most versatile secrets redaction.

   The last option can possibly be allowed to be injected via other means, that MSBuild currently uses for injecting pluggable fnctionality (command line argument; environment variable; binary placed in a specific search location)
* A built in redacting plugin - to be opted-in via env var or command line. Plugin will use same extension point as custom plugins - with extended interface allowing to provide redaction values as well:
   ```csharp

   public interface IValueRedactor
   {
      public ISet<Tuple<string, string>>? GetPartsToRedact(string value);
   }
   ```
   This plugin will allow for no-touch redacting of most comon secret patterns by various providers. The default plugin is going to be provided as contribution by 1ES (by @michaelcfanning) and is currently out of scope of this document.


First presented option is not to be used. All the other options will likely be supported.

# Special considerations

* There should be no (or very minimal) performance impact to cases where redacting is not opted-in and/or to cases where there is lower/minimal level of logging. In another words - we should not spend cycles detecting and redacting secrets on log events that are not going to be loged (todo: second case might be more problematic - as loggers can decide their level of logging).
* Order of processing and imports is important here - if we indicate secret metadata in items, the properties are processed first and hence we can miss preanalyzing (or even redacting) some data. Same applies for order of processing of the properties.
* Considering above two facts - we need a opt-in commandline switch or environemnt variable (or combination) to indicate that secrets metadata might be used - in which case we'll need to buffer build/log events before we have processed all the metadata indicating what needs to be redacted. Extra care will need to be given to sending command line args via EventSource ([source](https://github.com/dotnet/msbuild/blob/main/src/MSBuild/XMake.cs#L655))
* There are no global items today - this can be simulated by putting those to directory.props
* Even seemingly innocent tasks with seemingly innocent logging can spill possibly sensitive data (e.g. think the RAR task, logging all the inputs, while those are just reference related info - those can contain paths that might already by itself be sensitive info). Related: [#8493](https://github.com/dotnet/msbuild/issues/8493) 
* `MSBuild` task can pose a boundary for some context passing (e.g. properties/items).
* Properties/items can occure dynamically after the initial processing of the script - e.g. [`CreateProperty task`](https://learn.microsoft.com/en-us/visualstudio/msbuild/createproperty-task). That should not be a problem, but we should keep it in mind (as additional entrypoint of external data into internal data holders).
* Task authors and consumers are posibly different personas with disconected codebases. For this reason we want to support ability to indicate that task input/output is meant to be a secret. A user of the task should follow the contract and denote the data to be mounted to the task appropriately (otherwise a build warning/error will be issued).

# Suggested Implementation

* Need for explicit opt-in - command line switch or environment variable.
* On detection of opt in, all build events for loggers need to be buffered for a deffered dispatch to loggers (similarly as we have ['DeferredBuildMessage'](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/BuildManager/BuildManager.cs#L400) and [`LogDeferredMessages`](https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/BuildManager/BuildManager.cs#L2890)), until the full pass through the build script (including all imports and `.props` and `.targets` files) so that properties initialization and items initialization is fully performed - as only then we know the full extent of requested redacting.
  * In the future version - with first-class citizen type for secrets, we can possibly frontload single pass through the script just for detection of the secret redaction declarations and avoid the buffering and post-process need.
* Buffered events need to be post-processed in respect with the redaction requests, only then dispatched.
* We'll maintain lookup of elements requested for redaction - those explicitly requested by the name of property/item and those identified as sensitive by value or by transfer of value from other sensitive element.
* We'll intercept assigments of value to property ([`TrackPropertyWrite`](https://github.com/dotnet/msbuild/blob/main/src/Build/Evaluation/PropertyTrackingEvaluatorDataWrapper.cs#L223)), item and task parameter
  * If value is assigned to a task parameter and such is indicated by user as sensitive, the holder of the value (the R-value - parameter/item being assigned to the task input) needs to be as well tracked as sensitive, otherwise build waring/error will be issued.
  * If value is assigned to a task parameter and such is not indicated by user as sensitive, but the holder of the value (the R-value - parameter/item being assigned to the task input) is tracked as sensitive (either because it was explicitly denoted by name, or it was later marked by MSBuild due to holding value matching a sensitivity value regex or callback) - a build warning/error will be issued.
  * If value is assigned to property/item from a task output and such is indicated by user as sensitive, the L-value holder of the value (the property/item being assigned to) need to be as well tracked as sensitive, otherwise build waring/error will be issued.
  * If value is being assigned to property or item
    * and such is indicated by user as sensitive, the generated build event needs to be redacted.
    * and such is not indicated by user as sensitive, but the R-value is indicated as sensitive - the data holder (property/item) is marked as holding sensitive data and treated accordingly.
    * and such is not indicated by user as sensitive, the value is passed to sensitivity indicating regex or callback (in case any of those are configured by user) and if matched - the data holder (property/item) is marked as holding sensitive data and treated accordingly.
* No other redacting of log events will be performed. This is not a strong requirement - we can introduce another opt-in level of strict inspection of all log events. The gain is very questionable though, while the performance impact is severe (internal experiments by @michaelcfanning measured by @rokonec indicate 4-times slow-down on mid-size build). Additionally to being perf-expensive, it can possibly get easily confused - e.g.:

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

In case we'd want to redact all occurences of value of `MySecret` from the task result - we might get a lot of false positives and very confusing results.

# Open questions
 * What to use as a replacement of the data to be redacted? (Randomized hash, fixed token, etc.) - *very likely just a static pattern ('******'). The built-in redactor plugin will be allowed to provide custom replacements*
 * Do we want to allow to supply custom replacement value for injectable redaction functionality? There would need to be very strong compeling reason, as this is easily suspectible to [log forging attack](https://owasp.org/www-community/attacks/Log_Injection) - *most likely no.*
 * Balancing performance and accuracy - can we afford to not support arbitrary output of tasks? Otherwise we'd need to process all log events (similar experiments indicate 4 times slowdown of the build of mid-size project (Orchard)). On the other with explicit 'secret metadata' feature users might expect 100% correctness. Should we make this configurable as well (input data only vs all log entries)? Plus this might be suspectible to false positives (see above).


# Links
 * Nightfall data redaction syntax: https://docs.nightfall.ai/docs/redacting-sensitive-data-in-4-lines-of-code
 * `spark.redaction.regex`: https://people.apache.org/~pwendell/spark-releases/latest/configuration.html
 * Redacting secrets in k8s logs in ops tool `Komodor`: https://docs.komodor.com/Learn/Sensitive-Information-Redaction.html
 * MSBuild opt-in functionality for properties/items/metadata logging disabling: https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs#L1199

