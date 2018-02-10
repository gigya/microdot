@echo off
cd /d %~dp0
paket.bootstrapper.exe
paket.exe restore
echo.
echo.
pause