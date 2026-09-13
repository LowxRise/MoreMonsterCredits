Install the .NET SDK and the mod's dependencies in an r2modman profile.

The default profile is "Singleplayer". Build and package from this folder with:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1

Use -Profile "Your profile name" to select a different r2modman profile. NuGet must be reachable on the first build to restore the compile-time dependencies.

The project includes its own helper code. Game, loader and dependency DLLs are referenced locally or through compile-time packages; they are not included in this repository.

The ZIP is created locally and ignored by Git. Build.ps1 checks the manifest and icon, then packages only the README, manifest, icon and this mod's DLL. -NoBuild repackages the DLL already in Thunderstore/plugins.
