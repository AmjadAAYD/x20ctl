# X20CTL privacy policy

Effective 10 October 2026. This policy covers the X20CTL desktop app for Windows and the Linux Controller Check
(`x20ctl-check.py`). X20CTL is an independent open-source project; it is not affiliated with
EasySMX.

## The short version

X20CTL works on your own PC. It has no account, no analytics and no advertising. Something leaves your PC only when
you press a button that says it will send, and only to the X20CTL inbox described below. The one exception is an
update check, which is on by default and you can turn off (see "Updates").

## What stays on your PC

These are stored on your computer only and never uploaded:

- your setups, preferences and player assignments (in `%APPDATA%\x20ctl` on Windows);
- a crash log, if the app hits an error (in `%LOCALAPPDATA%\x20ctl\native\crash.log`);
- every Controller Check report ZIP (in `Documents\X20CTLInputReports` on Windows, `~/X20CTLInputReports` on Linux).
- Write scan findings, only when you press Save (in `Documents\X20CTLWriteScans`). The phone's Bluetooth log you open is read where it is; a copy is kept only if you tick "Also save a copy of the Bluetooth log".

Controller serial numbers are read only if you turn on "Include serial numbers", and even then they stay in the local
report.

## What is sent, and only when you choose to

**Send feedback** (Tools → Send feedback) sends:

- the message you type and the topic you pick;
- the controller and connection type, if you choose them;
- the X20CTL version and your Windows version;
- the time it was sent and a random submission ID.

**Scan and send the report** (Controller Check, Windows and Linux) sends a compact summary of the scan:

- the names and USB/Bluetooth IDs the controller gives your operating system, and the layout of its input interfaces;
- the result of each scan step, and measurements such as stick range, drift, trigger travel, report rate, turbo rate
  and what each paddle sends;
- any battery level the operating system reports for the controller, with the controller's name;
- the bytes the controller sent by itself on its vendor channels during the scan;
- the X20CTL and scanner versions, your operating system version, the model and connection you picked, the time and a
  random submission ID.

The summary does not include serial numbers, file paths, your user name, other devices, or the full local report.
Choosing "Scan, keep it on this PC" sends nothing.

**Send to X20CTL** (Write scan results) sends, only after you tick "I've read the summary above and I'm happy to
send it":

- the summary shown on screen, the findings, and the timeline;
- every message the official app and the controller exchanged in the recording, decoded. These can include the
  controller's name and an id it reports;
- what you typed about the test (controller, app, setting and values) and when you confirmed each step;
- only if you also tick it, the Bluetooth data packets of the controller's connection, from just before the test to
  just after it, re-packed without Bluetooth's own messages (the part where device names and addresses travel). The
  phone's other devices, such as earbuds or a watch, are left out. What the app and the controller said is still in it;
  re-packing is not anonymising;
- the X20CTL version, your operating system version, the time and a random submission ID.

It never sends the bug report or sysdiagnose itself, or anything else from the phone.

**`x20 state save`** (the command line) reads the controller's setting records and writes them to the file you name.
`x20 state diff` compares two such files. Both stay on your PC; nothing is uploaded. Nothing is sent until the inbox
says it accepts Write scans; until then the findings stay on your PC.

## Where it goes and who can see it

Submissions go to the X20CTL inbox at `x20-admin.vercel.app`, a private service run by the maintainer. It is hosted on
[Vercel](https://vercel.com/legal/privacy-policy), and submissions are kept in private Vercel Blob storage that only the
maintainer can read. The inbox stores what is listed above; it does not record your IP address with a submission.
Vercel, as the host, may keep its own short-lived request logs under its privacy policy.

Submissions are used only to fix problems and to add support for more controllers. They are never sold, shared for
advertising, or used to identify you. Findings may be summarised in public (for example "the Dune 8K reports 1000
reports per second over its dongle") without anything that identifies who sent them.

## How long it is kept, and deleting it

Submissions are kept while they help support a controller or answer feedback, and deleted when no longer needed. Each
submission gives you a receipt (`FB-…` for feedback, `CR-…` for reports). To have one deleted, open an issue at
<https://github.com/AmjadAAYD/x20ctl/issues> or message `mistermajid` on Discord with the receipt. You don't need to
say anything else about yourself.

## Updates

Tools has a "Keep X20CTL up to date" switch, on by default (the setup asks about it too). While it is on, each launch asks GitHub's public releases
API (`api.github.com`) whether a newer X20CTL exists, and the one-file exe downloads it from the project's GitHub
releases, checks it against that release's `SHA256SUMS.txt`, and replaces itself for the next launch. "Check now" does
the same check once. These requests carry nothing about you or your controllers; GitHub sees them like any download,
under [GitHub's privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement).

## Links to other sites

Buttons such as "Star our GitHub repo" and "Support X20CTL" open GitHub or Ko-fi in your browser. Those sites have
their own privacy policies; X20CTL sends them nothing.

## Children

X20CTL isn't directed at children and doesn't knowingly collect information about anyone.

## Changes

Changes to this policy are made in this file, so its history on GitHub shows every version.
