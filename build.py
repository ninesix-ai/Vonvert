# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2026 ninesix-ai studio

"""
Vonvert — Open-Source Build Script

Builds the Vonvert application (Engine + App).

Usage:
    build.py              Build (publish self-contained EXE)
    build.py --sign       Build + sign with code-signing certificate
    build.py --package    Build + create the NSIS installer
    build.py --clean      Clean then build
    build.py --clean-only Clean and exit
    build.py --no-pause   Build without waiting at end
"""

import argparse
import os
import shutil
import subprocess
import sys
import time
import webbrowser

# ── ANSI colors (Windows 10+) ────────────────────────────────
if sys.platform == "win32":
    os.system("")

CYAN   = "\033[36m"
YELLOW = "\033[33m"
GREEN  = "\033[32m"
RED    = "\033[31m"
GRAY   = "\033[90m"
RESET  = "\033[0m"
BOLD   = "\033[1m"

ROOT = os.path.dirname(os.path.abspath(__file__))
SLN  = os.path.join(ROOT, "Vonvert.OSS.sln")

PROJECT_DIRS = [
    os.path.join(ROOT, "Vonvert.Engine"),
    os.path.join(ROOT, "Vonvert.App"),
    os.path.join(ROOT, "test", "code", "Vonvert.Tests"),
]


def cprint(msg, color=RESET):
    print(f"{color}{msg}{RESET}")


def header(title):
    cprint(f"\n{'═' * 60}", CYAN)
    cprint(f"  {title}", CYAN)
    cprint(f"{'═' * 60}", CYAN)


def run(cmd, **kwargs):
    return subprocess.run(cmd, **kwargs).returncode


# ── Read version from Vonvert.App.csproj ──────────────────────
def _read_version() -> str:
    import re
    csproj = os.path.join(ROOT, "Vonvert.App", "Vonvert.App.csproj")
    try:
        with open(csproj, encoding="utf-8") as f:
            m = re.search(r"<Version>(\d+\.\d+\.\d+)</Version>", f.read())
            if m:
                return m.group(1)
    except Exception:
        pass
    # Version lives in Directory.Build.props (shared across all projects).
    props = os.path.join(ROOT, "Directory.Build.props")
    try:
        with open(props, encoding="utf-8") as f:
            m = re.search(r"<Version>(\d+\.\d+\.\d+)</Version>", f.read())
            if m:
                return m.group(1)
    except Exception:
        pass
    return "0.0.0"


# ── Clean ────────────────────────────────────────────────────
def clean():
    header("Clean")
    removed = 0
    for proj_dir in PROJECT_DIRS:
        for d in ["bin", "obj"]:
            p = os.path.join(proj_dir, d)
            if os.path.isdir(p):
                shutil.rmtree(p, ignore_errors=True)
                cprint(f"  Removed {os.path.relpath(p, ROOT)}/", GRAY)
                removed += 1
    cprint(f"\n  Cleaned {removed} directories.", GREEN)


# ── Restore ──────────────────────────────────────────────────
def restore():
    header("Restore")
    cprint("  dotnet restore Vonvert.OSS.sln -r win-x64", GRAY)
    if run(["dotnet", "restore", SLN, "-r", "win-x64"]) != 0:
        cprint("  FAILED: dotnet restore", RED)
        return False
    cprint("  OK", GREEN)
    return True


# ── Build ────────────────────────────────────────────────────
def build():
    header("Build")
    version = _read_version()
    cprint(f"  Version: {version}", GRAY)
    cprint(f"  Solution: Vonvert.OSS.sln", GRAY)
    cprint("")

    if run(["dotnet", "build", SLN, "-c", "Release", "--no-restore"]) != 0:
        cprint("\n  FAILED: dotnet build", RED)
        return False
    cprint("\n  Build succeeded.", GREEN)
    return True


