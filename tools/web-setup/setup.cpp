// X20CTL web setup (owner request 19 Oct 2026: "a setup is small"). A few hundred KB: it downloads the release's exe,
// checks it against that release's SHA256SUMS.txt, starts it as the setup wizard (--setup) and removes its download when
// the wizard closes. It installs nothing itself; the wizard in the app does that, so the install is the same as before.
#define WIN32_LEAN_AND_MEAN
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <winhttp.h>
#include <bcrypt.h>
#include <commctrl.h>
#include <shellapi.h>
#include <objbase.h>
#include <uxtheme.h>
#include <atomic>
#include <cwctype>
#include <cstdio>
#include <functional>
#include <string>
#include <thread>
#include <vector>
#include "setup_config.h"

namespace {
#ifdef X20_BASE_URL  // local test builds only: a trace of the steps, so a test can say where a run stopped
void Trace(const std::wstring& text) {
  wchar_t path[MAX_PATH]; GetTempPathW(MAX_PATH, path); wcscat_s(path, L"x20ctl-web-setup-trace.log");
  FILE* f = nullptr; if (_wfopen_s(&f, path, L"a, ccs=UTF-8") == 0 && f) { fwprintf(f, L"%llu %s\n", GetTickCount64(), text.c_str()); fclose(f); }
}
#else
inline void Trace(const std::wstring&) {}
#endif
const COLORREF kBack = RGB(10, 15, 28), kText = RGB(231, 238, 250), kSoft = RGB(150, 165, 190), kBar = RGB(47, 123, 240), kTrack = RGB(26, 36, 60);
enum State { Running, Done, Failed, Cancelled };
std::atomic<State> state{Running};
std::atomic<bool> cancelRequested{false}, launched{false};
std::atomic<unsigned long long> doneBytes{0}, totalBytes{0};
std::atomic<int> phase{0};  // 0 checksums, 1 download, 2 check, 3 wizard
CRITICAL_SECTION lock; std::wstring failure; HWND window, title, status, bar, cancel; HFONT titleFont, bodyFont; HBRUSH backBrush; int dpi = 96;

int S(int v) { return MulDiv(v, dpi, 96); }
std::wstring BaseUrl() {
#ifdef X20_BASE_URL
  return X20_BASE_URL;
#else
  return std::wstring(L"https://github.com/") + X20_REPO + L"/releases/download/" + X20_TAG + L"/";
#endif
}
std::wstring ExeName() { return std::wstring(L"X20CTL-") + X20_NAME + L"-windows-x64.exe"; }
void Fail(const std::wstring& message) { Trace(L"fail: " + message); EnterCriticalSection(&lock); if (failure.empty()) failure = message; LeaveCriticalSection(&lock); state = Failed; }

// One GET: the body goes to `sink` in pieces; returning false from it stops the transfer.
bool Get(const std::wstring& url, const std::function<bool(const BYTE*, DWORD)>& sink, std::wstring& error) {
  URL_COMPONENTS parts{}; parts.dwStructSize = sizeof parts; parts.dwHostNameLength = parts.dwUrlPathLength = parts.dwExtraInfoLength = (DWORD)-1;
  if (!WinHttpCrackUrl(url.c_str(), 0, 0, &parts)) { error = L"The address is not valid."; return false; }
  std::wstring host(parts.lpszHostName, parts.dwHostNameLength), path(parts.lpszUrlPath, parts.dwUrlPathLength + parts.dwExtraInfoLength);
  HINTERNET session = WinHttpOpen(L"X20CTL-setup/" X20_NAME, WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY, nullptr, nullptr, 0);
  if (!session) { error = L"Windows could not start a download."; return false; }
  WinHttpSetTimeouts(session, 15000, 15000, 30000, 60000);
  HINTERNET connection = WinHttpConnect(session, host.c_str(), parts.nPort, 0);
  HINTERNET request = connection ? WinHttpOpenRequest(connection, L"GET", path.c_str(), nullptr, WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, parts.nScheme == INTERNET_SCHEME_HTTPS ? WINHTTP_FLAG_SECURE : 0) : nullptr;
  bool ok = false;
  if (request && WinHttpSendRequest(request, WINHTTP_NO_ADDITIONAL_HEADERS, 0, WINHTTP_NO_REQUEST_DATA, 0, 0, 0) && WinHttpReceiveResponse(request, nullptr)) {
    DWORD code = 0, size = sizeof code;
    WinHttpQueryHeaders(request, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER, WINHTTP_HEADER_NAME_BY_INDEX, &code, &size, WINHTTP_NO_HEADER_INDEX);
    if (code != 200) error = L"The server answered " + std::to_wstring(code) + L".";
    else {
      wchar_t length[32]; size = sizeof length;
      if (WinHttpQueryHeaders(request, WINHTTP_QUERY_CONTENT_LENGTH, WINHTTP_HEADER_NAME_BY_INDEX, length, &size, WINHTTP_NO_HEADER_INDEX)) totalBytes = _wcstoui64(length, nullptr, 10);
      std::vector<BYTE> buffer(64 * 1024); ok = true;
      for (;;) {
        DWORD read = 0;
        if (!WinHttpReadData(request, buffer.data(), (DWORD)buffer.size(), &read)) { error = L"The connection dropped."; ok = false; break; }
        if (!read) break;
        if (!sink(buffer.data(), read)) { ok = false; if (error.empty()) error = L"Stopped."; break; }
      }
    }
  } else error = L"Could not reach the download server.";
  if (request) WinHttpCloseHandle(request);
  if (connection) WinHttpCloseHandle(connection);
  WinHttpCloseHandle(session);
  return ok;
}

struct Sha256 {
  BCRYPT_ALG_HANDLE alg = nullptr; BCRYPT_HASH_HANDLE hash = nullptr; bool ok = false;
  Sha256() { ok = BCryptOpenAlgorithmProvider(&alg, BCRYPT_SHA256_ALGORITHM, nullptr, 0) == 0 && BCryptCreateHash(alg, &hash, nullptr, 0, nullptr, 0, 0) == 0; }
  ~Sha256() { if (hash) BCryptDestroyHash(hash); if (alg) BCryptCloseAlgorithmProvider(alg, 0); }
  void Add(const BYTE* data, DWORD size) { if (ok) BCryptHashData(hash, (PUCHAR)data, size, 0); }
  std::wstring Finish() {
    BYTE digest[32]{}; if (!ok || BCryptFinishHash(hash, digest, sizeof digest, 0) != 0) return L"";
    static const wchar_t* hex = L"0123456789abcdef"; std::wstring out;
    for (BYTE b : digest) { out += hex[b >> 4]; out += hex[b & 15]; }
    return out;
  }
};

// SHA256SUMS.txt is `sha256sum` output: "<64 hex>  <file name>" per line.
std::wstring ExpectedHash(const std::string& sums, const std::wstring& file) {
  size_t at = 0;
  while (at < sums.size()) {
    size_t end = sums.find('\n', at); if (end == std::string::npos) end = sums.size();
    std::string line = sums.substr(at, end - at); at = end + 1;
    while (!line.empty() && (line.back() == '\r' || line.back() == ' ')) line.pop_back();
    if (line.size() < 66) continue;
    std::wstring name(line.begin() + 64, line.end()); while (!name.empty() && (name.front() == L' ' || name.front() == L'*')) name.erase(0, 1);
    if (name == file) { std::wstring hash(line.begin(), line.begin() + 64); for (auto& c : hash) c = (wchar_t)towlower(c); return hash; }
  }
  return L"";
}

void Work() {
  const std::wstring base = BaseUrl(), exe = ExeName(); std::wstring error;
  // 1. the checksums of this release
  phase = 0; std::string sums;
  totalBytes = 0; doneBytes = 0;
  if (!Get(base + L"SHA256SUMS.txt", [&](const BYTE* d, DWORD n) { if (cancelRequested) return false; sums.append((const char*)d, n); return true; }, error)) {
    if (cancelRequested) { state = Cancelled; return; }
    Fail(L"Couldn't reach the download. " + error); return;
  }
  Trace(L"sums ok, " + std::to_wstring(sums.size()) + L" bytes");
  std::wstring expected = ExpectedHash(sums, exe); Trace(L"expected=" + expected);
  if (expected.empty()) { Fail(L"This release has no checksum for " + exe + L", so it was not downloaded."); return; }
  // 2. the exe, into a folder of its own in %TEMP%
  wchar_t temp[MAX_PATH]; GetTempPathW(MAX_PATH, temp);
  std::wstring folder = std::wstring(temp) + L"X20CTL-setup-" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64() & 0xffffff), file = folder + L"\\" + exe;
  if (!CreateDirectoryW(folder.c_str(), nullptr)) { Fail(L"Couldn't make a temporary folder."); return; }
  auto clean = [&] { DeleteFileW(file.c_str()); RemoveDirectoryW(folder.c_str()); };
  HANDLE out = CreateFileW(file.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_TEMPORARY, nullptr);
  if (out == INVALID_HANDLE_VALUE) { clean(); Fail(L"Couldn't save the download. Is the disk full?"); return; }
  Trace(L"file created"); Sha256 sha; Trace(sha.ok ? L"sha ok" : L"sha NOT ok"); phase = 1; doneBytes = 0; totalBytes = 0; bool wrote = true;
  bool got = Get(base + exe, [&](const BYTE* d, DWORD n) {
    if (cancelRequested) return false;
    DWORD w = 0; if (!WriteFile(out, d, n, &w, nullptr) || w != n) { wrote = false; return false; }
    sha.Add(d, n); doneBytes += n; return true;
  }, error);
  CloseHandle(out);
  if (!got) { clean(); if (cancelRequested) state = Cancelled; else if (!wrote) Fail(L"Couldn't save the download. Is the disk full?"); else Fail(L"The download stopped. " + error); return; }
  // 3. only a file that matches the release is ever started
  phase = 2;
  if (sha.Finish() != expected) { clean(); Fail(L"The download did not match its checksum, so it was not started."); return; }
#ifdef X20_TEST_NO_LAUNCH
  state = Done; return;
#endif
  // 4. the wizard (the exe asks Windows for permission itself)
  phase = 3;
  CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
  SHELLEXECUTEINFOW run{}; run.cbSize = sizeof run; run.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI; run.lpVerb = L"open"; run.lpFile = file.c_str(); run.lpParameters = L"--setup"; run.lpDirectory = folder.c_str(); run.nShow = SW_SHOWNORMAL;
  if (!ShellExecuteExW(&run)) { DWORD why = GetLastError(); clean(); if (why == ERROR_CANCELLED) { state = Cancelled; return; } Fail(L"Couldn't start the setup (error " + std::to_wstring(why) + L")."); return; }
  launched = true; PostMessageW(window, WM_APP + 1, 0, 0);
  if (run.hProcess) { WaitForSingleObject(run.hProcess, INFINITE); CloseHandle(run.hProcess); }
  clean(); state = Done;
}

