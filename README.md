# SnowBreakLauncher-Dotnet
 A custom launcher for [SnowBreak: Containment Zone](https://snowbreak.amazingseasun.com/), written in C# of .NET7 (can target .NET8 to get its improvement).
 

# Notes
 Currently, this is a WIP: The launcher can check for game's update, install game client, update the game client and start the game.
 But the launcher has not self-update function yet.
 
 
# Usage:
### You can download the binaries and use it in [Release page](../../releases/latest)
### Or you can build the whole thing from source:
- Requirements:
  - [.NET 7 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0):
    - You can install it into your system.
    - **Or** deploy the SDK binaries into `sdk` directory. The directory is at the project's root, if the directory doesn't exist, you can create it. [(Check the batch script for insight)](build-release.bat#L10)
- After installing the SDK, you can compile by running one of the build script files:
  - [build-rel-win-standalone.bat](build-rel-win-standalone.bat) file: For 64-bit Windows OSs, the binaries will be portably standalone and requires no dependencies to run. This is recommended if you are to distribute your binaries to other users.
  - [crossbuild-rel-linux-standalone.bat](crossbuild-rel-linux-standalone.bat) file: For 64-bit Windows OSs, the binaries will be portably standalone **which is for running on Linux OSs**. In the nutshell, this is `Building it on Windows, running it on Linux`.
  - [build-rel-linux-standalone.sh](build-rel-linux-standalone.sh) file: For 64-bit Linux OSs, the binaries will be portably standalone and requires no dependencies to run. This is recommended if you are to distribute your binaries to other users.
  - [build-rel-linux-dependent.sh](build-rel-linux-dependent.sh) file: For 64-bit Linux OSs, the binaries will be portable but requiring [.NET7 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) installed on the user's system to run. The .NET7 SDK includes all runtimes so you don't need to install .NET7 Desktop Runtime if you already have the SDK installed.
- After the build is completed successfully, all the compiled files are in `release` directory.


The source is also taking some .NET8's improvement in, in case you're targeting .NET8. You can install or deploy [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (the same like steps above) as well as [edit the project file (`.csproj` file) in the source](src/SnowBreakLauncher.csproj#L4) to target .NET8 before building.

# Development
As of writing this, I'm using `Visual Studio 2022` IDE with. However, you can use any IDEs you like as long as it can deal with .NET C#.


# Third-party libraries
- [CsWin32](https://github.com/Microsoft/CsWin32): A source code generator which generates P/Invoke function "easily" (as sometimes you have to deal with pointers and aliased types).
- [Avalonia](https://github.com/AvaloniaUI/Avalonia): A cross-platform UI framework.
- [MessageBox.Avalonia](https://github.com/AvaloniaCommunity/MessageBox.Avalonia): MessageBox for [Avalonia](https://github.com/AvaloniaUI/Avalonia).
