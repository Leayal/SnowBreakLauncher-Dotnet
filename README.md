# SnowBreakLauncher-Dotnet
 A custom launcher for [SnowBreak: Containment Zone](https://snowbreak.amazingseasun.com/), written in C# of .NET8.
 
# IMPORTANT
 **Currently, the launcher is broken and can't be used due to [the recent patching system changes](https://github.com/Leayal/SnowBreakLauncher-Dotnet/issues/5#issuecomment-2140717282).
 You can't use the launcher until the code is updated to comply with the new patching system.**

# Notes
 - Currently, this is a hobby project: The launcher can check for game's update, install game client, update the game client and start the game. I can only maintain it in free time and I'll prioritize bug-fixing over feature-adding.
 - The launcher has no self-update function. In order to update the launcher, you will need to re-download the launcher by yourself and overwrite the old files.
 - **Regarding playing on Linux OSs**: **`tl;dr: Play at your own risk`**. While this launcher supports downloading and launching the game on Linux-based platforms, the game itself is not designed to support Linux natively. Playing the game via third-party emulations or translation layers \(such as [Wine](https://wiki.winehq.org/Main_Page) or [Proton](https://github.com/ValveSoftware/Proton)\) has risks of banning \(as the emulators/layers may cause false-positive detect from the game's anti-cheat\).
 
 
# Usage:
### You can download the binaries and use it in [Release page](../../releases/latest)
### Or you can build the whole thing from source:
- Requirements:
  - [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0):
    - You can install it into your system.
    - **Or** deploy the SDK binaries into `sdk` directory. The directory is at the project's root, if the directory doesn't exist, you can create it. [(Check the batch script for insight)](build-rel-win-standalone.bat#L10)
- After installing the SDK, you can compile by running one of the build script files (if you're building it for personal use, I recommend building dependent one):
  - [build-rel-win-standalone.bat](build-rel-win-standalone.bat) file: For 64-bit Windows OSs, the binaries will be portably standalone and requires no dependencies to run.
  - [build-rel-win-dependent.bat](build-rel-win-dependent.bat.sh) file: For 64-bit Windows OSs, the binaries will be portable but requiring `.NET Runtime` installed on the user's system to run.
  - [crossbuild-rel-linux-standalone.bat](crossbuild-rel-linux-standalone.bat) file: For 64-bit Windows OSs, the binaries will be portably standalone **which is for running on Linux OSs**. In the nutshell, this is `Building it on Windows, running it on Linux`.
  - [crossbuild-rel-linux-dependant.bat](crossbuild-rel-linux-dependant.bat) file: For 64-bit Windows OSs, the binaries will be portable but requiring `.NET Runtime` installed on the user's system to run.
  - [build-rel-linux-standalone.sh](build-rel-linux-standalone.sh) file: For 64-bit Linux OSs, the binaries will be portably standalone and requires no dependencies to run. This is recommended if you are to distribute your binaries to other users.
  - [build-rel-linux-dependent.sh](build-rel-linux-dependent.sh) file: For 64-bit Linux OSs, the binaries will be portable but requiring `.NET Runtime` installed on the user's system to run.
- After the build is completed successfully, all the compiled binaries can be found in `release` directory.

### Why are there multiple build script files:
- Scripts with `standalone` in the name was meant to build binaries which you can give to others.
- Scripts with `build` was meant to implies the script "should" produce the same kernel-targeting binaries as the OS invoked the script. `Crossbuild` means the opposite, produce different kernel-targeting binaries from the OS invoked the script.
- Scripts with `rel` means building `release` binaries (meant to be distributed). `rel-<OS>` means release to be run on the target OS, for instance, `rel-linux` means binaries are for running on Linux-based OSs.
### The .NET SDK includes all runtimes so you don't need to install .NET Runtime if you already have the SDK installed.

# Development
As of writing this, I'm using `Visual Studio 2022` IDE with. However, you can use any IDEs you like as long as it can deal with .NET C#.


# Third-party libraries
- [CsWin32](https://github.com/Microsoft/CsWin32): A source code generator which generates Windows platform's P/Invoke functions "easily" (as sometimes you have to deal with pointers, aliased types and their haywires).
- [Avalonia](https://github.com/AvaloniaUI/Avalonia): A cross-platform UI framework.
- [MessageBox.Avalonia](https://github.com/AvaloniaCommunity/MessageBox.Avalonia): MessageBox for [Avalonia](https://github.com/AvaloniaUI/Avalonia).
