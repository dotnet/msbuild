
# Security Metadata

The feature is meant to improve the security of builds executed via MSBuild, by reducing the chances of spilling secrets (and possibly other sensitive data) from otherwise secured or/and inaccessible build environments.

It builds upon the other efforts reducing the cases accidentaly logging secrets - ['not logging unused environemnt variables'](https://github.com/dotnet/msbuild/pull/7484), 'redacting known secret patterns' (internal). Distinction here is that we want to give users option how to configure their build data so that they can indicate what contains secret/sensitive data and shouldn't get output into logs.

The feature is envisioned to be facilitated via global items and/or properties that will be masking logging of specific types of log entries.

Out of scope vision contains categorization of tasks (e.g. 'trustworthy'/'unknown' and 'outputing input data'/'not outputing input data'/'unknown') and passing data marked as sensitive/secrets would not be allowed (would lead to build error) based on specific configurations tunable by user. So e.g. it would not be possible to pass secrets to [`WriteLinesToFile` task](https://learn.microsoft.com/en-us/visualstudio/msbuild/writelinestofile-task).

# Scope

## In scope
 * Following data can be opted-in for redacting:
    * property values
    * item values
    * item metadata values
    * all item metadata
    * any combination of above
    * task OutputItems (? do we want to support this as additional data type? Can be hand in cases like [`ReadLinesFromFile` task](https://learn.microsoft.com/en-us/visualstudio/msbuild/readlinesfromfile-task))
 * Redacting the above select data from all log events (before they are being sent to loggers). Some examples of additional places where the data can propagate and hence attempted to be logged:   
    * task input parameters
    * task OutputItems (? do we want to support this as possible additional 'transformation' of property/item values? Possibly only when output is equal to the sensitive input) 
    * referenced/evaluated environemnt variables
    * input command line
    * properties/items evalution - causing value to be transfered to a new holder (`<MyProp>$(SomeSecret)</MyProp>`)

## Scope to be decided
 * concatentaing property with other values or flattening item values or transforming items and then passing via other property - should such be ignored or redacted as whole, or redacting just the part formed from the sensitive data?
 * values created via [Property functions](https://learn.microsoft.com/en-us/visualstudio/msbuild/property-functions) or [Item functions](https://learn.microsoft.com/en-us/visualstudio/msbuild/item-functions).

## Out of scope
 * spilling sensitive data via other means then logs (e.g. [`WriteLinesToFile` task](https://learn.microsoft.com/en-us/visualstudio/msbuild/writelinestofile-task))
 * passing values to task and explicit being logged there (this might be controversial for built in task - in case any is logging input values). TODO: we might want to consider revision of logging of some common tasks to form a better idea here.
 * passing values to task and there embedding into additional text and passing out
 

# User interaction

There needs to be a way how user specifies which data should be redacted from log. We have several options:

 * [Not recomended] Denoting those via some metadata on a definition of the data to be redacted - this has two main drawbacks - a) For some data types (properties, metadata) we'd need new constructs how to attach additional info (property metadata; item meta-metadata). b) some data can be defined implicitly or dynamicaly
 * Global property - e.g. 
   ```xml
   <DataToRedactFromLogs>Foo;Bar;Baz->SomeMetadata;MyItem->*</DataToRedactFromLogs>
   ```
   single property might look bit cryptic for denoting different data types

 * Global item - e.g. 
   ```xml
   <ItemGroup>
     <DataToRedactFromLogs Include="Foo" Type="Property">
     </DataToRedactFromLogs>
     <DataToRedactFromLogs Include="Bar" Type="Item" RedactValue=True>
     </DataToRedactFromLogs>
     <DataToRedactFromLogs Include="Baz" Type="Item" RedactValue=False Metadata="SomeMetadata">
     </DataToRedactFromLogs>
     <DataToRedactFromLogs Include="MyItem" Type="Item" RedactValue=False Metadata="*">
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


Only the first presented option is definitely not to be used. All the other options might possibly be used (up to a discussions if appropriate and what should be in scope). 

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
 * What to use as a replacement of the data to be redacted? (Randomized hash, fixed token, etc.) 
 * Do we want to allow to supply custom replacement value for injectable redaction functionality? There would need to be very strong compeling reason, as this is easily suspectible to [log forging attack](https://owasp.org/www-community/attacks/Log_Injection)


# Links
 * Nightfall data redaction syntax: https://docs.nightfall.ai/docs/redacting-sensitive-data-in-4-lines-of-code
 * `spark.redaction.regex`: https://people.apache.org/~pwendell/spark-releases/latest/configuration.html
 * Redacting secrets in k8s logs in ops tool `Komodor`: https://docs.komodor.com/Learn/Sensitive-Information-Redaction.html
 * MSBuild opt-in functionality for properties/items/metadata logging disabling: https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs#L1199

