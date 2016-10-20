# Migration Defaults Constructor

This pulls the migration property and item defaults from a clone of the dotnet/sdk repo.

Run `./run.sh` to generate an sdkdefaults.json

Move it to the Microsoft.DotNet.ProjectJsonMigration project under `src` to override the defaults in dotnet (it's embedded as a resource).