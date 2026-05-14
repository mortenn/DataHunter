# DataHunter

DataHunter is a Windows disk usage viewer for finding what is taking up space on your drives.

Point it at a drive or folder and it scans the full folder tree, totals the real size of each folder, and gives you a visual way to drill into the biggest offenders. It is built for the common "my disk is full, where did the space go?" moment.

## What It Does

- Shows fixed drives and folders in a familiar tree.
- Calculates full folder sizes, including everything below each folder.
- Sorts child folders by size so the largest items float to the top.
- Shows subtle row background bars for each folder's share of the current folder.
- Includes a collapsible detail pane with a pie chart of the current folder or selected child.
- Provides shortcuts for common hot spots like your profile, Windows temp, and Program Files.
- Warns before loading very large folder tables, so folders like `WinSxS` stay usable.
- Supports canceling active scans.
- Follows the Windows light/dark theme and uses a modern Windows 11-style UI.

## Download

Download the latest release from the [GitHub releases page](https://github.com/mortenn/DataHunter/releases).

There are two Windows packages:

- `DataHunter-*-win-x64-self-contained.zip`
  Includes the .NET runtime. This is the easiest option if you just want to download and run the app.

- `DataHunter-*-win-x64-framework-dependent.zip`
  Smaller download. Requires the .NET 10 Desktop Runtime to already be installed.

After downloading, extract the zip file and run `DataHunter.exe`.

## Permissions

DataHunter requests the highest available privileges when it starts. This helps it inspect folders that are otherwise hidden from normal user processes. You can still run it without elevation, but Windows may deny access to some folders and those folders may appear incomplete or unavailable.

## Using DataHunter

1. Start the app.
2. Choose a drive or shortcut from the start page.
3. Wait while the scan runs, or drill into folders as results appear.
4. Use the folder table, row bars, and pie chart to identify where space is used.
5. Click a folder or pie slice to navigate deeper.
6. Use Cancel if a scan is no longer useful.

The status bar shows the active scan state, elapsed time, scanned folder counts, current path, and number of active scan workers.

## Building From Source

Requirements:

- Windows
- .NET 10 SDK

Build and test:

```powershell
dotnet restore DataHunter.slnx
dotnet build DataHunter.slnx
dotnet test
```

Run from source:

```powershell
dotnet run --project src\DataHunter\DataHunter.csproj
```

## Formatting

This repository uses CSharpier for C# and XAML Styler for XAML.

```powershell
dotnet tool restore
dotnet csharpier format .
dotnet xstyler --recursive --directory .
```

## License

DataHunter is released under the MIT License. See [LICENSE](LICENSE).
