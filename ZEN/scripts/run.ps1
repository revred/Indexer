Push-Location $PSScriptRoot/..
dotnet build -c Release
dotnet test -c Release
dotnet run -c Release --project Cli -- --data ../DATA --out ../OUTPUT/IndexContainment.xlsx --symbols SPY,QQQ,IWM,DIA --anchor 10:00 --resample auto
Pop-Location