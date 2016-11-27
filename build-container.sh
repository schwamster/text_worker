#!bin/bash
set -e
dotnet restore
dotnet test test/text-worker.test/project.json
rm -rf $(pwd)/publish/worker
dotnet publish src/text_worker/project.json -c release -o $(pwd)/publish/worker