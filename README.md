# GPhoto

Copyright (c) 2025 AkaKSR

> Simple Windows Forms tool for managing images and optional payload files, with FTP upload support and single-file publish scripts.

**Project**
- **Name:** `GPhoto`
- **Framework:** .NET 10 (TargetFramework: `net10.0-windows`)
- **UI:** Windows Forms (WinForms)

**What it does**
- Manage a list of images in a `DataGridView` with columns such as `No.`, `이미지 파일명`, payload flags and generated payload filename.
- Adds a left-most selection checkbox column (`colSelect`) with a header checkbox that toggles all rows.
- Create and upload generated payload files to an FTP server using settings stored via the built-in `UploadSettingsForm` (`config.dat`, DPAPI encrypted).
- Shows a modal `UploadProgressForm` with per-file and overall progress and supports canceling uploads.

**Important file/controls**
- `MainForm.Designer.cs`: UI layout and `dataGridViewFiles` column definitions (contains `colSelect`, `colNo`, `colFileName`, `colPayload`, `colPayloadName`, `colGeneratedPayloadName`, `colUploaded`, `colDescription`).
- `MainForm.cs`: UI logic, header checkbox behavior, upload flow and FTP interactions.
- `UploadSettingsForm.cs`: UI to configure and save FTP host/port/user/password (saved to `config.dat` using DPAPI).
- `UploadProgressForm.cs`: Upload progress dialog.

Build & Publish
-----------------
Two helper scripts are included to publish a framework-dependent single-file Release build (single EXE without bundling the .NET runtime).

- Bash (for Git Bash / WSL / bash.exe): `build_singlefile_fd.sh`
- PowerShell: `build_singlefile_fd.ps1`

Default usage (Win x64):
```bash
./build_singlefile_fd.sh
```
Or explicitly specify the RID:
```bash
RID=win-x86 ./build_singlefile_fd.sh
```
PowerShell:
```powershell
.\build_singlefile_fd.ps1
.\build_singlefile_fd.ps1 -RID win-x86
```

What the scripts do
- Run `dotnet publish` in `Release` configuration.
- Use `-p:PublishSingleFile=true` and `-p:SelfContained=false` so the produced EXE is a single file but depends on an installed .NET runtime on the host.
- Force `OutputType=WinExe` so the built executable runs as a Windows GUI application (no console window when launched from Explorer).

Run the produced EXE
--------------------
- The published EXE is under `publish/<RID>/singlefile-fd/` (e.g. `publish/win-x64/singlefile-fd/GPhoto.exe`).
- Double-clicking the EXE in Explorer launches the GUI without an attached console window.
- If you launch the EXE from an existing `cmd`/PowerShell session, that shell remains visible. To start detached (no console), use:
```powershell
Start-Process -FilePath 'C:\path\to\publish\win-x64\singlefile-fd\GPhoto.exe'
```
or from `cmd`:
```cmd
start "" "C:\path\to\publish\win-x64\singlefile-fd\GPhoto.exe"
```

Configuration / Settings
------------------------
- FTP settings are saved by the `UploadSettingsForm` into `config.dat` in the application directory. The file stores JSON that is encrypted using Windows DPAPI (current user) — the same format is read by the main app.
- If you need to migrate settings, use `UploadSettingsForm` to export or re-enter credentials.

Notes & Troubleshooting
------------------------
- If the published EXE still shows a console when started by double-click, verify the project `OutputType` in `GPhoto.csproj` is set to `WinExe` (the included scripts override this during publish).
- The project currently uses the legacy `FtpWebRequest` APIs for FTP. For robust FTP/FTPS support, connection retries, and better error handling consider migrating to a maintained library such as `FluentFTP`.
- When publishing trimmed (`PublishTrimmed=true`) be careful: reflection-based code (resource loading or certain third-party libraries) may be trimmed out and cause runtime failures.

Development tips
----------------
- To build and run locally in Debug (without publishing):
```bash
dotnet build
dotnet run --project ./GPhoto.csproj
```
- To publish a self-contained single-file (include runtime) change the script flags to `-p:SelfContained=true` and specify `-p:RuntimeIdentifier=<rid>`.

License
-------
This project is licensed under the MIT License. See the `LICENSE` file for details.
