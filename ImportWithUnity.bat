@echo off
setlocal
REM Imports the .unitypackage next to this file with Unity itself (no Import window).
REM Put this file + the package in the project folder (next to Assets), close Unity, double-click.
cd /d "%~dp0"
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not exist "%UNITY%" (
  echo [X] Unity 6000.3.22f1 not found at default Hub path.
  echo     Edit the UNITY= line in this file to your Unity.exe path.
  pause
  exit /b 1
)
if not exist "%~dp0Assets" (
  echo [X] Put this file in the project folder next to Assets.
  pause
  exit /b 1
)
tasklist /fi "imagename eq Unity.exe" | find /i "Unity.exe" >nul && (
  echo [X] Close Unity first.
  pause
  exit /b 1
)
set "PKG="
for /f "delims=" %%f in ('dir /b /o:d "%~dp0*.unitypackage" 2^>nul') do set "PKG=%~dp0%%f"
if not defined PKG (
  echo [X] No .unitypackage next to this file.
  pause
  exit /b 1
)
echo Importing "%PKG%" ... (1-5 min, no window)
"%UNITY%" -batchmode -quit -projectPath "%~dp0." -importPackage "%PKG%" -logFile "%~dp0import_log.txt"
if errorlevel 1 (
  echo [!] Unity reported an error. See import_log.txt
) else (
  echo [OK] Done. Open the project in Unity.
)
pause
