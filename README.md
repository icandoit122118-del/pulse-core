# Pulse Core (`v1.0.0`)

**Microsoft PC Manager (`v3.23.0.0`) REA 리버스 엔지니어링 재조립 + 미꾸라지 VPN & 롤(LoL) 핑 가드 데스크톱 앱**

## 주요 기능
1. **프로세스 관리 (`SoftwarePlugin` & `ProcessUtils` 복원)**
   - 다중 하위 프로세스를 부모 앱 단위로 그룹화하고 점유 메모리 및 고해상도 아이콘 표시
   - 개별 앱 **종료** 및 **1-Click RAM 부스트** (`psapi.dll!EmptyWorkingSet` + `kernel32.dll!SetProcessWorkingSetSizeEx`)
2. **미꾸라지 VPN & 롤(LoL) 4ms 핑 가드 (`MudfishVpnPlugin`)**
   - 미꾸라지 Full VPN 우회 라우트(`0.0.0.0/1` & `128.0.0.0/1`) 실시간 감지
   - **원클릭 스플릿 터널링 (`롤 핑 4ms 즉시 복구`)**: 메이플스토리 클래식 월드 VPN은 유지하면서 롤 국내 트래픽만 4ms 직결로 복구
   - **Auto Game Ping Guard**: `League of Legends.exe` 실행 시 자동으로 Full VPN 납치 라우트 차단
   - **3점 실시간 핑 모니터**: 게이트웨이(`192.168.219.1`), 국내 롤/KT(`168.126.63.1`), 해외 Google(`8.8.8.8`)
3. **고정형 100% GDI+ 벡터 UI & 시스템 트레이 상주**
   - `464 x 708` 고정 해상도 + Windows 11 DWM 라운드 코너 + 커스텀 벡터 아이콘 레일
   - `X`(닫기) 버튼 클릭 시 종료되지 않고 Windows 시스템 트레이(알림 영역)에 상주하여 24시간 백그라운드 작동
   - Windows 시작 시 자동 실행 (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run\PulseCore`)

## 빌드 방법 (외부 의존성 없음)
```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /out:PulseCore.exe /win32icon:assets\pulse_core.ico /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll src\PulseCore.cs
```
