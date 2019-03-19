#!/usr/bin/env bash

# SimpleApp
echo "SimpleApp"

cd ./integration/SimpleApp

rm WORKSPACE
rm **/BUILD.bazel
dotnet run --project ../../vs-to-bazel -- ./SimpleApp.sln

bazel build ...
