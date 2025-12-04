@echo off
:: ChillPatcher Build Script
:: Simply build the project - auto-deploy is configured in csproj

setlocal

set Configuration=Debug
if not "%1"=="" set Configuration=%1

echo.
echo === ChillPatcher Build Script ===
echo.
echo Configuration: %Configuration%
echo.

:: Just build - the csproj will handle copying to plugins directory
dotnet build ChillPatcher.csproj -c %Configuration%

if errorlevel 1 (
    echo.
    echo Build failed!
    exit /b 1
)

echo.
echo === Build Complete ===
echo.
echo Auto-deployed to:
echo   C:\Program Files (x86)\Steam\steamapps\common\wallpaper_engine\projects\myprojects\chill_with_you\BepInEx\plugins\ChillPatcher\
echo.
echo Files:
echo   - ChillPatcher.dll
echo   - System.Data.SQLite.dll
echo   - x64/SQLite.Interop.dll (x64 native)
echo   - x86/SQLite.Interop.dll (x86 native)
echo   - rime.dll (if librime was compiled)
echo.

endlocal
