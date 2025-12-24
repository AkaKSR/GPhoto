#!/usr/bin/env bash
set -euo pipefail

# Publish framework-dependent single-file Release build (no runtime bundled)
# Usage:
#   ./build_singlefile_fd.sh            # uses default RID win-x64
#   RID=win-x86 ./build_singlefile_fd.sh

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
RID="${RID:-win-x64}"
OUTDIR="$ROOT_DIR/publish/$RID/singlefile-fd"

echo "Publishing framework-dependent single-file (RID=$RID) to: $OUTDIR"

dotnet publish "$ROOT_DIR" \
  -c Release \
  -r "$RID" \
  -p:PublishSingleFile=true \
  -p:SelfContained=false \
  -p:PublishTrimmed=false \
  -p:OutputType=WinExe \
  -o "$OUTDIR"

echo "Publish complete. Listing output directory:" 
ls -la "$OUTDIR" || true

echo "Done. To run without showing a console window, launch the EXE from Explorer or use 'start' / 'Start-Process' to detach from an open console." 
