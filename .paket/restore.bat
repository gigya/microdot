@echo off
cd /d %~dp0
paket.bootstrapper.exe 5.124.1
paket.exe restore
echo.
echo.
pause