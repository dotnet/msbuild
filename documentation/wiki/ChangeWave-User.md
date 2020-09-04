# MSBuild Change Waves For Users

## What are Change Waves?
Sometimes we want to make a breaking change _and_ give folks a heads up as to what's breaking. So we develop the change and give them an opt-out while letting you know that this will become a standard feature down the line.

## How To Disable A Change Wave
Preferred way:
Manually set `MSBuildChangeWaveVersion` somewhere in your project file like so:
```xml
<MSBuildChangeWaveVersion>17.0</MSBuildChangeWaveVersion>
```
A temporary fix would be to set `MSBuildChangeWaveVersion` as an environment variable. <!-- Rainer mentioned we don't want customers to do this? -->

**Note:** Ensure you correctly follow the format `xx.yy`. eg; 16.8, 17.12, etc.