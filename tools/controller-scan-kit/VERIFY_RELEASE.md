# Verify a scanner release

Choose a `scanner-v…` release from **AmjadAAYD/x20ctl**. Download the portable ZIP,
`SHA256SUMS.txt`, `BUILD_INFO.json`, and `SBOM.cdx.json`. The separate EXE is also available.

In PowerShell, check the downloaded ZIP against its exact line in the checksum file:

```powershell
Get-FileHash .\ControllerScanKit-1.1.0-win-x64.zip -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

If you downloaded every listed asset, use the source's `scripts/verify_release.ps1`
with `-Directory` pointing at your downloads. After extraction, compare the inner EXE's
hash with `executableSha256` in `BUILD_INFO.json`. A checksum detects changed bytes; a
checksum distributed beside a malicious file is not independent evidence of safety.

To verify GitHub build provenance using GitHub CLI:

```powershell
gh attestation verify .\ControllerScanKit-1.1.0-win-x64.zip --repo AmjadAAYD/x20ctl --signer-workflow AmjadAAYD/x20ctl/.github/workflows/controller-scan-kit.yml
gh attestation verify .\ControllerScanKit.exe --repo AmjadAAYD/x20ctl --signer-workflow AmjadAAYD/x20ctl/.github/workflows/controller-scan-kit.yml
```

Inspect the verified provenance's source commit/workflow and compare it with the release
tag and `sourceCommit` in `BUILD_INFO.json`. Open its `workflowRun` URL and inspect the
tests, CodeQL, build, packaged checks, Defender scan and attestation steps. Do not infer
a successful run from a README claim. The metadata records actual versions and hashes.
The SBOM lists the locked Python runtime/build distributions and CPython, not every native DLL.

The EXE is unsigned and the workflow does not claim bit-for-bit reproducibility. A build
attestation is not an Authenticode signature or a guarantee that the code is harmless.
Physical-controller tests and clean-machine acceptance are not performed by the build.
You can inspect and run [the source](README.md#run-the-source-instead) instead.
