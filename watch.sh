#!/usr/bin/env bash

echo Contract.cs | entr -s 'dotnet publish -c Release -o bin && cd bin && dotnet ../../neo-compiler/neon/bin/neon.dll neo-raffle.dll'