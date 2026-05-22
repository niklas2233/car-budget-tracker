; Custom NSIS include for CarBudget installer

; Override default per-user install path to LocalAppData\CarBudget
; (electron-builder defaults to LocalAppData\Programs\CarBudget)
; Only sets it when no previous installation path exists in the registry.
!macro preInit
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
  ${If} $DesktopShortcutState == ${BST_CHECKED}
    CreateShortcut "$DESKTOP\CarBudget.lnk" "$INSTDIR\CarBudget.exe" "" "$INSTDIR\resources\favicon.ico" 0
  ${EndIf}

  ${If} $StartMenuShortcutState == ${BST_CHECKED}
    CreateDirectory "$SMPROGRAMS\CarBudget"
    CreateShortcut "$SMPROGRAMS\CarBudget\CarBudget.lnk" "$INSTDIR\CarBudget.exe" "" "$INSTDIR\resources\favicon.ico" 0
  ${EndIf}
!macroend

!endif ; BUILD_UNINSTALLER

!macro customUnInstall
  Delete "$DESKTOP\CarBudget.lnk"
  Delete "$SMPROGRAMS\CarBudget\CarBudget.lnk"
  RMDir "$SMPROGRAMS\CarBudget"
!macroend
