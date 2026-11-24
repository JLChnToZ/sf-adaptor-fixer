# SFAdaptorFixer

Windows console application for safely enabling and disabling the Valve 8832CU
VR network adapter.

## Modes

- Run `SFAdaptorFixer.exe` without arguments to open the interactive TUI. It
  provides guided actions for enabling/disabling the adapter, registering or
  unregistering tasks, and exiting.
- `SFAdaptorFixer.exe register`: elevate once, install to
  `%ProgramFiles%\VRNic`, and register or update `VRNicOn` / `VRNicOff`.
- `SFAdaptorFixer.exe unregister`: remove `VRNicOn` / `VRNicOff` while keeping
  the installed executable, logs, and backup files.
- `SFAdaptorFixer.exe on`: reset the adapter, enable it, and retry until a
  6 GHz BSS is visible.
- `SFAdaptorFixer.exe off`: disable the wireless interface and PnP device,
  then verify that it is safe to remove power.
- `SFAdaptorFixer.exe status`: inspect the adapter state without changing it.

The scheduled tasks run the installed executable directly. PowerShell and
desktop batch launchers are not required.

## Localization

The application follows the current Windows UI culture and supports:

- English
- Traditional Chinese
- Simplified Chinese
- Japanese

Chinese script cultures such as `zh-Hant` and `zh-Hans` map to the appropriate
Traditional or Simplified Chinese resource set.

## Build

```powershell
dotnet test .\SFAdaptorFixer.sln -c Release

# Includes the .NET runtime; requires no .NET installation on the host.
dotnet publish .\SFAdaptorFixer.csproj -c Release -r win-x64 --self-contained true

# Smaller build; requires a Windows x64 .NET 10 runtime on the host.
dotnet publish .\SFAdaptorFixer.csproj -c Release -r win-x64 --self-contained false `
  -o .\bin\Release\net10.0-windows\framework-dependent\
```

The self-contained artifact is:

```text
bin\Release\net10.0-windows\win-x64\publish\SFAdaptorFixer.exe
```

The framework-dependent artifact is:

```text
bin\Release\net10.0-windows\framework-dependent\SFAdaptorFixer.exe
```

## Release

Releases are created manually from the **Release** GitHub Actions workflow. Enter
the version code, such as `1.2.3` or `1.2.3-beta.1`, when dispatching the
workflow. The workflow creates the corresponding `v`-prefixed tag and applies
the version to the published binaries' file, assembly, and product metadata.

`register` and `unregister` affect scheduled tasks and require administrator
approval. `on` and `off` can disable network connectivity, so integration
validation must be performed on the intended Windows computer with the Valve
adapter connected.
