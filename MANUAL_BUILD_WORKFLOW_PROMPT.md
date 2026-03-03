# Manual Build Workflow 생성 프롬프트 (현재 기준)

아래 요구사항을 **정확히 반영해서** GitHub Actions 워크플로우 파일 `.github/workflows/manual-project-build.yml`을 작성해 주세요.

## 목적
- `workflow_dispatch`로 수동 실행한다.
- 프로젝트별(`manager-windows`, `player-windows`, `player-android`)로 빌드한다.
- 빌드 결과를 Actions Artifact로 업로드하고, 동시에 GitHub Release 자산으로 자동 첨부한다.
- Windows 빌드 시 dependencies DLL을 사전에 올바른 빌드 경로로 복사해서 참조 오류를 방지한다.
- 저장소의 `*.dll`이 LFS 대상이므로 체크아웃 시 반드시 LFS를 활성화한다.

## 필수 조건
- 파일명: `.github/workflows/manual-project-build.yml`
- workflow 이름: `Manual Build by Project`
- 트리거: `workflow_dispatch`
- `permissions`: `contents: write`

### workflow_dispatch inputs
- `project` (choice, required)
  - options: `manager-windows`, `player-windows`, `player-android`
- `source_ref` (string, required, default: `main`)
- `release_tag` (string, optional, default: `""`)
- `release_name` (string, optional, default: `""`)
- `prerelease` (boolean, optional, default: `false`)

## Job 1: build-manager-windows
- 조건: `${{ inputs.project == 'manager-windows' }}`
- 러너: `windows-latest`
- 체크아웃:
  - `actions/checkout@v4`
  - `ref: ${{ inputs.source_ref }}`
  - `lfs: true`
- NuGet: `NuGet/setup-nuget@v2`
- MSBuild: `microsoft/setup-msbuild@v2` with `msbuild-architecture: x86`
- NuGet restore:
  - `nuget restore "Manager/AndoW_Manager/AndoW_Manager.sln" -PackagesDirectory "Manager/packages" -NonInteractive`
- Dependencies 사전 복사 (PowerShell):
  - source: `Manager/Dependencies`
  - destinations:
    - `Manager/dll`
    - `Manager/AndoW_Manager/bin/Release`
    - `Manager/AndoW_Manager/bin/x64/Release`
  - 각 destination 디렉토리 생성 후 `"$src/*.dll"`만 복사 (`-Force`)
- Build:
  - x86 MSBuild 경로를 `vswhere`로 찾고(amd64 제외),
  - `AndoW_Manager.sln`을 `Release|x64`로 빌드
- Artifact 업로드:
  - `actions/upload-artifact@v4`
  - name: `manager-windows-manual-${{ github.run_number }}`
  - path: `Manager/AndoW_Manager/**/bin/**/Release/**`
- Release 메타데이터 step (`id: release_meta`):
  - `release_tag` 비어있으면: `manual-${{ inputs.project }}-run-${{ github.run_number }}`
  - `release_name` 비어있으면: `Manual ${{ inputs.project }} build #${{ github.run_number }}`
  - `GITHUB_OUTPUT`에 `tag`, `name` 기록
- Release용 zip 패키징:
  - `release-assets/manager-windows-manual-${{ github.run_number }}.zip`
  - 대상: `Manager/AndoW_Manager/bin`
- GitHub Release 첨부:
  - `softprops/action-gh-release@v2`
  - `tag_name`, `name`은 `release_meta` 출력 사용
  - `target_commitish: ${{ inputs.source_ref }}`
  - `prerelease: ${{ inputs.prerelease }}`
  - files: 위 zip
  - `overwrite_files: true`

## Job 2: build-player-windows
- 조건: `${{ inputs.project == 'player-windows' }}`
- 러너: `windows-latest`
- 체크아웃:
  - `actions/checkout@v4`
  - `ref: ${{ inputs.source_ref }}`
  - `lfs: true`
- NuGet: `NuGet/setup-nuget@v2`
- MSBuild: `microsoft/setup-msbuild@v2` with `msbuild-architecture: x86`
- NuGet restore:
  - `nuget restore "Player/Windows/AndoW Player.sln" -PackagesDirectory "Player/Windows/packages" -NonInteractive`
- Dependencies 사전 복사 (PowerShell):
  - source: `Player/Windows/Dependencies`
  - destinations:
    - `Player/Windows/AndoW_Player/bin/x64/Release`
    - `Player/Windows/ConfigPlayer/bin/x64/Release`
  - destination 디렉토리 생성 후 `"$src/*.dll"`만 복사 (`-Force`)
- Build:
  - x86 MSBuild 경로를 `vswhere`로 찾고(amd64 제외),
  - `AndoW Player.sln`을 `Release|x64`로 빌드
- Artifact 업로드:
  - name: `player-windows-manual-${{ github.run_number }}`
  - path: `Player/Windows/**/bin/**/Release/**`
- Release 메타데이터 step (`id: release_meta`)는 Job 1과 동일 규칙
- Release용 zip 패키징:
  - `release-assets/player-windows-manual-${{ github.run_number }}.zip`
  - 대상: `Player/Windows/AndoW_Player/bin`, `Player/Windows/ConfigPlayer/bin`
- GitHub Release 첨부:
  - `softprops/action-gh-release@v2`
  - `target_commitish`, `prerelease`, `overwrite_files: true` 동일

## Job 3: build-player-android
- 조건: `${{ inputs.project == 'player-android' }}`
- 러너: `ubuntu-latest`
- 체크아웃:
  - `actions/checkout@v4`
  - `ref: ${{ inputs.source_ref }}`
  - `lfs: true`
- Java: `actions/setup-java@v4` (`distribution: temurin`, `java-version: '11'`)
- Android SDK: `android-actions/setup-android@v3`
- SDK 설치:
  - `yes | sdkmanager --licenses || true`
  - `sdkmanager "platform-tools" "platforms;android-22" "build-tools;29.0.2"`
- 빌드:
  - working-directory: `Player/Android`
  - `chmod +x ./gradlew`
  - `./gradlew --no-daemon clean :app:assembleRelease`
- Artifact 업로드:
  - name: `player-android-manual-${{ github.run_number }}`
  - path: `Player/Android/app/build/outputs/apk/release/*.apk`
- Release 메타데이터 step (`id: release_meta`)는 동일 규칙
- GitHub Release 첨부:
  - files: `Player/Android/app/build/outputs/apk/release/*.apk`
  - 나머지 옵션 동일

## 제약/주의
- 삭제된 `SignalR_Net472` 프로젝트는 워크플로우 대상에서 제외한다.
- 들여쓰기/문법이 GitHub Actions YAML에서 바로 동작하도록 작성한다.
- 결과물은 설명 없이 **완성된 YAML 코드만** 출력한다.
