; SPDX-License-Identifier: Apache-2.0
; Copyright (c) 2026 ninesix-ai studio
;
; Vonvert OSS Edition Installer (NSIS)
; User-level install (no admin required). Registers Start Menu / Desktop shortcuts
; and an Add/Remove Programs (Control Panel) uninstall entry.
;
; Local build:  makensis installer\setup.oss.nsi
; CI build:     makensis /DBUILD_DIR=..\publish /DAPP_VERSION=0.0.1 installer\setup.oss.nsi
;               (BUILD_DIR is relative to this script's directory; APP_VERSION should be
;                kept in sync with Directory.Build.props <Version>.)
;
; Encoding note: keep this file pure ASCII. Chinese UI text below uses NSIS
; ${UTF8} byte escapes so the script compiles identically under any code page.

!include "MUI2.nsh"

; -- App Metadata (overridable via /D<name>=<value>) --------------------------
!ifndef APP_NAME
  !define APP_NAME "Vonvert"
!endif
!ifndef APP_DISPLAY
  !define APP_DISPLAY "Vonvert"
!endif
!ifndef APP_VERSION
  !define APP_VERSION "0.0.1"
!endif
!ifndef APP_PUBLISHER
  !define APP_PUBLISHER "ninesix-ai studio"
!endif
!ifndef APP_URL
  !define APP_URL "https://github.com/ninesix-ai/Vonvert"
!endif
!ifndef APP_EXE
  !define APP_EXE "Vonvert.exe"
!endif
!ifndef BUILD_DIR
  ; Default matches the local layout produced by: build.bat (publishes to Vonvert.App\bin\publish)
  !define BUILD_DIR "..\Vonvert.App\bin\publish"
!endif

; -- NSIS Configuration --------------------------------------------------------
Name "${APP_DISPLAY} ${APP_VERSION}"
OutFile "Output\Vonvert_Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\Vonvert"
InstallDirRegKey HKCU "Software\Vonvert" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
Unicode true

; -- MUI Configuration ---------------------------------------------------------
!define MUI_ICON "..\Vonvert.App\Assets\icon.ico"
!define MUI_UNICON "..\Vonvert.App\Assets\icon.ico"
!define MUI_ABORTWARNING

; -- Pages ---------------------------------------------------------------------
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; -- Languages -----------------------------------------------------------------
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "SimpChinese"

; -- Version Information -------------------------------------------------------
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "${APP_DISPLAY}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "Copyright (C) 2026 ninesix-ai studio"
VIAddVersionKey "FileDescription" "${APP_DISPLAY} Installer"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"

; -- Installer Sections --------------------------------------------------------
Section "!${APP_DISPLAY} (required)" SecApp
    SectionIn RO

    SetOutPath "$INSTDIR"
    File /r "${BUILD_DIR}\*.*"

    WriteUninstaller "$INSTDIR\uninstall.exe"

    WriteRegStr HKCU "Software\Vonvert" "" $INSTDIR
    WriteRegStr HKCU "Software\Vonvert" "InstallDir" $INSTDIR

    ; Add/Remove Programs entry (Control Panel uninstall)
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
                     "DisplayName" "${APP_DISPLAY}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
                     "UninstallString" '"$INSTDIR\uninstall.exe"'
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
                     "DisplayIcon" '"$INSTDIR\${APP_EXE}"'
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
                     "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
                     "Publisher" "${APP_PUBLISHER}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
                     "URLInfoAbout" "${APP_URL}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
                     "InstallLocation" "$INSTDIR"
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
                      "NoModify" 1
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" \
                      "NoRepair" 1
SectionEnd

; -- Start Menu Shortcuts ------------------------------------------------------
Section "Start Menu Shortcuts" SecStartMenu
    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
    CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\uninstall.exe"
SectionEnd

; -- Desktop Shortcut ----------------------------------------------------------
Section "Desktop Shortcut" SecDesktop
    CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
SectionEnd

; -- Uninstaller Section -------------------------------------------------------
Section "Uninstall"
    Delete "$DESKTOP\${APP_NAME}.lnk"
    RMDir /r "$SMPROGRAMS\${APP_NAME}"

    DeleteRegKey HKCU "Software\${APP_NAME}"
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

    RMDir /r "$INSTDIR"
SectionEnd

; -- Section Descriptions ------------------------------------------------------
LangString DESC_SecApp ${LANG_ENGLISH} "Install ${APP_DISPLAY} application files (required)."
LangString DESC_SecApp ${LANG_SIMPCHINESE} "\xe5\xae\x89\xe8\xa3\x85 ${APP_DISPLAY} \xe7\xa8\x8b\xe5\xba\x8f\xe6\x96\x87\xe4\xbb\xb6\xef\xbc\x88\xe5\xbf\x85\xe9\x9c\x80\xef\xbc\x89\xe3\x80\x82"
LangString DESC_SecStartMenu ${LANG_ENGLISH} "Create Start Menu shortcuts for ${APP_NAME}."
LangString DESC_SecStartMenu ${LANG_SIMPCHINESE} "\xe5\x88\x9b\xe5\xbb\xba ${APP_NAME} \xe5\xbc\x80\xe5\xa7\x8b\xe8\x8f\x9c\xe5\x8d\x95\xe5\xbf\xab\xe6\x8d\xb7\xe6\x96\xb9\xe5\xbc\x8f\xe3\x80\x82"
LangString DESC_SecDesktop ${LANG_ENGLISH} "Create a desktop shortcut for ${APP_NAME}."
LangString DESC_SecDesktop ${LANG_SIMPCHINESE} "\xe5\x88\x9b\xe5\xbb\xba ${APP_NAME} \xe6\xa1\x8c\xe9\x9d\xa2\xe5\xbf\xab\xe6\x8d\xb7\xe6\x96\xb9\xe5\xbc\x8f\xe3\x80\x82"

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SecApp} $(DESC_SecApp)
    !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu} $(DESC_SecStartMenu)
    !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} $(DESC_SecDesktop)
!insertmacro MUI_FUNCTION_DESCRIPTION_END
