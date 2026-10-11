# X20CTL privacy policy

Effective 10 October 2026. This policy covers the X20CTL desktop app for Windows and the Linux Controller Check
(`x20ctl-check.py`). X20CTL is an independent open-source project maintained by Amjad AAYD; it is not affiliated with
EasySMX.

## The short version

X20CTL works on your own PC. It has no account, no analytics and no advertising. Something leaves your PC only when
you press a button that says it will send, and only to the X20CTL inbox described below. The one exception is an
update check you can turn on yourself (see "Updates"); it is off unless you turn it on.

## What stays on your PC

These are stored on your computer only and never uploaded:

- your setups, preferences and player assignments (in `%APPDATA%\x20ctl` on Windows);
- a crash log, if the app hits an error (in `%LOCALAPPDATA%\x20ctl\native\crash.log`);
- every Controller Check report ZIP (in `Documents\X20CTLInputReports` on Windows, `~/X20CTLInputReports` on Linux).

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

Tools has a "Keep X20CTL up to date" switch, off by default. When it is on, each launch asks GitHub's public releases
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
