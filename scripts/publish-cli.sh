#!/usr/bin/env bash
set -euo pipefail

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/src/RazorFormatter.Cli/RazorFormatter.Cli.csproj"

CONFIGURATION="${CONFIGURATION:-Release}"
OUTPUT_BASE="${OUTPUT_BASE:-$REPO_ROOT/artifacts}"
PACK_TOOL="${PACK_TOOL:-true}"
PUBLISH_STANDALONE="${PUBLISH_STANDALONE:-true}"
RUNTIMES="${RUNTIMES:-win-x64 linux-x64 osx-x64 osx-arm64}"

echo "===================================================="
echo " RazorFormatter CLI Distribution Publisher"
echo " Configuration: $CONFIGURATION"
echo " Output Directory: $OUTPUT_BASE"
echo "===================================================="

mkdir -p "$OUTPUT_BASE"

# 1. Package .NET Global Tool
if [ "$PACK_TOOL" = "true" ]; then
    TOOL_OUTPUT_DIR="$OUTPUT_BASE/tool"
    echo ""
    echo "[1/2] Packing .NET Global Tool..."
    dotnet pack "$PROJECT_PATH" \
        --configuration "$CONFIGURATION" \
        --output "$TOOL_OUTPUT_DIR"
    echo "Global Tool package generated at: $TOOL_OUTPUT_DIR"
fi

# 2. Publish Self-Contained Single-File Executables
if [ "$PUBLISH_STANDALONE" = "true" ]; then
    echo ""
    echo "[2/2] Publishing Self-Contained Single-File Executables..."

    for RID in $RUNTIMES; do
        TARGET_DIR="$OUTPUT_BASE/$RID"
        echo ""
        echo " -> Publishing for RID: $RID..."

        dotnet publish "$PROJECT_PATH" \
            --configuration "$CONFIGURATION" \
            --runtime "$RID" \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:IncludeNativeLibrariesForSelfExtract=true \
            -p:EnableCompressionInSingleFile=true \
            --output "$TARGET_DIR"

        echo " -> Successfully published $RID to: $TARGET_DIR"
    done
fi

echo ""
echo "===================================================="
echo " CLI Distribution Build Complete!"
echo " Artifacts stored at: $OUTPUT_BASE"
echo "===================================================="

