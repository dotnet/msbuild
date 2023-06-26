#compdef dotnet

_dotnet_completion() {
  local -a completions=("${(@f)$(dotnet complete "${words}")}")
  compadd -a completions
  _files
}

compdef _dotnet_completion dotnet
