Plan of attack:

* add multiple outputs
* add entry target detection to projects
* add output type detection to projects
* if EntryTargets contains Build/Publish
    * if output type is exe
        * find path to Exe
* if EntryTargets contains Pack
    * find path to generated nupkg

Once we're collecting multiple kinds of outputs
* render them in nested sublists under each project summary?