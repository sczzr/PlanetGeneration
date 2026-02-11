@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "CSPROJ_DIR=%SCRIPT_DIR%csharp"

cd /d "%CSPROJ_DIR%" || (
  echo Error: failed to enter "%CSPROJ_DIR%".
  exit /b 1
)

where godot4-mono >nul 2>nul
if %errorlevel%==0 (
  godot4-mono --path .
  goto :eof
)

where godot4 >nul 2>nul
if %errorlevel%==0 (
  godot4 --path .
  goto :eof
)

where godot >nul 2>nul
if %errorlevel%==0 (
  godot --path .
  goto :eof
)

echo Error: Godot was not found in PATH. Please install Godot 4 ^(.NET^), then run again.
exit /b 1
