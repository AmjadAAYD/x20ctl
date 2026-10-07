X15 TRIGGER CHECK - SMALL WINDOWS KIT

1. Use Windows "Extract All" on this ZIP.
2. Double-click START_HERE.bat. No Python or administrator rights needed.
3. Close games, Steam, remappers and controller settings apps.
4. Follow the disconnect/reconnect and selection instructions.
5. Pull ONLY the trigger named on screen. Move slowly to quarter, half,
   three-quarter and full travel, then release. Keep the other trigger released.
6. Send Amjad the NEW ZIP inside the results folder. It contains the input
   samples, device/mode metadata, integrity hashes and SUMMARY.txt.

The tool records about 35 seconds of input, plus time for your answers.
Values between 1 and 254 demonstrate partial values on that input path.
Only 0 and 255 means no intermediate values were observed in this run;
it does NOT prove the physical sensors are digital. Mixed-trigger pulls,
missing neutral samples and incomplete pulls can be inconclusive.
Approximate physical positions are instructions, not measured calibration.

Supported input paths:
- Windows standard XInputGetState.
- The 0079:181C, gamepad usage 1/5, input=10/output=5/feature=0 HID profile
  found in the X15-labelled submission. This is an experimental layout match,
  NOT automatic X15 identification. Unknown HID layouts are not guessed.

Keep the current mode for your first test. If you already know how to select
another normal controller mode, change it yourself and repeat separately.
Enter "unknown" if unsure; do not install an app or update firmware for this.
If no input path appears, send the exact message and connection/mode details.

All results stay local. Raw controller input is included after your consent;
review it privately before sending. No network access, driver installation,
firmware operation, feature-report probe or configuration/motor write is used.
Serial numbers and full device paths are not requested/exported.
Host sampling frequency is NOT controller polling rate or latency.

Windows 10/11 x64 with .NET Framework 4.5+ (normally supplied by Windows).
No Python required. Local unsigned research tool, not a release of x20ctl.
Tested offline with synthetic values and received recordings; live hardware
and a clean Windows installation have not been tested by this build.

Technical source is included in technical/TriggerCheck.cs under the included
MIT LICENSE. Offline maintainer checks:
  TriggerCheck.exe --self-test
  TriggerCheck.exe --analyze "PATH TO EXTRACTED INPUT RESULTS"
Neither offline command enumerates or reads devices.

API references:
https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetdeviceinterfacedetailw
https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/obtaining-hid-reports
https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream
