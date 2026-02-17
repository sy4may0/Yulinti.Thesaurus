#!/bin/bash
docker run --rm -it \
  -v "$(pwd)":/work \
  -w /work \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet new classlib -n thesaurus -f netstandard2.1

sudo chown -R $(id -u):$(id -g) "$(pwd)"