# ── Publish ──────────────────────────────────────────────────
def publish():
    header("Publish")
    version = _read_version()
    publish_dir = os.path.join(ROOT, "Vonvert.App", "bin", "publish")

    cprint(f"  Publishing Vonvert v{version} (self-contained)...", GRAY)

    if run([
        "dotnet", "publish",
        os.path.join(ROOT, "Vonvert.App", "Vonvert.App.csproj"),
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-o", publish_dir,
    ]) != 0:
        cprint("  FAILED: dotnet publish", RED)
        return False

    exe = os.path.join(publish_dir, "Vonvert.exe")
    if os.path.isfile(exe):
        size_mb = os.path.getsize(exe) / (1024 * 1024)
        cprint(f"\n  OK: Vonvert.exe ({size_mb:.1f} MB)", GREEN)
        cprint(f"  Output: {os.path.relpath(publish_dir, ROOT)}\\", GRAY)

        # Unblock exe — remove Zone.Identifier ADS (equivalent to PowerShell Unblock-File)
        # Prevents Windows SmartScreen from blocking the unsigned exe on launch.
        zone_id = exe + ":Zone.Identifier"
        try:
            os.remove(zone_id)
            cprint("  Unblocked (Zone.Identifier removed)", GRAY)
        except FileNotFoundError:
            pass  # already unblocked
    return True


# ── NSIS installer (packaging) ───────────────────────────────
def check_nsis() -> str:
    found = shutil.which("makensis")
    if found:
        return found
    for p in [
        r"C:\Program Files (x86)\NSIS\makensis.exe",
        r"C:\Program Files\NSIS\makensis.exe",
    ]:
        if os.path.isfile(p):
            return p
    cprint("  MISSING: NSIS not installed.", RED)
    webbrowser.open("https://nsis.sourceforge.io/Download")
    return ""


def build_installer(publish_dir: str) -> bool:
    header("Package (NSIS)")
    nsis = check_nsis()
    if not nsis:
        return False
    out_dir = os.path.join(ROOT, "installer", "Output")
    os.makedirs(out_dir, exist_ok=True)
    version = _read_version()
    cprint(f"  makensis /DBUILD_DIR={publish_dir} /DAPP_VERSION={version}", GRAY)
    rc = run([
        nsis,
        f"/DBUILD_DIR={publish_dir}",
        f"/DAPP_VERSION={version}",
        os.path.join(ROOT, "installer", "setup.oss.nsi"),
    ])
    setup = os.path.join(out_dir, "Vonvert_Setup.exe")
    if rc == 0 and os.path.isfile(setup):
        size_mb = os.path.getsize(setup) / (1024 * 1024)
        cprint(f"\n  OK: {os.path.relpath(setup, ROOT)} ({size_mb:.1f} MB)", GREEN)
        return True
    cprint("  FAILED: makensis", RED)
    return False


# ── Find signtool.exe ────────────────────────────────────────
def find_signtool() -> str:
    sdk_base = r"C:\Program Files (x86)\Windows Kits\10\bin"
    if os.path.isdir(sdk_base):
        versions = sorted(
            [d for d in os.listdir(sdk_base) if os.path.isdir(os.path.join(sdk_base, d))],
            reverse=True,
        )
        for ver in versions:
            candidate = os.path.join(sdk_base, ver, "x64", "signtool.exe")
            if os.path.isfile(candidate):
                return candidate
    # fallback to PATH
    for d in os.environ.get("PATH", "").split(os.pathsep):
        p = os.path.join(d, "signtool.exe")
        if os.path.isfile(p):
            return p
    return ""


# ── Sign ─────────────────────────────────────────────────────
def _find_pfx() -> str:
    """Return the first existing Vonvert dev .pfx among likely locations."""
    candidates = [
        os.path.join(ROOT, "docs", "vonvert-dev.pfx"),
        os.path.join(ROOT, "..", "docs", "vonvert-dev.pfx"),
    ]
    for c in candidates:
        if os.path.isfile(c):
            return os.path.normpath(c)
    return ""


