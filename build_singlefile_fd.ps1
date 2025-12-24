param(
  [string]$RID = "all"
)

# Publish framework-dependent single-file Release build (no runtime bundled)
# Usage examples:
#   .\build_singlefile_fd.ps1                # uses default RID win-x64
#   .\build_singlefile_fd.ps1 -RID win-x86
#   .\build_singlefile_fd.ps1 -RID x86       # maps to win-x86
#   .\build_singlefile_fd.ps1 -RID all       # builds win-x64, win-x86, win-arm64

function Map-Rid([string]$r) {
  switch ($r.ToLowerInvariant()) {
    'x64'   { 'win-x64' ; break }
    'x86'   { 'win-x86' ; break }
    'arm64' { 'win-arm64' ; break }
    'all'   { 'win-x64','win-x86','win-arm64' ; break }
    default { $r ; break }
  }
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

# Build list of RIDs to process (support comma-separated lists)
$rids = @()
if ($RID -like '*,*') {
  $parts = $RID -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
  foreach ($p in $parts) {
    $mapped = Map-Rid $p
    $rids += $mapped
  }
}
else {
  $mapped = Map-Rid $RID
  $rids += $mapped
}

foreach ($rid in $rids) {
  $OutDir = Join-Path $ScriptDir "publish\$rid\singlefile-fd"
  Write-Host "Publishing framework-dependent single-file (RID=$rid) to: $OutDir"

  dotnet publish $ScriptDir `
    -c Release `
    -r $rid `
    -p:PublishSingleFile=true `
    -p:SelfContained=false `
    -p:PublishTrimmed=false `
    -p:OutputType=WinExe `
    -o $OutDir

  Write-Host "Publish complete for $rid. Listing output directory:"
  if (Test-Path $OutDir) { Get-ChildItem -Force $OutDir | Format-List } else { Write-Host "Output directory not found: $OutDir" }
  # Rename produced EXE to include RID suffix (e.g. GPhoto_win-x64.exe)
  $exe = Get-ChildItem -Path $OutDir -Filter *.exe -File -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($exe) {
    $origName = $exe.Name
    $base = [System.IO.Path]::GetFileNameWithoutExtension($origName)
    $newName = "{0}_{1}.exe" -f $base, $rid
    $newPath = Join-Path $OutDir $newName
    try {
      Rename-Item -Path $exe.FullName -NewName $newName -Force
      Write-Host "Renamed: $origName -> $newName"
    }
    catch {
      Write-Host "Failed to rename EXE: $_"
    }

    # If a matching PDB exists, rename it too
    $pdbPath = Join-Path $OutDir ($base + ".pdb")
    if (Test-Path $pdbPath) {
      $newPdb = "{0}_{1}.pdb" -f $base, $rid
      try { Rename-Item -Path $pdbPath -NewName $newPdb -Force; Write-Host "Renamed PDB: $(Split-Path $pdbPath -Leaf) -> $newPdb" } catch { Write-Host "Failed to rename PDB: $_" }
    }
  }
  else { Write-Host "No EXE found in $OutDir to rename." }

  # --- Now create a self-contained single-file build (runtime included) ---
  $OutDirSc = Join-Path $ScriptDir "publish\$rid\singlefile-sc"
  Write-Host "Publishing self-contained single-file (RID=$rid) to: $OutDirSc"

  dotnet publish $ScriptDir `
    -c Release `
    -r $rid `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:PublishTrimmed=false `
    -p:OutputType=WinExe `
    -o $OutDirSc

  Write-Host "Self-contained publish complete for $rid. Listing output directory:"
  if (Test-Path $OutDirSc) { Get-ChildItem -Force $OutDirSc | Format-List } else { Write-Host "Output directory not found: $OutDirSc" }

  $exeSc = Get-ChildItem -Path $OutDirSc -Filter *.exe -File -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($exeSc) {
    $origNameSc = $exeSc.Name
    $baseSc = [System.IO.Path]::GetFileNameWithoutExtension($origNameSc)
    $newNameSc = "{0}_{1}_sc.exe" -f $baseSc, $rid
    try { Rename-Item -Path $exeSc.FullName -NewName $newNameSc -Force; Write-Host "Renamed: $origNameSc -> $newNameSc" } catch { Write-Host "Failed to rename EXE: $_" }

    $pdbPathSc = Join-Path $OutDirSc ($baseSc + ".pdb")
    if (Test-Path $pdbPathSc) {
      $newPdbSc = "{0}_{1}_sc.pdb" -f $baseSc, $rid
      try { Rename-Item -Path $pdbPathSc -NewName $newPdbSc -Force; Write-Host "Renamed PDB: $(Split-Path $pdbPathSc -Leaf) -> $newPdbSc" } catch { Write-Host "Failed to rename PDB: $_" }
    }
  }
  else { Write-Host "No EXE found in $OutDirSc to rename." }
}

Write-Host "Done. To run without showing a console window, launch the EXE from Explorer or use Start-Process to detach from an open console."
