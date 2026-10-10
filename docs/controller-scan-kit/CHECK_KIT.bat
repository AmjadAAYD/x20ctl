@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0ControllerScanKit.exe" (
  echo The executable is missing. Extract the entire kit ZIP first.
  pause
  exit /b 1
)
"%~dp0ControllerScanKit.exe" --self-check
echo.
echo This check uses synthetic data. It does not scan or open a controller.
pause
