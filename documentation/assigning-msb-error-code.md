# MSBuild error codes

User-facing errors encountered in MSBuild should have an error code in the form of a four-digit number prefixed by `MSB`, for example `MSB3021: Unable to copy file`.

Errors that should not be user-facing (because they're a result of a problem internal to MSBuild like an `InternalErrorException`) do not need an error code.

This code is defined in the `.resx` file that includes the string representation of the error. For example, [MSB3021 is defined as](https://github.com/Microsoft/msbuild/blob/ea30bf10ad0d7ab37ea54ab9d98fe39a5d97bfb0/src/Tasks/Resources/Strings.resx#L234-L237):

```xml
  <data name="Copy.Error">
    <value>MSB3021: Unable to copy file "{0}" to "{1}". {2}</value>
    <comment>{StrBegin="MSB3021: "}</comment>
  </data>
```

This is consumed with a method that extracts the error code from the string and ensures that the appropriate metadata is applied to the error event:

```c#
Log.LogErrorWithCodeFromResources("Copy.Error", SourceFiles[i].ItemSpec, DestinationFolder.ItemSpec, e.Message);
```

MSBuild error codes must be unique (ideally across all versions of MSBuild).

## Error code ranges

MSBuild error codes are divided into ranges referred to as buckets. The initial digit of the code is the coarsest bucket:

* **`MSB1xxx` errors** are problems encountered when handling the MSBuild command line.
* **`MSB2xxx` errors** are problems encountered in the (deprecated) `Microsoft.Build.Conversion` process.
* **`MSB3xxx` errors** are problems encountered in tasks shipped as part of `Microsoft.Build.Tasks.Core.dll`.
* **`MSB4xxx` errors** are problems encountered in the MSBuild engine.
* **`MSB5xxx` errors** are problems encountered in code that is shared between multiple MSBuild assemblies.
* **`MSB6xxx` errors** are problems encountered in `Microsoft.Build.Utilities`.

## Creating a new error code

To create a new error code, first find the `Strings.resx` file for the assembly in which you plan to produce the error.

A comment at the bottom of the `.resx` will have an index of the error codes it contains and possibly a list of retired error codes, for example

```text
The engine message bucket is: MSB4001 - MSB4999

MSB4128 is being used in FileLogger.cs (can't be added here yet as strings are currently frozen)
MSB4129 is used by Shared\XmlUtilities.cs (can't be added here yet as strings are currently frozen)

Next message code should be MSB4259.

Some unused codes which can also be reused (because their messages were deleted, and UE hasn't indexed the codes yet):
    <none>

Retired codes, which have already shipped, but are no longer needed and should not be re-used:
MSB4056
MSB4005
...

Don't forget to update this comment after using a new code.
```

### Finding a code number

You should select the next message code mentioned in the comment, after doing a repo-wide search to make sure it's not already in use. If it is, increment the number in the comment and try again.

The MSB3xxx bucket for Tasks is subdivided into buckets for each individual task. If a bucket is exhausted, allocate another bucket range for that task with the comment `Task: {whatever} overflow` and allocate a new code within that range.

### Adding a new error

After finding a not-in-use-or-retired error number, add a new resource with a meaningful name whose string begins with that error, a colon, and a space. Add this in the resx file in numeric order.

```xml
  <data name="FeatureArea.DescriptiveName">
    <value>MSBxxxx: User-facing description of the error. Use {0} specifiers if you will need to fill in values at runtime.</value>
    <!-- Only if necessary:
    <comment>LOCALIZATION: Notes to translators. Mention the nature of {} blocks, and any key words or phrases that might get mistranslated.</comment>
     -->
  </data>
```

Then use the new resource's name in code when throwing or logging the error.

### Localization

Error _codes_ are never localized, but the text in the error resource will be localized into many languages. After adding a new error resource (as with any resource change), run a full build to generate placeholder localizations in `*.xlf` files. The strings will be translated by a localization team.

This follows the overall repo [localization](wiki/Localization.md) process.
