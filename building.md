This project uses [Git Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules) for code shared with other projects.

You can fetch the submodules when you clone:

```
git clone --recurse-submodules https://github.com/saucecontrol/PhotoSauce
```

Or if you have already cloned, fetch the submodules with:

```
git submodule update --init
```

Fetch submodule changes with:

```
git submodule update --remote
```

The projects can then be built as normal with Visual Studio or dotnet CLI.  The NuGet packages are built only under the `Dist` build configuration.

```
dotnet build -c Dist
```
