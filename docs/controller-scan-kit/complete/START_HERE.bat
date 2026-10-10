@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0ControllerScanKit.exe" (
  echo The tool is missing. Use Extract All on the ZIP first.
  echo If it is still missing, stop and tell Amjad.
  pause
  exit /b 1
)
echo Checking the tool before collection...
"%~dp0ControllerScanKit.exe" --self-check
if errorlevel 1 (
  echo The tool check failed. Stop and send Amjad the message above.
  pause
  exit /b 1
)
echo.
echo 1. Full controller scan and optional settings-app capture
echo 2. Settings-app capture only - use this for another setting
choice /c 12 /n /m "Choose 1 or 2: "
if errorlevel 2 (
  "%~dp0ControllerScanKit.exe" --app-capture --output "%~dp0results"
) else (
  "%~dp0ControllerScanKit.exe" --guided --output "%~dp0results"
)
echo.
echo If the tool said DONE, send the new scan ZIP from results to Amjad.
echo If you stopped or no ZIP was saved, tell Amjad where you got stuck.
if exist "%~dp0results" start "" explorer.exe "%~dp0results"
pause
