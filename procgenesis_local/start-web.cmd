@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "WEB_DIR=%SCRIPT_DIR%web"

cd /d "%WEB_DIR%" || (
  echo Error: failed to enter "%WEB_DIR%".
  exit /b 1
)

where python >nul 2>nul
if %errorlevel%==0 (
  echo Starting web server with Python at http://localhost:8000
  python server.py
  goto :eof
)

where py >nul 2>nul
if %errorlevel%==0 (
  echo Starting web server with Python Launcher at http://localhost:8000
  py -3 server.py
  goto :eof
)

where node >nul 2>nul
if %errorlevel%==0 (
  echo Starting web server with Node.js at http://localhost:8080
  node server.js
  goto :eof
)

echo Error: python/python launcher or node is required to run the web server.
exit /b 1
