@echo off
rem Launch the app with no console window behind it.
rem
rem pythonw.exe is the same interpreter as python.exe with the console
rem subsystem flag cleared, so nothing opens a black window. Starting it with
rem `start ""` also detaches it, which means closing this script does not
rem take the app with it.
start "" pythonw -m x20ctl.gui %*
