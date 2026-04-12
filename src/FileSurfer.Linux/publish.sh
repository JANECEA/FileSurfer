#!/bin/bash
set -e
PROJ="FileSurfer.Linux.csproj"
OUT_BASE="publish/release/net8.0/"

rm -rf $OUT_BASE

for arch in linux-x64 linux-arm64; do
    echo "Publishing $arch..."
    dotnet publish "$PROJ" -c Release -r "$arch" --self-contained true -o "$OUT_BASE/$arch"
done

echo "Done."