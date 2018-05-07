@echo off
cd /d %~dp0
paket.bootstrapper.exe
cd ..
.paket\paket.exe pack nugetPackages --minimum-from-lock-file --pin-project-references --build-config "Debug"  -v --symbols
echo.
echo.
pause