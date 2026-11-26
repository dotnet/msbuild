# MSBuild's property tracking capabilities

MSBuild Property Tracking is a built-in diagnostic feature that tracks property value changes during the build process.
By default, this feature is opted out due to performance considerations.

## Property Tracking Coverage

The implementation tracks properties in the following scenarios:

1. Properties set via command-line arguments (e.g. using `/p:` switches)
2. Properties defined based on environment variables and used by MSBuild
3. Properties set as target outputs
   - Tracks changes when properties are modified by target execution
4. Properties set as task outputs
   - Monitors property modifications resulting from task execution
5. Properties defined in XML during evaluation
   - Provides exact location information for properties defined in project files
   - Includes line and column information from the source XML
   - Reports on property modifications

## Event Types and Message Formatting

The feature implements specialized event handling for four tracking scenarios (plus a 'None' option to disable tracking):

1. `PropertyReassignmentEventArgs`
   - Triggered when a property value is changed
   - `set MsBuildLogPropertyTracking=1`
   - **Note:** Property reassignment tracking is automatically enabled with ChangeWaves.Wave17_10, even when `MsBuildLogPropertyTracking=0`

2. `PropertyInitialValueSetEventArgs`
   - Triggered when a property is first initialized
   - `set MsBuildLogPropertyTracking=2`

3. `EnvironmentVariableRead`
   - Tracks when environment variables are read
   - `set MsBuildLogPropertyTracking=4`

4. `UninitializedPropertyReadEventArgs`
   - Triggered when attempting to read a property that hasn't been initialized
   - `set MsBuildLogPropertyTracking=8`

5. None
   - Disables explicit property tracking (but see note below about PropertyReassignment)
   - `set MsBuildLogPropertyTracking=0`

If you want to enable all these events reporting, enable it by `set MsBuildLogPropertyTracking=15`.

### Special Behavior for PropertyReassignment

Property reassignment tracking has special enabling logic. It is enabled when:
- `PropertyReassignment` flag is explicitly set (value includes `1`), OR
- `MsBuildLogPropertyTracking=0` AND ChangeWaves.Wave17_10 features are enabled

This means that in projects with Wave17_10 enabled, property reassignments will be tracked even when property tracking appears to be disabled.
