# Manpage update tool

Utility to update dotnet-cli documentation from https://github.com/dotnet/docs.

## Prerequisites

* Unix OS
* `python` in PATH
* `pandoc` installed
* `pandocfilters` package installed
* `unzip`
* `wget` or `curl`

## Usage

```sh
./update-man-pages.sh
```

## Ubuntu example (from scratch)

```sh
apt update
apt install -y jq curl python python-pip wget unzip git < /dev/null
pip install pandocfilters

pandocVersion=$(curl -s https://api.github.com/repos/jgm/pandoc/releases/latest | jq -r ".tag_name")
pandocVersionedName="pandoc-$pandocVersion-1-amd64.deb"
curl -sLO https://github.com/jgm/pandoc/releases/download/$pandocVersion/$pandocVersionedName > /dev/null
dpkg -i $pandocVersionedName
rm $pandocVersionedName*

git clone https://github.com/dotnet/cli
./cli/Documentation/manpages/tool/update-man-pages.sh
```
