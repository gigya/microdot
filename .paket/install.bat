@echo off
cd /d %~dp0
paket.bootstrapper.exe
paket.exe install
echo.
echo.
pause