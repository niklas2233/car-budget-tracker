; Custom NSIS include for CarBudget installer

; ─────────────────────────────────────────────────────────────────
; customInstallMode — called inside the "me vs all users" page's own
; pre-function.  Setting $isForceCurrentInstall / $isForceMachineInstall
; to "1" causes that pre-function to call Abort, skipping the page.
; On upgrades we preserve whichever mode was used previously.
; ─────────────────────────────────────────────────────────────────
!macro customInstallMode
  ReadRegDWORD $0 HKCU "Software\CarBudget_Upgrade" "IsUpgrade"
  StrCmp $0 "1" 0 CB_InstMode_done
    StrCmp $hasPerMachineInstallation "1" 0 CB_ForceUser
      StrCpy $isForceMachineInstall "1"
      Goto CB_InstMode_done
    CB_ForceUser:
      StrCpy $isForceCurrentInstall "1"
  CB_InstMode_done:
!macroend

; ─────────────────────────────────────────────────────────────────
; preInit — runs inside .onInit before any pages are shown.
; ─────────────────────────────────────────────────────────────────
!macro preInit
  ReadRegStr $0 HKCU "${INSTALL_REGISTRY_KEY}" InstallLocation
  ${If} $0 != ""
    WriteRegDWORD HKCU "Software\CarBudget_Upgrade" "IsUpgrade" 1

    ${If} ${FileExists} "$DESKTOP\CarBudget.lnk"
      WriteRegDWORD HKCU "Software\CarBudget_Upgrade" "RestoreDesktop" 1
    ${Else}
      WriteRegDWORD HKCU "Software\CarBudget_Upgrade" "RestoreDesktop" 0
    ${EndIf}

    ${If} ${FileExists} "$SMPROGRAMS\CarBudget\CarBudget.lnk"
      WriteRegDWORD HKCU "Software\CarBudget_Upgrade" "RestoreStartMenu" 1
    ${Else}
      WriteRegDWORD HKCU "Software\CarBudget_Upgrade" "RestoreStartMenu" 0
    ${EndIf}
  ${Else}
    DeleteRegKey HKCU "Software\CarBudget_Upgrade"
  ${EndIf}

  SetRegView 64
  ReadRegStr $0 HKCU "${INSTALL_REGISTRY_KEY}" InstallLocation
  ${If} $0 == ""
    WriteRegStr HKCU "${INSTALL_REGISTRY_KEY}" InstallLocation "$LOCALAPPDATA\CarBudget"
  ${EndIf}
  SetRegView 32
  ReadRegStr $0 HKCU "${INSTALL_REGISTRY_KEY}" InstallLocation
  ${If} $0 == ""
    WriteRegStr HKCU "${INSTALL_REGISTRY_KEY}" InstallLocation "$LOCALAPPDATA\CarBudget"
  ${EndIf}
!macroend

!ifndef BUILD_UNINSTALLER

!include nsDialogs.nsh

Var DesktopShortcutCB
Var StartMenuShortcutCB
Var DesktopShortcutState
Var StartMenuShortcutState

Function ShortcutOptionsCreate
  ReadRegDWORD $0 HKCU "Software\CarBudget_Upgrade" "IsUpgrade"
  ${If} $0 == 1
    Abort
  ${EndIf}

  nsDialogs::Create 1018
  Pop $0

  ${NSD_CreateLabel} 0 0 100% 12u "Select shortcuts to create:"
  Pop $0

  ${NSD_CreateCheckbox} 0 20u 100% 12u "Create Desktop Shortcut"
  Pop $DesktopShortcutCB
  ${NSD_SetState} $DesktopShortcutCB ${BST_CHECKED}

  ${NSD_CreateCheckbox} 0 38u 100% 12u "Create Start Menu Shortcut"
  Pop $StartMenuShortcutCB
  ${NSD_SetState} $StartMenuShortcutCB ${BST_CHECKED}

  nsDialogs::Show
FunctionEnd

Function ShortcutOptionsLeave
  ${NSD_GetState} $DesktopShortcutCB $DesktopShortcutState
  ${NSD_GetState} $StartMenuShortcutCB $StartMenuShortcutState
FunctionEnd

!macro customPageAfterChangeDir
  Page custom ShortcutOptionsCreate ShortcutOptionsLeave
!macroend

!macro customInstall
  ReadRegDWORD $0 HKCU "Software\CarBudget_Upgrade" "IsUpgrade"

  ${If} $0 == 1
    ReadRegDWORD $0 HKCU "Software\CarBudget_Upgrade" "RestoreDesktop"
    ${If} $0 == 1
      CreateShortcut "$DESKTOP\CarBudget.lnk" "$INSTDIR\CarBudget.exe" "" "$INSTDIR\resources\favicon.ico" 0
    ${EndIf}
    ReadRegDWORD $0 HKCU "Software\CarBudget_Upgrade" "RestoreStartMenu"
    ${If} $0 == 1
      CreateDirectory "$SMPROGRAMS\CarBudget"
      CreateShortcut "$SMPROGRAMS\CarBudget\CarBudget.lnk" "$INSTDIR\CarBudget.exe" "" "$INSTDIR\resources\favicon.ico" 0
    ${EndIf}
  ${Else}
    ${If} $DesktopShortcutState == 1
      CreateShortcut "$DESKTOP\CarBudget.lnk" "$INSTDIR\CarBudget.exe" "" "$INSTDIR\resources\favicon.ico" 0
    ${EndIf}
    ${If} $StartMenuShortcutState == 1
      CreateDirectory "$SMPROGRAMS\CarBudget"
      CreateShortcut "$SMPROGRAMS\CarBudget\CarBudget.lnk" "$INSTDIR\CarBudget.exe" "" "$INSTDIR\resources\favicon.ico" 0
    ${EndIf}
  ${EndIf}

  DeleteRegKey HKCU "Software\CarBudget_Upgrade"
!macroend

!endif ; BUILD_UNINSTALLER

!macro customUnInstall
  Delete "$DESKTOP\CarBudget.lnk"
  Delete "$SMPROGRAMS\CarBudget\CarBudget.lnk"
  RMDir  "$SMPROGRAMS\CarBudget"
  ; CarBudget_Upgrade intentionally NOT deleted here.
!macroend
