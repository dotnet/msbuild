## SDK Resolution Algorithm
In 17.3 under ChangeWave 17.4 the sdk resolution algorithm is changed.

### Reason for change
Previously (before ChangeWave 17.4) all SDK resolvers were loaded and then ordered by priority. The resolvers are tried one after one until one of them succeeds. In order to decrease the number of assemblies to be load we change the behavior in 17.3 under ChangeWave 17.4.

### New SDK Resolution Algorithm
Under ChangeWave 17.4 all the resolvers divides into two groups:
- Specific resolvers, i.e. resolvers with specified name pattern
- General resolvers, i.e. resolvers without specified name pattern

The resolving algorithm works in two passes. 
- On the first pass all the specific resolvers that match the given sdk name would be loaded (if needed), ordered by priority and tried one after one. 
- If the sdk is not found, on the second pass all general resolvers would be loaded (if needed), ordered by priority and tried one after one.

By default the resolvers are general. To make all the resolvers from some dll specific, in the corresponding manifest (xml file) one need to specify the `NamePattern` using C# regex format:
```
<SdkResolver>
  <Path>MySdkResolver.dll</Path>
  <NamePattern>MySdk.*</NamePattern>
</SdkResolver>
```