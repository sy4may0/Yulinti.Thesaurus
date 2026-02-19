#!/bin/bash
docker run --rm -it \
  -v "$(pwd)/.":/work \
  -w /work \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test thesaurus.tests/thesaurus.tests.csproj
