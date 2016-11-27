#!bin/bash
set -e
dotnet restore
dotnet test test/text_worker.test/project.json -xml $(pwd)/testresults/out.xml
rm -rf $(pwd)/publish/worker
dotnet publish src/text_worker/project.json -c release -o $(pwd)/publish/worker