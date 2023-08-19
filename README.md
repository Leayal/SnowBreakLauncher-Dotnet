# SnowBreakLauncher-Dotnet
 A custom launcher for [SnowBreak: Containment Zone](https://snowbreak.amazingseasun.com/), written in C# of .NET8.
 

# Notes
 Currently, this is a WIP: The launcher can check for game's update, install game client, update the game client and start the game.
 But the launcher has not self-update function yet.
 
 
# Build from source:
- Requirements:
  - [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (specifically, .NET SDK version `8.0.100-preview.7` or later[^1]):
    - You can install it into your system.
    - **Or** deploy the SDK binaries into `sdk` directory. The directory is at the project's root, if the directory doesn't exist, you can create it. [(Check the batch script for insight)](build-release.bat#L10)
- After installing the SDK, you can compile by running the file `build-release.bat` file (for Windows).
- After the build is completed successfully, all the compiled files are in `release` directory.


### As of writing this, I'm using `Visual Studio 2022` IDE. 
- You do **not** need an IDE to compile this launcher, only the SDK above is enough.
- You can use any IDEs you like as long as it can deal with .NET C#.


# Third-party libraries
- [CsWin32](https://github.com/Microsoft/CsWin32): A source code generator which generates P/Invoke function "easily" (as sometimes you have to deal with pointers and aliased types).
- [Avalonia](https://github.com/AvaloniaUI/Avalonia): A cross-platform UI framework.
- [MessageBox.Avalonia](https://github.com/AvaloniaCommunity/MessageBox.Avalonia): MessageBox for [Avalonia](https://github.com/AvaloniaUI/Avalonia).

[^1]: Please note that as the new SDK preview version may have breaking or backward-incompatible changes which results in compilation failures. In this case, you should use the exact SDK version (which is 8.0.100-preview.7).