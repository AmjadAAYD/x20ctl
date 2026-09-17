@echo off
setlocal enabledelayedexpansion

echo ==========================================================
echo        x20ctl - Windows Standalone EXE Compiler
echo ==========================================================
echo.
echo [1/3] Verifying Python installation...
python --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Python is not installed or not in your PATH!
    echo Please download and install Python from https://python.org
    pause
    exit /b 1
)

echo [2/3] Installing Python dependencies (bleak, pyinstaller, pygame)...
python -m pip install --upgrade pip
python -m pip install -r requirements.txt

echo.
echo [3/3] Compiling x20ctl.exe using PyInstaller...
python build_exe.py

if %ERRORLEVEL% equ 0 (
    echo.
    echo ==========================================================
    echo [SUCCESS] x20ctl.exe compiled successfully!
    echo Location: dist\x20ctl.exe
    echo ==========================================================
    echo.
    if exist "dist\x20ctl.exe" (
        echo Launching x20ctl.exe in 3 seconds...
        timeout /t 3 /nobreak >nul
        start "" "dist\x20ctl.exe"
    )
) else (
    echo.
    echo [ERROR] PyInstaller build failed. Check console output above.
)

pause
