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
sudo apt install -y pandoc python python-pip wget unzip < /dev/null
sudo pip install pandocfilters

git clone https://github.com/dotnet/cli
./Documentation/manpage/tool/update-man-pages.sh
```
