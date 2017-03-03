# Parameter completion for the dotnet CLI

_dotnet_zsh_complete() 
{
  local dotnetPath=$words[1]

  local completions=("$(./bin/Debug/netcoreapp1.0/osx.10.11-x64/publish/dotnet complete "$words")")

  reply=( "${(ps:\n:)completions}" )
}

compctl -K _dotnet_zsh_complete dotnet