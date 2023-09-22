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
- After installing the SDK, you can compile by running the file `build-release.bat` file (for Windows).
- After the build is completed successfully, all the compiled files are in `release` directory.


The source is also taking some .NET8's improvement in, in case you're targeting .NET8. You can install or deploy [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (the same like steps above) as well as [edit the project file (`.csproj` file) in the source](src/SnowBreakLauncher.csproj#L4) to target .NET8 before building.

# Development
As of writing this, I'm using `Visual Studio 2022` IDE with. However, you can use any IDEs you like as long as it can deal with .NET C#.


# Third-party libraries
- [CsWin32](https://github.com/Microsoft/CsWin32): A source code generator which generates P/Invoke function "easily" (as sometimes you have to deal with pointers and aliased types).
- [Avalonia](https://github.com/AvaloniaUI/Avalonia): A cross-platform UI framework.
- [MessageBox.Avalonia](https://github.com/AvaloniaCommunity/MessageBox.Avalonia): MessageBox for [Avalonia](https://github.com/AvaloniaUI/Avalonia).
