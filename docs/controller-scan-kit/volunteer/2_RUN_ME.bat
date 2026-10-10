@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0tool\ControllerScanKit.exe" (
  echo The tool is missing. Use Extract All on the ZIP and try again.
  echo If it is still missing, tell Amjad. You do not need to install anything.
  pause
  exit /b 1
)
"%~dp0tool\ControllerScanKit.exe" --output "%~dp0results"
echo.
if exist "%~dp0results" (
  echo Open 3_SEND_THE_RESULT.txt for what to send to Amjad.
  start "" explorer.exe "%~dp0results"
) else (
  echo No result folder was created. Tell Amjad where you stopped.
)
pause
