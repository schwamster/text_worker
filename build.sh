#!/bin/bash
set -e
dotnet restore
# dotnet test test/folder/project.json
rm -rf $(pwd)/publish/worker
dotnet publish text_worker/project.json -c release -o $(pwd)/publish/worker
