# Microsoft.DotNet.GenAPI

`Microsoft.DotNet.GenAPI` (with its command line tool `Microsoft.DotNet.GenAPI.Tool` and MSBuild task `Microsoft.DotNet.GenAPI.Task`) generates C# source from an assembly. The generated source contains stub declarations for types and members, where declarations preserve the API contract of the original source.

`Microsoft.DotNet.GenAPI` loads an assembly and any explicitly defined references, and visits all of its symbols recursively. Based on a symbol's accessibility and how it affects the API contract, each visitor will use a `SyntaxGenerator` to reconstruct a `SyntaxNode` for that symbol. Following this phase, various `CSharpSyntaxRewriter` implementations will fix up that syntax tree. For example, they will add dummy method bodies, flatten simple definitions into a single line, remove `object` from base types, add the `partial` keyword, and remove generic type arguments.

```
Usage:
  Microsoft.DotNet.GenAPI.Tool [options]

Options:
  --assembly <assembly> (REQUIRED)                     The path to one or more assemblies or directories with
                                                       assemblies.
  --assembly-reference <assembly-reference>            Paths to assembly references or their underlying directories for
                                                       a specific target framework in the package.
  --exclude-api-file <exclude-api-file>                The path to one or more api exclusion files with types in DocId
                                                       format.
  --exclude-attributes-file <exclude-attributes-file>  The path to one or more attribute exclusion files with types in
                                                       DocId format.
  --output-path <output-path>                          Output path. Default is the console. Can specify an existing
                                                       directory as well
                                                                   and then a file will be created for each assembly
                                                       with the matching name of the assembly.
  --header-file <header-file>                          Specify a file with an alternate header content to prepend to
                                                       output.
  --exception-message <exception-message>              If specified - method bodies should throw
                                                       PlatformNotSupportedException, else `throw null`.
  --include-visible-outside                            Include internal API's. Default is false.
  --include-assembly-attributes                        Includes assembly attributes which are values that provide
                                                       information about an assembly. Default is false.
  --version                                            Show version information
  -?, -h, --help                                       Show help and usage information
```
