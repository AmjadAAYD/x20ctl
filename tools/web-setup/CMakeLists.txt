# The X20CTL web setup (a few hundred KB). Built by the release workflow and named X20CTL-<name>-windows-x64-setup.exe.
#   cmake -S tools/web-setup -B build -A x64 -DX20_NAME=V0.017 -DX20_TAG=v0.017 -DX20_VERSION=0.17.0
#   cmake --build build --config Release
# X20_BASE_URL and X20_TEST_NO_LAUNCH exist only for local tests (a local server, no wizard started); the release sets neither.
cmake_minimum_required(VERSION 3.20)
project(x20ctl-web-setup CXX RC)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded")  # no Visual C++ runtime to install first
set(X20_NAME "V0.000" CACHE STRING "Release name used in file names and shown in the window, e.g. V0.017")
set(X20_TAG "v0.000" CACHE STRING "Release tag the files are downloaded from, e.g. v0.017")
set(X20_VERSION "0.0.0" CACHE STRING "Numeric version for the file properties, e.g. 0.17.0")
set(X20_REPO "AmjadAAYD/x20ctl" CACHE STRING "GitHub repository that holds the release")
set(X20_BASE_URL "" CACHE STRING "Test only: a folder URL that replaces the release download address")
option(X20_TEST_NO_LAUNCH "Test only: stop after the checksum matches" OFF)
string(REPLACE "." "," X20_VERSION_COMMAS "${X20_VERSION}")
set(X20_VERSION_COMMAS "${X20_VERSION_COMMAS},0")
configure_file(setup_config.h.in setup_config.h @ONLY)
configure_file(setup.rc.in setup.rc @ONLY)
add_executable(x20ctl-web-setup WIN32 setup.cpp ${CMAKE_CURRENT_BINARY_DIR}/setup.rc)
target_include_directories(x20ctl-web-setup PRIVATE ${CMAKE_CURRENT_BINARY_DIR})
target_compile_options(x20ctl-web-setup PRIVATE /W4 /O1 /GL)
target_link_options(x20ctl-web-setup PRIVATE /LTCG /OPT:REF /OPT:ICF /MANIFEST:NO)  # setup.rc embeds the manifest
target_link_libraries(x20ctl-web-setup PRIVATE winhttp bcrypt comctl32 shell32 uxtheme ole32 user32 gdi32)
set_target_properties(x20ctl-web-setup PROPERTIES OUTPUT_NAME "x20ctl-web-setup")
