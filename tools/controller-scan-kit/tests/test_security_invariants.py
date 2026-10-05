"""Regression guards for OUR application source, not proof against malicious code."""
import ast
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
ALLOWED_IMPORTS = {"argparse", "asyncio", "ctypes", "datetime", "hashlib", "json", "math",
                   "pathlib", "platform", "re", "stat", "struct", "subprocess", "sys",
                   "tempfile", "time", "uuid", "zipfile", "bleak", "controller_scan_kit"}
FORBIDDEN_APIS = {"WriteFile", "HidD_SetFeature", "HidD_SetOutputReport", "HidD_GetSerialNumberString",
                 "XInputSetState", "write_gatt_char", "write_gatt_descriptor", "start_notify", "pair",
                 "ShellExecuteW", "ShellExecuteExW", "system", "eval", "exec", "__import__"}


def violations(source):
    result = []
    for node in ast.walk(ast.parse(source)):
        if isinstance(node, ast.Import):
            for alias in node.names:
                if alias.name.split(".")[0] not in ALLOWED_IMPORTS: result.append(alias.name)
        elif isinstance(node, ast.ImportFrom) and node.level == 0:
            if (node.module or "").split(".")[0] not in ALLOWED_IMPORTS: result.append(node.module)
        elif isinstance(node, ast.Attribute) and node.attr in FORBIDDEN_APIS:
            if not (node.attr == "system" and isinstance(node.value, ast.Name) and node.value.id == "platform"):
                result.append(node.attr)
        elif isinstance(node, ast.Name) and node.id in FORBIDDEN_APIS:
            result.append(node.id)
        elif isinstance(node, ast.Constant) and isinstance(node.value, str) and node.value in FORBIDDEN_APIS:
            result.append(node.value)
        elif isinstance(node, ast.Call):
            for kw in node.keywords:
                if kw.arg in {"pair", "shell"} and not (isinstance(kw.value, ast.Constant) and kw.value.value is False):
                    result.append(kw.arg)
    return result


class SecurityTests(unittest.TestCase):
    def test_own_source_has_no_forbidden_imports_or_apis(self):
        for path in (ROOT / "src").rglob("*.py"):
            with self.subTest(path=path.name): self.assertEqual(violations(path.read_text()), [])

    def test_guard_catches_aliases_and_ctypes_bindings(self):
        for source in ("import socket as innocuous", "from urllib.request import urlopen as u",
                       "self._bind(dll, 'WriteFile', None, [])", "client.write_gatt_char(x,y)",
                       "client.pair()", "BleakClient(x, pair=True)", "subprocess.run(x, shell=True)",
                       "__import__('socket')", "eval(x)"):
            with self.subTest(source=source): self.assertTrue(violations(source))
        self.assertFalse(violations("from bleak import BleakClient\nBleakClient(x, pair=False)"))

    def test_policy_matches_intended_invariants(self):
        policy = json.loads((ROOT / "CAPABILITIES.json").read_text())
        for key in ("internetUpload", "telemetry", "controllerConfigurationWrites", "firmwareOperations",
                    "driverInstallation", "administratorRequired", "serialNumberRequested", "automaticPairing", "bleNotifications"):
            self.assertIs(policy[key], False)
        self.assertTrue(policy["limits"]["rawHidAndTracesMayContainIdentifiers"])
        self.assertFalse(policy["limits"]["rawTracesAutomaticallyAnonymized"])

    def test_win32_handles_never_request_generic_write(self):
        tree = ast.parse((ROOT / "src/controller_scan_kit/windows.py").read_text())
        call = next(n for n in ast.walk(tree) if isinstance(n, ast.Call) and
                    isinstance(n.func, ast.Attribute) and n.func.attr == "CreateFileW")
        access = call.args[1]
        self.assertIsInstance(access, ast.IfExp)
        self.assertEqual(access.body.value, 0x80000000)  # GENERIC_READ
        self.assertEqual(access.orelse.value, 0)  # metadata handles

    def test_executable_does_not_request_elevation(self):
        spec = (ROOT / "ControllerScanKit.spec").read_text()
        self.assertIn("uac_admin=False", spec)
        self.assertNotIn("uac_admin=True", spec)

    def test_runtime_lock_has_exact_versions_and_hashes(self):
        for name in ("requirements.lock", "requirements-build.lock"):
            for line in (ROOT / name).read_text().splitlines():
                if line and not line.startswith(("#", " ", "-r")):
                    self.assertRegex(line, r"^[A-Za-z0-9_.-]+==[^\s]+ \\")
            self.assertIn("--hash=sha256:", (ROOT / name).read_text())

    def test_version_is_consistent(self):
        source = (ROOT / "src/controller_scan_kit/__init__.py").read_text()
        version = re.search(r'VERSION = "([^"]+)"', source)[1]
        self.assertIn(f'version = "{version}"', (ROOT / "pyproject.toml").read_text())


if __name__ == "__main__": unittest.main()
