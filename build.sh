#!/bin/bash

# rid: ["osx-x64", "osx-arm64", "linux-x64", "win-x64", "win-arm64"]
#rid="osx-x64"
rid="linux-x64"

dotnet publish NginxCacheFileReader/NginxCacheFileReader.csproj \
    -c Release \
    -r ${rid} \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    --self-contained \
    -o build/

scp build/* pro-cache-01:/tmp/
scp build/* pro-cache-02:/tmp/
