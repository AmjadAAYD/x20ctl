@echo off
echo Starting x20ctl Gamepad Configurator (Python)...
python app.py
if %ERRORLEVEL% neq 0 (
    echo.
    echo If missing modules, please run: pip install -r requirements.txt
    pause
)
