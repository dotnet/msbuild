using Xunit;

// Don't let the tests execute concurrently
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]