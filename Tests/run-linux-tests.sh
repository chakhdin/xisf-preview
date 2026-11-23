#!/bin/bash
# Runs the test suite under Mono with a hard timeout, skipping the WindowsOnly (WinForms UI) tests.
# Build first: dotnet build Tests/XisfPreview.Tests.csproj -p:SignAssembly=false -p:SkipKillSurrogates=true
# XUNIT_CONSOLE points at xunit.console.exe (net48); the Sandcastle image sets it.
set -e
cd "$(dirname "$0")"
timeout -k 10 300 mono "${XUNIT_CONSOLE:?XUNIT_CONSOLE is not set}" bin/Debug/net48/XisfPreview.Tests.dll \
  -noshadow -notrait Category=WindowsOnly
