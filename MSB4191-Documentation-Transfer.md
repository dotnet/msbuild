# Documentation for MSB4191 Error - For Transfer to msbuild-api-docs

This document contains the complete documentation content for the MSB4191 error that should be added to the msbuild-api-docs repository. The content is ready to be transferred as-is or adapted to fit the structure of the msbuild-api-docs repository.

---

## Suggested Location in msbuild-api-docs

This content could be added as:
- A new article in the troubleshooting or common errors section
- An addition to existing documentation about MSBuild batching or item metadata
- A new FAQ entry about MSB4191 error

---

## Documentation Content

### Title: Item Metadata in Conditions Outside of Targets (MSB4191)

### Summary

When using `ItemGroup` elements with conditions that reference item metadata (using the `%(ItemType.MetadataName)` syntax), these conditions only work inside `Target` elements, not at the project level (outside of targets). This results in error MSB4191.

### Error Message

```
error MSB4191: The reference to custom metadata "X" at position 1 is not allowed in this condition "'%(Content.X)' == 'abc'".
```

### The Problem

The following code **fails at the project level** with MSB4191:

```xml
<Project>
  <ItemGroup>
    <Content Include="file1.txt">
      <X>abc</X>
    </Content>
    <Content Include="file2.txt">
      <X>def</X>
    </Content>
  </ItemGroup>
  
  <!-- This will FAIL with MSB4191 error -->
  <ItemGroup>
    <FilteredContent Include="@(Content)" Condition="'%(Content.X)' == 'abc'" />
  </ItemGroup>
</Project>
```

The same code **works inside a target**:

```xml
<Project>
  <ItemGroup>
    <Content Include="file1.txt">
      <X>abc</X>
    </Content>
    <Content Include="file2.txt">
      <X>def</X>
    </Content>
  </ItemGroup>
  
  <Target Name="FilterItems">
    <!-- This works inside a target -->
    <ItemGroup>
      <FilteredContent Include="@(Content)" Condition="'%(Content.X)' == 'abc'" />
    </ItemGroup>
    <Message Text="FilteredContent: @(FilteredContent)" />
  </Target>
</Project>
```

### Why This Happens

The `%(ItemType.MetadataName)` syntax implies **batching** - evaluating the condition once for each distinct value of the metadata. Batching only happens inside `Target` elements during target execution, not during project evaluation (which processes everything outside of targets).

Outside of targets, MSBuild evaluates each element as a single entity. The batching infrastructure required to split items into buckets based on metadata values is only available during target execution.

### Solutions

There are three ways to work around this limitation:

#### Solution 1: Move the Item Filtering to a Target

The simplest solution is to move your item filtering logic into a target:

```xml
<Target Name="FilterItems" BeforeTargets="Build">
  <ItemGroup>
    <FilteredContent Include="@(Content)" Condition="'%(Content.X)' == 'abc'" />
  </ItemGroup>
</Target>
```

Use the `BeforeTargets` or `AfterTargets` attribute to control when the filtering happens in your build process.

#### Solution 2: Use `WithMetadataValue` Item Function

For project-level filtering, use the `->WithMetadataValue()` item function instead:

```xml
<ItemGroup>
  <!-- This works at project level -->
  <FilteredContent Include="@(Content->WithMetadataValue('X', 'abc'))" />
</ItemGroup>
```

The `WithMetadataValue` function is specifically designed for filtering items by metadata at the project level without requiring batching.

**Syntax:** `@(ItemType->WithMetadataValue('MetadataName', 'value'))`

#### Solution 3: Use Other Item Functions

MSBuild provides several item functions that can be used at the project level:

```xml
<ItemGroup>
  <!-- Filter using metadata value -->
  <FilteredContent Include="@(Content->WithMetadataValue('X', 'abc'))" />
  
  <!-- Check if metadata exists -->
  <ItemsWithMetadata Include="@(Content->HasMetadata('X'))" />
  
  <!-- Transform based on metadata -->
  <TransformedContent Include="@(Content->'%(X)')" />
</ItemGroup>
```

### Related Topics

- [MSBuild Batching](https://learn.microsoft.com/visualstudio/msbuild/msbuild-batching) - Understanding how batching works in MSBuild
- [Item Functions](https://learn.microsoft.com/visualstudio/msbuild/item-functions) - Complete list of item functions available
- [MSBuild Items](https://learn.microsoft.com/visualstudio/msbuild/msbuild-items) - Overview of items in MSBuild
- [Item Metadata](https://learn.microsoft.com/visualstudio/msbuild/msbuild-items#item-metadata) - Understanding item metadata

### Additional Context

This limitation has existed since the early versions of MSBuild. Outside of targets, each element is evaluated as a single entity, whereas the `%(ItemType.MetadataName)` syntax in conditions requires batching - evaluating for each distinct bucket. The batching infrastructure is only available during target execution.

While there's no fundamental technical reason why batching couldn't work outside of targets (at least on conditions), implementing this would require significant work to ensure performance isn't negatively impacted.

---

## Suggested Keywords/Tags for Searchability

- MSB4191
- Item metadata
- Batching
- ItemGroup condition
- Metadata condition
- Custom metadata not allowed
- WithMetadataValue
- Project evaluation vs target execution

---

## Related GitHub Issues

- Original issue: https://github.com/dotnet/msbuild/issues/3479
- Related discussion: https://github.com/dotnet/msbuild/issues/3520

---

## Notes for Integration

1. **Cross-references**: Update any existing documentation about batching or item metadata to reference this error
2. **Error code reference**: Add MSB4191 to the error codes index/reference if one exists
3. **Examples**: The code examples provided are tested and verified to work as described
4. **Links**: Update internal links (like `../Contributions/MSBuild-overview.md`) to match the structure of msbuild-api-docs
5. **Versioning**: This limitation exists in all versions of MSBuild; no version-specific notes needed
