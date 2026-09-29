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
import glob
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

        # Unblock the produced binaries - remove Zone.Identifier ADS (equivalent to
        # PowerShell Unblock-File). The managed assembly matters as much as the
        # apphost: a policy-blocked Vonvert.dll aborts the process during load.
        unblocked = _unblock(exe)
        for dll in glob.glob(os.path.join(publish_dir, "Vonvert*.dll")):
            unblocked += _unblock(dll)
        if unblocked:
            cprint(f"  Unblocked {unblocked} file(s) (Zone.Identifier removed)", GRAY)
    return True


# ── Unblock / launch smoke test ──────────────────────────────
def _unblock(path: str) -> int:
    """Strip a file's Zone.Identifier stream. Returns 1 when one was removed."""
    try:
        os.remove(path + ":Zone.Identifier")
        return 1
    except OSError:
        return 0


# Exit code for "process died from an unhandled managed exception", which for a
# launch this early means the runtime refused to load our own assembly. Windows
# reports exit codes as an unsigned DWORD here, so compare the masked value.
MANAGED_EXCEPTION_EXIT = 0xE0434352


def verify_launch(publish_dir: str, wait_s: float = 6.0) -> bool:
    """
    Start the published exe and leave the window open for manual testing.

    A blocked or corrupt self-contained output fails before any log file is
    written, so the only symptom is a double-click that appears to do nothing.
    We wait briefly to catch that instant crash, then deliberately hand the
    still-open window back to the user to close by hand when done.
    """
    header("Launch check")
    exe = os.path.join(publish_dir, "Vonvert.exe")
    if not os.path.isfile(exe):
        cprint("  SKIP: Vonvert.exe not found", YELLOW)
        return True
    if os.environ.get("CI"):
        cprint("  SKIP: CI environment (no interactive desktop)", GRAY)
        return True

    try:
        proc = subprocess.Popen([exe], cwd=publish_dir)
    except OSError as exc:
        cprint(f"  FAILED: could not start Vonvert.exe: {exc}", RED)
        return False

    time.sleep(wait_s)
    rc = proc.poll()
    if rc is not None and (rc & 0xFFFFFFFF) == MANAGED_EXCEPTION_EXIT:
        # Observed in practice: a freshly written unsigned binary can be refused
        # on first sight and allowed again minutes later, so retry before
        # declaring the build unusable.
        for attempt in (2, 3):
            time.sleep(5)
            print(f"  retry {attempt - 1} of 2 ...", flush=True)
            proc = subprocess.Popen([exe], cwd=publish_dir)
            time.sleep(wait_s)
            rc = proc.poll()
            if rc is None:
                cprint("  RETRY OK: launched after a refused first attempt.", YELLOW)
                cprint("  A machine policy can block a just-written unsigned binary", YELLOW)
                cprint("  until it has been evaluated; sign the output (--sign) to", YELLOW)
                cprint("  make first launch deterministic.", YELLOW)
                cprint(f"  Window left open (PID {proc.pid}); close it when done.", GREEN)
                return True
    if rc is None:
        cprint(f"  OK: window is up (PID {proc.pid}).", GREEN)
        cprint("  Left running on purpose for you to test - close it manually when done.", GRAY)
        return True
    if rc == 0:
        # A clean exit right away, with the window deliberately left open by a
        # previous run, means the single-instance mutex made this second launch
        # quit silently. That is expected, not a build failure.
        cprint("  NOTE: exited immediately with code 0 - an instance is already", YELLOW)
        cprint("  running (single-instance lock). Close the old window first if you", YELLOW)
        cprint("  meant to start a fresh one.", YELLOW)
        return True

    cprint(f"  FAILED: exited right away, code {rc} (0x{rc & 0xFFFFFFFF:08X})", RED)
    if (rc & 0xFFFFFFFF) == MANAGED_EXCEPTION_EXIT:
        cprint("  This means an unhandled managed exception while loading the", RED)
        cprint("  application - the UI never had a chance to appear, and no app", RED)
        cprint("  log is written. Look at the Windows Application event log", RED)
        cprint("  (source '.NET Runtime') for the exact reason. The usual one is", RED)
        cprint("  an application-control / antivirus verdict on this unsigned", RED)
        cprint("  self-contained binary:", RED)
        cprint("    * retry in a minute, or run the framework-dependent build", RED)
        cprint("      (Vonvert.App\\bin\\Release\\...\\Vonvert.exe) to confirm", RED)
        cprint("    * sign the output: python build.py --sign", RED)
    return False


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


