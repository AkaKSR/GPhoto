param(
    [string]$RID = "win-x64"
)

# Publish framework-dependent single-file Release build (no runtime bundled)
# Usage:
#   .\build_singlefile_fd.ps1            # uses default RID win-x64
#   .\build_singlefile_fd.ps1 -RID win-x86

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$OutDir = Join-Path $ScriptDir "publish\$RID\singlefile-fd"

Write-Host "Publishing framework-dependent single-file (RID=$RID) to: $OutDir"

dotnet publish $ScriptDir `
  -c Release `
  -r $RID `
  -p:PublishSingleFile=true `
  -p:SelfContained=false `
  -p:PublishTrimmed=false `
  -p:OutputType=WinExe `
  -o $OutDir

Write-Host "Publish complete. Listing output directory:"
Get-ChildItem -Force $OutDir | Format-List

Write-Host "Done. To run without showing a console window, launch the EXE from Explorer or use Start-Process to detach from an open console."