def sign(publish_dir: str) -> bool:
    header("Sign")

    signtool = find_signtool()
    if not signtool:
        cprint("  ERROR: signtool.exe not found.", RED)
        cprint("  Install Windows SDK: https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/", GRAY)
        webbrowser.open("https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/")
        return False

    exe_path = os.path.join(publish_dir, "Vonvert.exe")
    if not os.path.isfile(exe_path):
        cprint(f"  ERROR: Vonvert.exe not found at: {exe_path}", RED)
        return False

    common = [signtool, "sign", "/fd", "SHA256",
              "/tr", "http://timestamp.digicert.com", "/td", "SHA256"]

    # 1) Explicit cert thumbprint already installed in the Windows cert store.
    thumb = os.environ.get("VONVERT_CERT_THUMBPRINT", "").strip()
    if thumb:
        cprint(f"  Signing with store cert thumbprint: {thumb}", GRAY)
        if run(common + ["/sha1", thumb, exe_path]) == 0:
            cprint("  OK: Vonvert.exe signed", GREEN)
            return True
        cprint("  Thumbprint signing failed; trying .pfx / store fallback.", YELLOW)

    # 2) .pfx file + password (the historical OSS flow).
    pfx_path = _find_pfx()
    pfx_password = os.environ.get("VONVERT_PFX_PASSWORD", "")
    if pfx_path and pfx_password:
        cprint(f"  Signing with PFX: {os.path.relpath(pfx_path, ROOT)}", GRAY)
        if run(common + ["/f", pfx_path, "/p", pfx_password, exe_path]) == 0:
            cprint("  OK: Vonvert.exe signed", GREEN)
            return True
        cprint("  ERROR: Code signing failed.", RED)
        return False

    # 3) Fallback: a 'Vonvert' code-signing cert already trusted in the cert
    #    store on this machine. No password required — SmartScreen clears
    #    because the cert is in Root.
    for store_flag, label in (("/sm", "LocalMachine"), ("", "CurrentUser")):
        args = common + ([store_flag] if store_flag else []) + ["/n", "Vonvert", exe_path]
        if run(args) == 0:
            cprint(f"  OK: Vonvert.exe signed with store cert ({label})", GREEN)
            return True

    cprint("  No signing certificate found.", YELLOW)
    cprint("  Provide one of:", GRAY)
    cprint("    * env VONVERT_CERT_THUMBPRINT = <thumbprint of a Vonvert cert>", GRAY)
    cprint("    * docs/vonvert-dev.pfx + env VONVERT_PFX_PASSWORD = <password>", GRAY)
    cprint("    * a 'Vonvert' code-signing cert installed in the cert store", GRAY)
    return False


# ── Main ─────────────────────────────────────────────────────
def main():
    parser = argparse.ArgumentParser(
        description="Vonvert Build Script",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  build.py                  Build and publish
  build.py --sign           Build + sign (requires VONVERT_PFX_PASSWORD)
  build.py --package        Build + create the NSIS installer
  build.py --clean          Clean then build
  build.py --clean-only     Clean and exit
  build.py --no-pause       Build without waiting at end
""")
    parser.add_argument("--clean", "-c", action="store_true",
                        help="Clean build artifacts before building")
    parser.add_argument("--clean-only", "-co", action="store_true",
                        help="Clean and exit (no build)")
    parser.add_argument("--sign", "-s", action="store_true",
                        help="Sign Vonvert.exe with code-signing certificate")
    parser.add_argument("--package", "-p", action="store_true",
                        help="Also build the NSIS installer after publishing")
    parser.add_argument("--no-pause", "-n", action="store_true",
                        help="Don't pause at the end")

    args = parser.parse_args()
    start = time.time()

    header("Vonvert Build")
    cprint(f"  Root:    {ROOT}", GRAY)
    if args.sign:
        cprint(f"  Sign:    enabled (auto Vonvert cert / PFX)", GRAY)

    if args.clean or args.clean_only:
        clean()
        if args.clean_only:
            return

    ok = True
    ok = restore() and ok
    if ok:
        ok = build() and ok
    if ok:
        ok = publish() and ok
    publish_dir = os.path.join(ROOT, "Vonvert.App", "bin", "publish")
    if ok and args.sign:
        ok = sign(publish_dir)
    if ok and args.package:
        ok = build_installer(publish_dir) and ok

    elapsed = time.time() - start

    if ok:
        header("BUILD COMPLETE")
        cprint(f"  Time: {elapsed:.1f}s", GREEN)
    else:
        header("BUILD FAILED")
        cprint(f"  Time: {elapsed:.1f}s", RED)

    if not args.no_pause:
        print()
        os.system("pause >nul 2>&1")

    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
