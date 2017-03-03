#!/bin/bash
# Parameter completion for the dotnet CLI

_dotnet_bash_complete()
{
  local word=${COMP_WORDS[COMP_CWORD]}
  local dotnetPath=${COMP_WORDS[1]}

  local completions=("$(./bin/Debug/netcoreapp1.0/osx.10.11-x64/publish/dotnet complete --position ${COMP_POINT} "${COMP_LINE}")")

  # https://www.gnu.org/software/bash/manual/html_node/Programmable-Completion-Builtins.html
  COMPREPLY=( $(compgen -W "$completions" -- "$word") )
}

complete -f -F _dotnet_bash_complete dotnet