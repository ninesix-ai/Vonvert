@rem SPDX-License-Identifier: Apache-2.0
@echo off
:: Vonvert — One-click build entry point
:: Builds the Vonvert application.
::
:: Usage:
::   build.bat              Build and publish
::   build.bat --sign       Build + sign (requires VONVERT_PFX_PASSWORD)
::   build.bat --clean      Clean then build
::   build.bat --clean-only Clean and exit
::   build.bat -c           Short alias for --clean
::   build.bat -co          Short alias for --clean-only
::   build.bat -s           Short alias for --sign
::   build.bat --no-pause   Build without waiting at end
::   build.bat -n           Short alias for --no-pause

setlocal
set "ROOT=%~dp0"
set PYTHONUNBUFFERED=1

where python >nul 2>&1
if %ERRORLEVEL% equ 0 ( python "%ROOT%build.py" %* & goto :done )
where python3 >nul 2>&1
if %ERRORLEVEL% equ 0 ( python3 "%ROOT%build.py" %* & goto :done )

for %%P in (
    "%LOCALAPPDATA%\Programs\Python\Python312\python.exe"
    "%LOCALAPPDATA%\Programs\Python\Python311\python.exe"
    "%LOCALAPPDATA%\Programs\Python\Python310\python.exe"
    "C:\Python312\python.exe"
    "C:\Python311\python.exe"
    "C:\Python310\python.exe"
) do ( if exist %%P ( %%P "%ROOT%build.py" %* & goto :done ) )

echo.
echo  ERROR: Python not found!
echo  Install Python 3.10+ from https://www.python.org/downloads/
echo.
pause
exit /b 1

:done
exit /b %ERRORLEVEL%
