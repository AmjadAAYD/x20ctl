@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0ControllerScanKit.exe" (
  echo The executable is missing. Extract the entire kit ZIP first.
  echo See SOURCE_REVIEW.md for the optional Python source route.
  pause
  exit /b 1
)
"%~dp0ControllerScanKit.exe" --advanced --output "%~dp0results"
echo.
echo Collection ended. Review the results folder before sharing.
pause