std::wstring Megabytes(unsigned long long bytes) { wchar_t t[32]; swprintf_s(t, L"%.0f MB", bytes / 1048576.0); return t; }

void Refresh() {
  switch (phase) {
    case 0: SetWindowTextW(status, L"Finding the release…"); break;
    case 1: {
      unsigned long long d = doneBytes, t = totalBytes;
      SetWindowTextW(status, (L"Downloading… " + Megabytes(d) + (t ? L" of " + Megabytes(t) : L"")).c_str());
      if (t) { SendMessageW(bar, PBM_SETRANGE32, 0, 1000); SendMessageW(bar, PBM_SETPOS, (WPARAM)(d * 1000 / t), 0); }
      break;
    }
    case 2: SetWindowTextW(status, L"Checking the download…"); SendMessageW(bar, PBM_SETPOS, 1000, 0); break;
    default: SetWindowTextW(status, L"Starting setup…"); break;
  }
}

LRESULT CALLBACK Proc(HWND h, UINT m, WPARAM w, LPARAM l) {
  switch (m) {
    case WM_ERASEBKGND: { RECT r; GetClientRect(h, &r); FillRect((HDC)w, &r, backBrush); return 1; }
    case WM_CTLCOLORSTATIC: { HDC dc = (HDC)w; SetBkColor(dc, kBack); SetTextColor(dc, (HWND)l == title ? kText : kSoft); return (LRESULT)backBrush; }
    case WM_COMMAND: if ((HWND)l == cancel) { cancelRequested = true; EnableWindow(cancel, FALSE); SetWindowTextW(status, L"Stopping…"); } return 0;
    case WM_CLOSE: if (state == Running && !launched) { cancelRequested = true; return 0; } break;
    case WM_TIMER: {
      if (state == Running) { if (!launched) Refresh(); return 0; }
      KillTimer(h, 1);
      if (state == Failed) {
        EnterCriticalSection(&lock); std::wstring text = failure; LeaveCriticalSection(&lock);
        ShowWindow(h, SW_HIDE);
        text += L"\n\nOpen the download page to get X20CTL directly?";
        if (MessageBoxW(nullptr, text.c_str(), L"X20CTL setup", MB_YESNO | MB_ICONWARNING | MB_SETFOREGROUND) == IDYES)
          ShellExecuteW(nullptr, L"open", (std::wstring(L"https://github.com/") + X20_REPO + L"/releases/tag/" + X20_TAG).c_str(), nullptr, nullptr, SW_SHOWNORMAL);
        PostQuitMessage(1);
      } else PostQuitMessage(0);
      return 0;
    }
    case WM_APP + 1: ShowWindow(h, SW_HIDE); return 0;
    case WM_DESTROY: PostQuitMessage(0); return 0;
  }
  return DefWindowProcW(h, m, w, l);
}
}  // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
  InitializeCriticalSection(&lock);
  INITCOMMONCONTROLSEX controls{sizeof controls, ICC_PROGRESS_CLASS}; InitCommonControlsEx(&controls);
  HDC screen = GetDC(nullptr); dpi = GetDeviceCaps(screen, LOGPIXELSX); ReleaseDC(nullptr, screen);
  backBrush = CreateSolidBrush(kBack);
  NONCLIENTMETRICSW metrics{sizeof metrics}; SystemParametersInfoW(SPI_GETNONCLIENTMETRICS, sizeof metrics, &metrics, 0);
  LOGFONTW face = metrics.lfMessageFont; face.lfHeight = -S(13); bodyFont = CreateFontIndirectW(&face);
  face.lfHeight = -S(21); face.lfWeight = FW_SEMIBOLD; titleFont = CreateFontIndirectW(&face);
  WNDCLASSW cls{}; cls.lpfnWndProc = Proc; cls.hInstance = instance; cls.hCursor = LoadCursor(nullptr, IDC_ARROW); cls.hIcon = LoadIcon(instance, MAKEINTRESOURCE(1)); cls.lpszClassName = L"X20CTLWebSetup"; cls.hbrBackground = backBrush;
  RegisterClassW(&cls);
  RECT frame{0, 0, S(440), S(168)}; AdjustWindowRect(&frame, WS_CAPTION | WS_SYSMENU, FALSE);
  int w = frame.right - frame.left, h = frame.bottom - frame.top;
  window = CreateWindowExW(0, cls.lpszClassName, L"X20CTL setup", WS_CAPTION | WS_SYSMENU, (GetSystemMetrics(SM_CXSCREEN) - w) / 2, (GetSystemMetrics(SM_CYSCREEN) - h) / 2, w, h, nullptr, nullptr, instance, nullptr);
  title = CreateWindowW(L"STATIC", (std::wstring(L"X20CTL ") + X20_NAME).c_str(), WS_CHILD | WS_VISIBLE, S(28), S(24), S(384), S(30), window, nullptr, instance, nullptr);
  status = CreateWindowW(L"STATIC", L"Finding the release…", WS_CHILD | WS_VISIBLE, S(28), S(64), S(384), S(20), window, nullptr, instance, nullptr);
  bar = CreateWindowW(PROGRESS_CLASS, nullptr, WS_CHILD | WS_VISIBLE | PBS_SMOOTH, S(28), S(94), S(384), S(8), window, nullptr, instance, nullptr);
  SetWindowTheme(bar, L"", L""); SendMessageW(bar, PBM_SETBARCOLOR, 0, kBar); SendMessageW(bar, PBM_SETBKCOLOR, 0, kTrack); SendMessageW(bar, PBM_SETRANGE32, 0, 1000);
  cancel = CreateWindowW(L"BUTTON", L"Cancel", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, S(322), S(122), S(90), S(28), window, nullptr, instance, nullptr);
  SendMessageW(title, WM_SETFONT, (WPARAM)titleFont, TRUE); for (HWND c : {status, cancel}) SendMessageW(c, WM_SETFONT, (WPARAM)bodyFont, TRUE);
  ShowWindow(window, SW_SHOW); SetTimer(window, 1, 100, nullptr);
  std::thread worker(Work);
  MSG message{}; int code = 0;
  while (GetMessageW(&message, nullptr, 0, 0) > 0) { TranslateMessage(&message); DispatchMessageW(&message); }
  code = (int)message.wParam;
  cancelRequested = true; worker.join();
  return code;
}
