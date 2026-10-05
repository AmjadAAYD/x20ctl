# Contributing

Keep collection small, selected-device-only, local and inspectable. Do not add an uploader,
updater, analytics, controller writes, serial requests, pairing or driver installer.
Change the capability policy and review implications explicitly before expanding scope.

Run `python -m unittest discover -s tests -v` and the hardware-free commands. On Windows,
install `requirements.lock` first so package-import self-checks run. Archive/privacy/consent
regressions and guard failures must be resolved before release.

Never submit real volunteer results, PCAP/PCAPNG/BTSnoop files, Android bug reports,
device paths/addresses or personal identifiers in public commits or issues. Use synthetic
fixtures for tests. `.gitignore` is a convenience, not an access-control boundary; it cannot
stop a forced add. Share sensitive reproduction details privately.
