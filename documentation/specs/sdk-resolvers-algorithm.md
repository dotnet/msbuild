## SDK Resolution Algorithm
In 17.3 under ChangeWave 17.4 the sdk resolution algorithm is changed.

### Reason for change
Previously (before ChangeWave 17.4) all SDK resolvers were loaded and then ordered by priority. The resolvers are tried one after one until one of them succeeds. In order to decrease the number of assemblies to be load we change the behavior in 17.3 under ChangeWave 17.4.

### New SDK Resolution Algorithm
Under ChangeWave 17.4 all the resolvers divides into two groups:
- Specific resolvers, i.e. resolvers with specified sdk name pattern `ResolvableSdkPattern`
- General resolvers, i.e. resolvers without specified sdk name pattern `ResolvableSdkPattern`

The resolving algorithm works in two passes. 
- On the first pass all the specific resolvers that match the given sdk name would be loaded, ordered by priority and tried one after one. 
- If the sdk is not found, on the second pass all general resolvers would be loaded, ordered by priority and tried one after one.

By default the resolvers are general. To make all the resolvers from some dll specific, in the corresponding manifest (xml file) one need to specify the `ResolvableSdkPattern` using C# regex format:
```
<SdkResolver>
  <Path>MySdkResolver.dll</Path>
  <ResolvableSdkPattern>MySdk.*</ResolvableSdkPattern>
</SdkResolver>
```

Note, that the manifest file, if exists, from ChangeWave 17.4 would have preference over the dll.
The sdk discovery works according to the following algorithm:
- First try locate the manifest file and use it. 
- If it is not found, we try to locate the dll in the resolver's folder. 
Both xml and dll name should match the following name pattern `...\SdkResolvers\(ResolverName)\(ResolverName).(xml/dll)`.