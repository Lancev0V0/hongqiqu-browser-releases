Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WordFunc.nsh"
!define PRODUCT "红旗渠爱国浏览器"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\HongqiquBrowser"
Name "${PRODUCT}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\HongqiquBrowser"
InstallDirRegKey HKCU "${UNINSTALL_KEY}" "InstallLocation"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
Icon "app\app.ico"
UninstallIcon "app\app.ico"
VIProductVersion "${APPVERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "${PRODUCT}"
VIAddVersionKey /LANG=2052 "FileDescription" "${PRODUCT} 安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${APPVERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "Hongqiqu Browser contributors"
!define MUI_ICON "app\app.ico"
!define MUI_UNICON "app\app.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "欢迎使用${PRODUCT}"
!define MUI_WELCOMEPAGE_TEXT "安装独立的浏览器和红旗渠主题。书签与个人设置单独保存。启动时检查 GitHub 更新：允许离线启动，一旦发现新版，必须完成更新后继续使用。"
!define MUI_FINISHPAGE_RUN "$INSTDIR\HongqiBrowser.exe"
!define MUI_FINISHPAGE_RUN_TEXT "启动${PRODUCT}"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

Function .onInit
  SetShellVarContext current
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "需要 64 位 Windows 10 或 Windows 11。"
    Abort
  ${EndIf}
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 528040
    MessageBox MB_ICONSTOP "需要 .NET Framework 4.8。请通过 Windows 更新安装后重试。"
    Abort
  ${EndIf}
  ReadRegStr $0 HKCU "${UNINSTALL_KEY}" "DisplayVersion"
  ${If} $0 != ""
    ${VersionCompare} $0 "${APPVERSION}" $1
    ${If} $1 == 1
      MessageBox MB_ICONSTOP "已安装的版本更新，不能降级。"
      Abort
    ${EndIf}
  ${EndIf}
  IfFileExists "$INSTDIR\HongqiBrowser.exe" 0 ready
  ExecWait '"$INSTDIR\HongqiBrowser.exe" --can-install' $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "请先关闭红旗渠爱国浏览器及更新窗口，再运行安装程序。"
    Abort
  ${EndIf}
  ready:
FunctionEnd

Section "浏览器" Main
  SetOutPath "$INSTDIR\versions\${APPVERSION}"
  File /r "${STAGE}\*.*"
  SetOutPath "$INSTDIR"
  File "${STAGE}\HongqiBrowser.exe"
  File "${STAGE}\app.ico"
  FileOpen $0 "$INSTDIR\current.txt" w
  FileWrite $0 "${APPVERSION}"
  FileClose $0
  FileOpen $0 "$INSTDIR\HongqiquBrowser.install-id" w
  FileWrite $0 "Lancie.HongqiquPatrioticBrowser"
  FileClose $0
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${APPVERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "Hongqiqu Browser contributors"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\app.ico,0"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "URLInfoAbout" "https://github.com/Lancev0V0/hongqiqu-browser-releases"
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  ExecWait '"$INSTDIR\HongqiBrowser.exe" --configure-shortcuts' $0
  ${If} $0 != 0
    MessageBox MB_ICONEXCLAMATION "安装完成，但创建快捷方式失败。请从安装目录启动 HongqiBrowser.exe。"
  ${EndIf}
SectionEnd

Function un.onInit
  SetShellVarContext current
  FileOpen $0 "$INSTDIR\HongqiquBrowser.install-id" r
  FileRead $0 $1
  FileClose $0
  ${If} $1 != "Lancie.HongqiquPatrioticBrowser"
    MessageBox MB_ICONSTOP "安装目录校验失败，已停止卸载。"
    Abort
  ${EndIf}
  ExecWait '"$INSTDIR\HongqiBrowser.exe" --can-install' $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "请先关闭红旗渠爱国浏览器及更新窗口，再卸载。"
    Abort
  ${EndIf}
FunctionEnd

Section "Uninstall"
  Delete "$DESKTOP\${PRODUCT}.lnk"
  Delete "$SMPROGRAMS\${PRODUCT}\${PRODUCT}.lnk"
  RMDir "$SMPROGRAMS\${PRODUCT}"
  RMDir /r "$INSTDIR\versions"
  Delete "$INSTDIR\HongqiBrowser.exe"
  Delete "$INSTDIR\app.ico"
  Delete "$INSTDIR\current.txt"
  Delete "$INSTDIR\HongqiquBrowser.install-id"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  ; Personal profile and update history are deliberately retained outside INSTDIR.
SectionEnd
