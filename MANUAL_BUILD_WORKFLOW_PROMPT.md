# Manual Build Workflow 생성 프롬프트 (범용)

아래 요구사항을 **정확히 반영해서** GitHub Actions 워크플로우 파일을 작성해 주세요.

## 목표
- `workflow_dispatch` 기반 수동 빌드 워크플로우를 만든다.
- 여러 빌드 타깃(예: 백엔드, 윈도우 앱, 모바일 앱)을 선택적으로 빌드한다.
- 빌드 결과를 Actions Artifact로 업로드한다.
- 옵션으로 GitHub Release 자산 자동 첨부를 지원한다.
- 필요 시 dependencies 파일(예: DLL/so/dylib)을 빌드 전에 지정 경로로 복사한다.
- 저장소가 Git LFS를 사용하면 체크아웃 시 LFS를 활성화한다.

## 출력 규칙
- 결과는 설명 없이 **완성된 YAML 코드만** 출력한다.
- 문법 오류 없는 단일 워크플로우 파일로 작성한다.
- 파일 경로는 `.github/workflows/manual-build.yml`로 가정한다.

## 필수 구조

### 1) 트리거/권한
- `on: workflow_dispatch`
- `permissions`는 Release 첨부를 위해 `contents: write`

### 2) 입력값(inputs)
- `target` (choice, required): 빌드 타깃 선택
- `source_ref` (string, required, default: `main`): 브랜치/태그/커밋
- `release_tag` (string, optional, default: `""`)
- `release_name` (string, optional, default: `""`)
- `prerelease` (boolean, optional, default: `false`)

### 3) 잡 분기 방식
- `if: ${{ inputs.target == '...' }}` 형태로 타깃별 잡 분리
- 각 잡은 필요한 OS 러너를 사용
  - Windows: `windows-latest`
  - Linux: `ubuntu-latest`
  - macOS 필요 시: `macos-latest`

### 4) 공통 체크아웃
- `actions/checkout@v4`
- `with.ref: ${{ inputs.source_ref }}`
- LFS 사용 저장소라면 `with.lfs: true`

### 5) 빌드 전 준비
- 각 타깃별 패키지 복원 단계 추가
  - .NET: `nuget restore` 또는 `dotnet restore`
  - Node: `npm ci` 또는 `pnpm install --frozen-lockfile`
  - Android: JDK/SDK 설치
- dependencies 사전 복사가 필요하면 다음 규칙 적용
  - source 디렉토리와 destination 목록을 변수로 분리
  - destination 디렉토리 생성 후 필요한 확장자만 복사
  - 예시 확장자: `*.dll`, `*.so`, `*.dylib`

### 6) 빌드 실행
- 타깃별 빌드 명령을 명시적으로 작성
- 플랫폼/구성값(예: `Release`, `x64`)을 파라미터로 고정
- 툴 경로 탐색이 필요하면(예: MSBuild) 안전한 방식으로 경로 탐색 후 실행

### 7) Artifact 업로드
- `actions/upload-artifact@v4`
- 아티팩트 이름에 타깃명과 `github.run_number` 포함
- 경로는 타깃별 빌드 산출물 위치를 사용
- `if-no-files-found: warn`

### 8) Release 자동 첨부(옵션이지만 기본 포함)
- release 메타데이터 step(`id: release_meta`) 추가
  - `release_tag`가 비어 있으면 자동 생성
  - `release_name`이 비어 있으면 자동 생성
  - `GITHUB_OUTPUT`에 `tag`, `name` 기록
- 필요 시 산출물 zip 패키징 후 `softprops/action-gh-release@v2`로 첨부
- `target_commitish: ${{ inputs.source_ref }}`
- `prerelease: ${{ inputs.prerelease }}`
- `overwrite_files: true`

## 자리표시자(반드시 교체)
- `<TARGET_OPTIONS>`: 예) `api`, `windows-client`, `android-app`
- `<RESTORE_COMMAND>`
- `<BUILD_COMMAND>`
- `<DEPENDENCY_SOURCE_DIR>`
- `<DEPENDENCY_DEST_DIRS>`
- `<ARTIFACT_PATH>`
- `<RELEASE_FILE_PATH>`

## 품질 기준
- 잡 이름/스텝 이름이 역할을 설명해야 한다.
- 동일 로직은 스텝 구조를 일관되게 유지한다.
- 경로/파일명은 하드코딩하되, 타깃마다 명확히 분리한다.
- 실패 지점 파악이 쉽도록 단계 이름을 세분화한다.
