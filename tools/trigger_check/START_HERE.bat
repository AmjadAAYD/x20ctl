@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0TriggerCheck.exe" (
  echo Use Extract All on the ZIP first. TriggerCheck.exe is missing.
  pause
  exit /b 1
)
"%~dp0TriggerCheck.exe" --self-test
if errorlevel 1 (
  echo The offline tool check failed. Send Amjad this message.
  pause
  exit /b 1
)
echo.
"%~dp0TriggerCheck.exe"
echo.
echo Your result ZIP, if created, is inside the results folder next to this launcher.
pause
