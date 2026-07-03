@echo off
setlocal

cd /d "%~dp0"

if not exist "logs" mkdir "logs"

"C:\Program Files\dotnet\dotnet.exe" run --no-build --urls http://localhost:5248 > "logs\cashflow-ai.out.log" 2> "logs\cashflow-ai.err.log"