def _sign_targets(publish_dir: str) -> list:
    """
    Every payload file a policy verdict can land on, apphost first.

    The apphost alone is not enough: an application-control policy evaluates
    the managed assembly on load, so an unsigned Vonvert.dll aborts the
    process with 0x800711C7 even when the signed exe started fine.
    """
    targets = [os.path.join(publish_dir, "Vonvert.exe")]
    targets += sorted(glob.glob(os.path.join(publish_dir, "Vonvert*.dll")))
    return [t for t in targets if os.path.isfile(t)]


def sign(publish_dir: str) -> bool:
    header("Sign")

    signtool = find_signtool()
    if not signtool:
        cprint("  ERROR: signtool.exe not found.", RED)
        cprint("  Install Windows SDK: https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/", GRAY)
        webbrowser.open("https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/")
        return False

    targets = _sign_targets(publish_dir)
    if not targets or os.path.basename(targets[0]) != "Vonvert.exe":
        cprint(f"  ERROR: Vonvert.exe not found at: {publish_dir}", RED)
        return False
    cprint(f"  Signing {len(targets)} file(s): "
           + ", ".join(os.path.basename(t) for t in targets), GRAY)

    common = [signtool, "sign", "/fd", "SHA256",
              "/tr", "http://timestamp.digicert.com", "/td", "SHA256"]

    def sign_all(cert_args) -> bool:
        for t in targets:
            if run(common + cert_args + [t]) != 0:
                cprint(f"  FAILED: {os.path.basename(t)}", RED)
                return False
        return True

    # 1) Explicit cert thumbprint already installed in the Windows cert store.
    thumb = os.environ.get("VONVERT_CERT_THUMBPRINT", "").strip()
    if thumb:
        cprint(f"  Signing with store cert thumbprint: {thumb}", GRAY)
        if sign_all(["/sha1", thumb]):
            cprint("  OK: payloads signed", GREEN)
            return True
        cprint("  Thumbprint signing failed; trying .pfx / store fallback.", YELLOW)

    # 2) .pfx file + password (the historical OSS flow).
    pfx_path = _find_pfx()
    pfx_password = os.environ.get("VONVERT_PFX_PASSWORD", "")
    if pfx_path and pfx_password:
        cprint(f"  Signing with PFX: {os.path.relpath(pfx_path, ROOT)}", GRAY)
        if sign_all(["/f", pfx_path, "/p", pfx_password]):
            cprint("  OK: payloads signed", GREEN)
            return True
        cprint("  ERROR: Code signing failed.", RED)
        return False

    # 3) Fallback: a 'Vonvert' code-signing cert already trusted in the cert
    #    store on this machine. No password required — SmartScreen clears
    #    because the cert is in Root.
    for store_flag, label in (("/sm", "LocalMachine"), ("", "CurrentUser")):
        cert_args = ([store_flag] if store_flag else []) + ["/n", "Vonvert"]
        if sign_all(cert_args):
            cprint(f"  OK: payloads signed with store cert ({label})", GREEN)
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
    parser.add_argument("--no-verify", "-nv", action="store_true",
                        help="Skip the post-publish launch check")
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
    # The launch check is diagnostic, not a gate: a transient policy verdict on a
    # just-written unsigned binary must not stop us from producing the installer.
    # Its verdict is folded into the exit status after packaging has run.
    published = ok
    if ok and args.sign:
        ok = sign(publish_dir)
    launch_ok = True
    if published and not args.no_verify:
        launch_ok = verify_launch(publish_dir)
    if ok and args.package:
        ok = build_installer(publish_dir) and ok
    if not launch_ok:
        ok = False

    elapsed = time.time() - start

    if ok:
        header("BUILD COMPLETE")
        cprint(f"  Time: {elapsed:.1f}s", GREEN)
    else:
        header("BUILD FAILED")
        cprint(f"  Time: {elapsed:.1f}s", RED)
        if not launch_ok:
            cprint("  Artifacts were still produced; only the launch check failed.", GRAY)

    if not args.no_pause:
        print()
        os.system("pause >nul 2>&1")

    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
