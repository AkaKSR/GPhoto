# GPhoto

Copyright (c) 2025 AkaKSR

이미지와 선택적 페이로드 파일을 관리하는 간단한 Windows Forms 도구입니다. FTP 업로드 지원과 단일 실행 파일 빌드 스크립트를 포함합니다.

**프로젝트 정보**
- **이름:** `GPhoto`
- **프레임워크:** .NET 10 (TargetFramework: `net10.0-windows`)
- **UI:** Windows Forms (WinForms)

**주요 기능**
- `DataGridView`에서 이미지 목록을 관리합니다 (`No.`, `이미지 파일명`, 페이로드 플래그, 생성된 페이로드 파일명 등).
- 왼쪽에 선택용 체크박스 컬럼(`colSelect`)을 추가했고, 헤더 체크박스로 모든 행을 토글할 수 있습니다.
- `UploadSettingsForm`를 통해 FTP 서버 정보를 입력/저장하면, 생성된 페이로드 파일을 FTP로 업로드할 수 있습니다. 설정은 `config.dat`에 DPAPI(Windows 데이터 보호 API)로 암호화되어 저장됩니다.
- 업로드 진행 상태는 `UploadProgressForm`에서 파일별/전체 진행률과 취소 기능을 제공합니다.

**중요한 파일/컨트롤**
- `MainForm.Designer.cs`: UI 레이아웃 및 `dataGridViewFiles` 컬럼 정의 (`colSelect`, `colNo`, `colFileName`, `colPayload`, `colPayloadName`, `colGeneratedPayloadName`, `colUploaded`, `colDescription`).
- `MainForm.cs`: 헤더 체크박스 동작, 업로드 흐름, FTP 상호작용 등 애플리케이션 로직.
- `UploadSettingsForm.cs`: FTP 호스트/포트/아이디/암호 구성 및 암호화된 설정 저장.
- `UploadProgressForm.cs`: 업로드 진행 대화상자.

빌드 및 배포
-----------------
레포 루트에 포함된 스크립트로 프레임워크-종속 단일 실행 파일(런타임 미포함) Release 빌드를 생성할 수 있습니다.

- Bash (Git Bash / WSL / bash.exe): `build_singlefile_fd.sh`
- PowerShell: `build_singlefile_fd.ps1`

기본 사용법 (Win x64):
```bash
./build_singlefile_fd.sh
```
RID를 지정하려면:
```bash
RID=win-x86 ./build_singlefile_fd.sh
```
PowerShell 사용 예:
```powershell
.\build_singlefile_fd.ps1
.\build_singlefile_fd.ps1 -RID win-x86
```

스크립트 동작
- `dotnet publish`를 `Release` 구성으로 실행합니다.
- `-p:PublishSingleFile=true` 및 `-p:SelfContained=false`를 사용하여 단일 EXE를 생성하되 .NET 런타임은 호스트에 설치되어 있어야 합니다.
- `OutputType=WinExe`를 강제하여 Windows GUI 서브시스템으로 빌드하므로 탐색기에서 실행할 때 콘솔 창이 나타나지 않습니다.

생성된 EXE 실행 방법
--------------------
- 생성된 EXE는 `publish/<RID>/singlefile-fd/`에 위치합니다 (예: `publish/win-x64/singlefile-fd/GPhoto.exe`).
- 탐색기에서 더블클릭하면 콘솔 창 없이 GUI가 실행됩니다.
- 이미 열린 `cmd`/PowerShell에서 직접 실행하면 해당 콘솔 창은 계속 보입니다. 콘솔 없이 분리하여 실행하려면:
```powershell
Start-Process -FilePath 'C:\path\to\publish\win-x64\singlefile-fd\GPhoto.exe'
```
또는 cmd에서:
```cmd
start "" "C:\path\to\publish\win-x64\singlefile-fd\GPhoto.exe"
```

설정 및 구성
------------------------
- FTP 설정은 `UploadSettingsForm`에서 `config.dat`로 저장됩니다. 파일은 JSON 형식이며 Windows DPAPI(현재 사용자)로 암호화됩니다.
- 설정을 이전하거나 백업하려면 `UploadSettingsForm`을 통해 다시 입력하거나 수동으로 처리하세요.

주의사항 및 문제해결
------------------------
- 탐색기에서 더블클릭해도 콘솔이 뜬다면 `GPhoto.csproj`의 `OutputType`이 `WinExe`로 설정되었는지 확인하세요 (스크립트에서 publish 시 강제로 설정합니다).
- 현재 FTP 전송은 구식 `FtpWebRequest` API를 사용합니다. FTPS, 재시도, 안정적인 전송이 필요하면 `FluentFTP` 같은 라이브러리로 마이그레이션을 권장합니다.
- `PublishTrimmed=true`로 트리밍하면 리플렉션으로 로드하는 코드나 일부 라이브러리가 제거되어 런타임 오류가 발생할 수 있으니 주의하세요.

개발 팁
----------------
- 로컬에서 Debug 빌드 및 실행:
```bash
dotnet build
dotnet run --project ./GPhoto.csproj
```
- 런타임을 포함한 self-contained 단일 실행 파일을 만들려면 스크립트에서 `-p:SelfContained=true`로 변경하고 적절한 `RuntimeIdentifier`를 지정하세요.

라이선스
-------
이 프로젝트는 MIT 라이선스에 따라 배포됩니다. 자세한 내용은 `LICENSE` 파일을 확인하세요.
