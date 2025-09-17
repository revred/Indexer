#!/usr/bin/env bash
set -e
cd "$(dirname "$0")/.."
dotnet build -c Release
dotnet test -c Release
dotnet run -c Release --project Cli -- --data ../DATA --out ../OUTPUT/IndexContainment.xlsx --symbols SPY,QQQ,IWM,DIA --anchor 10:00