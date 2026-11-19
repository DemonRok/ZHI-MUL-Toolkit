# ZHI MUL Toolkit

## Overview

ZHI MUL Toolkit is a Windows desktop application for inspecting and managing Ultima Online MUL maps and DIF patches. It is built with C# and Windows Forms. Map processing is kept in a separate library so the file engine can be tested without the interface.

The initial build provides the application shell, directory selection, map discovery, terrain block reading, release checks, and checksum verification. Map-writing operations remain disabled until their file formats and safe replacement flow are validated.

## Features

- Discover map identifiers from MUL and DIF filenames.
- Read and validate classic terrain map blocks and terrain patch lookup lists.
- Check GitHub Releases for stable application updates when a repository is configured at build time.
- Generate a Windows x64 single-file release and SHA-256 manifest through GitHub Actions.

## Requirements

- Windows 11 x64, or a Windows 10 Enterprise/LTSC version supported by .NET 10.
- Microsoft .NET 10 Desktop Runtime (x64). The application is framework-dependent and does not bundle the runtime.
- Download the runtime from [Microsoft .NET 10 downloads](https://dotnet.microsoft.com/download/dotnet/10.0).

## Download

Download `ZHIMulToolkit.exe` from the repository's GitHub Releases page. Releases include `SHA256SUMS.txt` for checksum verification.

## Usage

1. Select the original MUL directory.
2. Select the modified MUL or DIF directory.
3. Select a discovered map identifier.
4. Choose an operation.
5. Select the output directory.
6. Start processing and review the results.

Map-writing operations are not enabled in the initial build.

## Updates

The application checks for stable GitHub Releases at startup and from the Help menu. The repository is embedded at build time with `-p:GitHubRepository=OWNER/REPOSITORY`. The initial build detects releases; safe download and executable replacement are planned for a later phase.

## Building from Source

Install the .NET 10 SDK.

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Publish the Windows Forms project as a framework-dependent single-file executable:

```powershell
dotnet publish src/ZHIMulToolkit/ZHIMulToolkit.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishTrimmed=false
```

## License

Licensed under the MIT License. Copyright (c) 2026 DemonRokZHI.

See [LICENSE](LICENSE).
