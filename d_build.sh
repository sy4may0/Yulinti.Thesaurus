#!/bin/bash
docker run --rm -it \
  -v "$(pwd)/thesaurus":/work \
  -w /work \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet build
