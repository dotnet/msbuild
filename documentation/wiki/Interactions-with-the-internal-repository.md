MSbuild got forked from an internal Microsoft repository. Although the Github repository is the official one, where development takes place, there are still some left-over connections to the internal one. This page attempts to document these.

Changes to these files need to be migrated back into the internal repo because that's where they are localized:
- [src/XMakeCommandLine/Microsoft.Build.Core.xsd](https://github.com/Microsoft/msbuild/blob/xplat/src/XMakeCommandLine/Microsoft.Build.Core.xsd)
- [src/XMakeCommandLine/Microsoft.Build.CommonTypes.xsd](https://github.com/Microsoft/msbuild/blob/xplat/src/XMakeCommandLine/Microsoft.Build.CommonTypes.xsd)

There should be no changes to the following files. They are shipped from the internal repo. The github ones are stale.
- [all XamlRules](https://github.com/Microsoft/msbuild/tree/xplat/src/XMakeTasks/XamlRules)