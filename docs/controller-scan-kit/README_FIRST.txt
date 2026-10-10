MAINTAINER REFERENCE - ADVANCED CONTROLLER COLLECTION
Prepared for Amjad's controller research, 1 October 2026.

Amjad: start with 1_READ_ME_AMJAD.txt. Send the separate Volunteer Kit to helpers.
This longer reference describes advanced collection, which requires --advanced.
The default run is now a short first discovery, without input or BLE tests.

WHAT THIS IS FOR
Help Amjad identify your controller's Windows interfaces and normal input.
You do not need programming or reverse-engineering experience. Collection
is voluntary, local, and you can skip a stage or press CTRL+C to stop.

1. Extract the entire ZIP into a folder you can write to, such as Documents.
2. Read PRIVACY.txt. Close games, Steam Input and other controller remappers.
3. Double-click CHECK_KIT.bat for an optional offline package check.
4. For a maintainer-guided advanced run, open ControllerScanKit.exe with
   --advanced, or use the maintainer START_HERE.bat launcher.
5. Choose the printed model. X20 and X20 Pro are listed first. Owners of X05,
   X05 Pro, X10, D10 or another model: choose Other and enter the exact model.
6. Use one controller and one normal connection/mode for each session.
   Disconnect the target when asked, then connect it normally when asked.
   For a receiver session, unplug/reconnect the receiver at these prompts.
7. Select the device that appeared. Do not guess if several devices changed.
8. Input and BLE tests are optional. Use only modes documented for your pad.
9. Review the local files, create the ZIP, and follow HOW_TO_SEND.txt.

The model you enter is your identification claim, not automatic detection.
The app still has only X20 and the inactive X20 Pro placeholder. This kit
does not add support for another model or activate the Pro placeholder.

WHAT THE WIZARD DOES
- Compare Windows device inventories before/after the target is connected.
- Export selected-device VID/PID, driver service, available HID usage/caps.
- Try standard USB device/configuration descriptors for the confirmed target.
- Optionally record labeled XInput states or selected gameplay HID input bytes.
- Optionally discover a selected BLE target's service/characteristic UUIDs
  and read only standard battery and device-information allowlisted fields.
- Put separate connection/mode sessions in a timestamped, checksummed report.

WHAT YOU SHOULD EXPECT TO BE MISSING
The wizard does not obtain original HID report descriptor bytes, send EasySMX
configuration commands, monitor the manufacturer's app, or install USBPcap.
It does not record RGB/vibration/trigger configuration traffic. Home/Guide
is absent from documented XInputGetState. A receiver's USB descriptors describe
the receiver side. Classic Bluetooth input and BLE configuration are different.
Missing data is marked unavailable/skipped, never filled with guessed values.

BLE connection and standard metadata reads are real device operations. There
are no firmware/configuration writes, vendor-feature sweeps, notification
subscriptions or automatic pairing. The kit makes no internet upload.

When input starts, repeat the requested press/movement slowly until the result
appears. The worker may take a moment to start; it records a five-second window
after it opens the source. Release controls between presses. Extra controls can be
entered before the test list. 's' skips one action; 'q' ends the input tests.
Changing a byte is an observation, not proof that it encodes that control.
The host sampling interval is not the controller's polling rate or latency.

OUTPUT
results/scan-<random submission ID>/ contains readable evidence files.
results/scan-<same ID>.zip is the report to review and send privately.
Different sessions remain separate; start another run for a different model.
CTRL+C during collection creates a marked unreviewed partial ZIP. CTRL+C
during the final review leaves the folder without creating a ZIP.

This is an unsigned research build, not a published x20ctl release. Its SHA-256
and auditable source are included. Do not disable antivirus or replace drivers
to run it. If Windows blocks it, stop and tell Amjad the exact message.
No installation, administrator launch or security bypass is requested.

FOLDER GUIDE
START_HERE.bat             Start the wizard
CHECK_KIT.bat              Synthetic self-check; no hardware access
ControllerScanKit.exe      Windows x64 standalone collector + offline validator
PRIVACY.txt                What stays private and what needs review
HOW_TO_SEND.txt            Files and summary to send to Amjad
TROUBLESHOOTING.txt        Empty/failed/blocked stages and recovery
ADVANCED_CAPTURE.md       Guided follow-up only, after Amjad reviews discovery
MAINTAINER.md             Schema, interpretation, acceptance and source limits
SOURCE_REVIEW.md           How to inspect/rebuild the supplied source
source/                   Exact source used for this build; no installer
research/                 Deep background research, messages and templates
licenses/                 Project and bundled dependency notices
BUILD_INFO.json           Python/dependency versions and source hashes
CONTENTS_SHA256.txt        Hash of every kit file other than this hash list

The research/ documents are the earlier design/research snapshot. Proposed
collector behavior there is background; this README and MAINTAINER.md describe
what this implemented kit actually does. Optional PCAPs/photos go separately,
not into the automated report format. No official app/firmware is redistributed.
