# 플레이어 심리스 로직 크로스플랫폼 구현 프롬프트

## 문서 목적

이 문서는 현재 저장소의 Windows 플레이어와 Android 플레이어 구현을 **직접 다시 읽어 추출한 실제 동작 의미**를 기준으로, 코딩 에이전트가 Windows뿐 아니라 macOS, Linux, Android, 임베디드 Linux 계열까지 **동일한 심리스 재생 의미를 깨지 않고 구현**하도록 통제하기 위한 최종 프롬프트다.

이 문서의 목표는 아이디어 정리가 아니라 다음 두 가지다.

- 현재 제품이 이미 가진 심리스 재생 의미를 손상 없이 재구성한다.
- OS가 달라도 동일한 상태 전이, 준비 기준, 전환 기준, 예외 처리 기준을 유지하게 만든다.

이 문서는 특히 아래 현재 코드의 의미를 합쳐서 표준화한다.

- Windows 기준:
  - `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessPlaybackContainer.cs`
  - `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessLayoutRuntime.cs`
  - `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessContentSlot.cs`
  - `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessMpvSurface.cs`
  - `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackCoordinator.cs`
  - `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackContracts.cs`
  - `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml.cs`
  - `Player/Windows/NewHyOn_Player/MainWindow.xaml.cs`

- Android 기준:
  - `Player/Android/Quber/Quber_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/MediaView.java`
  - `Player/Android/Quber/Quber_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/PlaybackSlotView.java`
  - `Player/Android/Quber/Quber_Player/app/src/main/java/kr/co/turtlelab/andowsignage/AndoWSignage.java`
  - `Player/Android/Krizer/Krizer_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/MediaView.java`
  - `Player/Android/Krizer/Krizer_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/PlaybackSlotView.java`
  - `Player/Android/Krizer/Krizer_Player/app/src/main/java/kr/co/turtlelab/andowsignage/AndoWSignage.java`

---

## 현재 코드에서 확정된 핵심 사실

### 1. 심리스 재생은 단순한 페이지 넘김이 아니라 3계층 문제다

현재 코드 기준으로 심리스 재생은 아래 3계층을 동시에 풀어야 한다.

- 상위 오케스트레이션 계층
  - 페이지 선택
  - 다음 페이지 선준비
  - 스케줄 전환 예약
  - 플레이리스트 리로드 예약
  - sync 반영
  - on-air/off-air 반영
  - heartbeat 반영

- 레이아웃 계층
  - 현재 active 레이아웃
  - 다음 일반 페이지 standby 레이아웃
  - 다음 스케줄 또는 특별 전환 reserve 레이아웃

- 슬롯/미디어 계층
  - 각 영역별 콘텐츠 시퀀스 재생
  - 이미지/비디오 전환
  - 현재 콘텐츠 경계 판단
  - 첫 프레임 준비 판정
  - 콘텐츠 종료 또는 타이머 종료 판정

### 2. 현재 제품의 실제 기준 레이아웃 풀 크기는 3이다

Windows는 `Layout-A`, `Layout-B`, `Layout-C` 3개를 가진다.

Android는 `activePageRuntime`, `stagedPageRuntime`, `specialPageRuntime`과 그에 대응하는 컨테이너 3개를 가진다.

따라서 크로스플랫폼 표준 의미는 **2-layout이 아니라 3-layout pool**이다.

### 3. 슬롯은 동적으로 무한 생성하는 구조가 아니라 고정 풀 구조다

현재 코드에서 슬롯은 고정 개수 재사용이 핵심이다.

- Windows는 페이지 계획에서 최대 6개 `Media` 요소만 사용한다.
- Android Quber/Krizer는 `PRECREATED_MEDIA_SLOT_COUNT = 4` 로 컨테이너당 4개를 미리 만든다.

따라서 크로스플랫폼 구현은 아래 원칙을 따른다.

- 슬롯 수는 앱 시작 시 고정 생성한다.
- 실행 중 슬롯을 새로 만들지 않는다.
- 플랫폼/제품별 상한은 다를 수 있어도 동작 의미는 동일해야 한다.
- 권장 방식은 `MAX_MEDIA_SLOTS` 상수를 플랫폼 빌드 단위로 고정하고, 런타임 중 가변 확장하지 않는 것이다.

### 4. 심리스 준비는 prepare/show/start의 3단계로 분리되어야 한다

Android 현재 구현은 이 점이 매우 명확하다.

- `prepareInitialContent()`
  - 이미지면 현재 이미지 로드 + 다음 이미지 프리로드
  - 비디오면 첫 프레임 준비
- `showPreparedContent()`
  - 준비된 첫 화면을 먼저 노출
- `startPreparedPlayback()`
  - 실제 재생을 시작

Windows도 의미상 동일하다.

- plan 생성
- layout prepare
- activate
- standby prepare

따라서 다른 OS에서도 이 3단계는 반드시 살아 있어야 한다.

### 5. 콘텐츠 전환 기준은 파일 타입별로 다르며 현재 코드에 이미 엄격한 규칙이 있다

Windows `PlaybackCoordinator.cs` 기준:

- 재생 시간이 `00:00` 인 콘텐츠는 제외한다.
- 기간 제한을 벗어난 콘텐츠는 제외한다.
- 파일이 없거나 길이가 0이면 제외한다.
- 비디오는 실제 재생 길이를 읽어 configured duration과 비교한다.

비디오 전환 규칙은 아래와 같이 현재 코드에서 이미 정해져 있다.

- configured duration < actual duration
  - 루프하지 않는다.
  - end event를 기다리지 않고 타이머 기준으로 다음으로 넘어간다.
- configured duration == actual duration
  - 루프하지 않는다.
  - 1회의 정상 종료 event를 기다린다.
- configured duration > actual duration 이고 나머지가 0
  - 필요한 횟수만큼 루프한다.
  - 마지막 한 바퀴 직전에 loop를 끄고 end event 횟수로 정확히 닫는다.
- configured duration > actual duration 이고 나머지가 0이 아님
  - 루프는 유지한다.
  - 정확한 configured duration 시점에 타이머로 잘라 다음으로 넘어간다.

이 규칙은 다른 OS에서도 그대로 유지해야 한다.

### 6. 레이아웃 전환은 콘텐츠 경계와 페이지 경계를 구분한다

현재 코드에는 전환 타이밍 정책이 존재한다.

- `Immediately`
- `ContentEnd`
- `PageEnd`

따라서 스케줄 전환과 플레이리스트 리로드는 아래 조건식으로 처리해야 한다.

- 즉시 전환 허용
- 현재 콘텐츠 종료 시점에만 허용
- 현재 페이지 종료 시점에만 허용

이 의미는 OS가 바뀌어도 변하면 안 된다.

### 7. 스케줄 전환은 “도착 후 로딩”이 아니라 “선준비 후 활성화”여야 한다

Windows 현재 구현은 다음 스케줄을 **1분 이내**에 선준비하는 warmup 경로를 갖고 있다.

Android 현재 구현은 `special runtime`을 별도 reserve 성격으로 유지한다.

즉 표준 의미는 아래다.

- 현재 active 재생은 건드리지 않는다.
- reserve layout에서 다음 스케줄의 첫 페이지를 미리 준비한다.
- 준비가 안 끝났으면 전환을 보류한다.
- 준비가 끝난 뒤 허용 타이밍이 오면 즉시 교체한다.

### 8. 현재 코드의 전환 무결성 핵심은 “stale 결과 무효화”다

Windows는 `playbackVersion` 으로 이전 준비 결과를 무효화한다.

Android는 `pageBuildGeneration`, `mediaConfigurationVersion`, `prepareCancelled`로 오래된 준비 결과를 버린다.

따라서 크로스플랫폼 구현은 반드시 다음을 가져야 한다.

- 페이지 준비 generation
- 레이아웃 준비 generation
- 슬롯/미디어 준비 generation
- 준비 완료 callback에서 generation mismatch 발생 시 결과 버림

### 9. 심리스 구현에서 fallback은 기능이 아니라 버그 은폐 수단이 된다

현재 코드도 fallback timer를 일부 사용하지만, 그 fallback은 준비 완료 신호가 영원히 오지 않을 때 UI를 멈추지 않게 하는 안전장치일 뿐, 구조적 실패를 감추기 위한 새 경로가 아니다.

따라서 다른 OS에서도 허용되는 fallback은 아래뿐이다.

- 렌더 시작 이벤트가 드물게 오지 않을 때의 짧은 presentation fallback
- playback ready callback이 누락될 때의 짧은 ready fallback

허용되지 않는 fallback:

- 준비가 안 된 레이아웃을 그냥 보여주는 fallback
- 기존 구식 재생 경로로 몰래 우회하는 fallback
- 실패 시 새 뷰를 계속 생성해서 버티는 fallback
- 스케줄 전환이 늦으면 검은 화면을 넣고 나중에 덮는 fallback

---

## 크로스플랫폼 표준 구조

다른 OS로 구현할 때는 반드시 아래처럼 **플랫폼 독립 코어 + 플랫폼 어댑터** 구조로 구현한다.

### A. 플랫폼 독립 코어

코어는 순수 상태 머신이어야 한다.

책임:

- 현재 페이지, 다음 페이지, reserve 페이지 결정
- 플레이리스트 변경 예약
- 스케줄 변경 예약
- warmup 트리거
- 레이아웃 역할 배정
- 준비 완료 여부 판정
- stale generation 무효화
- 콘텐츠 경계, 페이지 경계, 전환 타이밍 정책 평가

코어는 직접 UI API를 호출하지 않는다.

### B. 플랫폼 어댑터

플랫폼 어댑터는 실제 UI와 미디어 백엔드를 다룬다.

책임:

- 레이아웃 host 생성/배치/숨김/전면화
- 슬롯 surface 생성/재사용
- 이미지 준비
- 비디오 첫 프레임 준비
- pause/resume/seek/index switch
- media loaded / rendering start / ended 이벤트 전달

### C. 공통 인터페이스 권장

다른 OS로 옮길 때 아래 인터페이스는 반드시 분리한다.

- `ISeamlessPlaybackEngine`
- `ISeamlessLayoutHost`
- `ISeamlessSlotSurface`
- `IMediaBackend`
- `IScheduleEvaluator`
- `IPlaylistProvider`
- `ISyncBridge`
- `IHeartbeatBridge`
- `IClock`
- `ILogger`

핵심은 UI toolkit과 media backend를 바꿔도 코어 상태 머신은 바뀌지 않게 만드는 것이다.

---

## 코딩 에이전트에게 그대로 전달할 최종 프롬프트

```md
현재 저장소의 플레이어 심리스 로직을 기준으로, Windows뿐 아니라 macOS, Linux, Android, 기타 OS에서도 동일한 의미로 재사용 가능한 **크로스플랫폼 심리스 재생 엔진**을 구현하라.

중요:
- 반드시 현재 시점의 실제 코드를 다시 읽고 시작한다.
- 추측하지 말고 현재 코드의 동작 의미를 우선한다.
- 질문하지 말고 끝까지 구현한다.
- 문서만 쓰지 말고 실제 동작하는 결과를 만든다.
- fallback으로 구조적 문제를 덮지 않는다.
- 응답과 작업은 항상 한국어 기준으로 한다.

## 먼저 읽어야 할 파일

Windows 기준:
- Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessPlaybackContainer.cs
- Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessLayoutRuntime.cs
- Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessContentSlot.cs
- Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessMpvSurface.cs
- Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackCoordinator.cs
- Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackContracts.cs
- Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml.cs
- Player/Windows/NewHyOn_Player/MainWindow.xaml.cs

Android 기준:
- Player/Android/Quber/Quber_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/MediaView.java
- Player/Android/Quber/Quber_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/PlaybackSlotView.java
- Player/Android/Quber/Quber_Player/app/src/main/java/kr/co/turtlelab/andowsignage/AndoWSignage.java
- Player/Android/Krizer/Krizer_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/MediaView.java
- Player/Android/Krizer/Krizer_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/PlaybackSlotView.java
- Player/Android/Krizer/Krizer_Player/app/src/main/java/kr/co/turtlelab/andowsignage/AndoWSignage.java

필요하면 위 파일들이 호출하는 실제 참조 파일도 추가로 읽는다.

## 절대 바꾸면 안 되는 동작 의미

### 1. 레이아웃 풀은 3개다

레이아웃 역할은 항상 아래 셋 중 하나다.
- ACTIVE: 현재 화면에 노출되어 실제 재생 중
- STANDBY: 다음 일반 페이지 또는 다음 일반 플레이리스트 첫 페이지가 준비된 상태
- RESERVED: 다음 스케줄 전환 또는 특별 전환을 선준비하는 상태

레이아웃 인스턴스는 앱 시작 시 한 번만 만든다.
실행 중 레이아웃을 새로 만들지 않는다.

### 2. 슬롯은 고정 풀이다

슬롯 수는 제품 상수로 고정한다.
- Windows 참조 구현은 6
- 현재 Android 참조 구현은 4

중요한 것은 숫자가 아니라 원칙이다.
- 시작 시 한 번만 생성
- 실행 중 새로 생성 금지
- 사용하지 않는 슬롯은 비활성화
- 페이지마다 필요한 element를 기존 슬롯에 재바인딩

### 3. prepare → show → start를 분리한다

한 레이아웃을 활성화하기 전 아래 단계가 분리되어야 한다.

1. prepare
- 레이아웃 크기/위치/z-order 반영
- 슬롯별 콘텐츠 시퀀스 구성
- 첫 이미지 로드 또는 첫 비디오 프레임 준비
- 다음 이미지 프리로드

2. show
- 준비된 첫 결과를 아직 재생 시작 전 화면에 올림
- active 전환 직전까지 off-screen 또는 hidden 상태 유지 가능

3. start
- 실제 시간 흐름을 시작
- 콘텐츠 경계 이벤트 발생 시작
- sync/heartbeat 반영 시작

### 4. 상태 머신은 명시적으로 둔다

반드시 아래 상태를 코드로 둔다.

레이아웃 상태:
- Idle
- Preparing
- Ready
- Active
- Error

슬롯 상태:
- Idle
- Preparing
- Ready
- Active
- Error

콘텐츠 준비 상태:
- None
- InitialPrepared
- VisiblePrepared
- PlaybackStarted

전환 예약 상태:
- None
- PendingScheduleSwitch
- PendingPlaylistReload
- WarmupRequested
- WarmupPrepared
- PendingActivation

각 상태 변경은 로그로 남기고, 상태 건너뛰기를 허용하지 않는다.

### 5. generation 기반 stale 무효화를 반드시 넣는다

반드시 아래 generation을 분리한다.
- playback generation
- page build generation
- layout prepare generation
- slot/media prepare generation

준비 callback, media loaded callback, image loaded callback, video first-frame callback이 도착했을 때 generation이 다르면 결과를 버린다.
오래된 prepare 결과가 현재 active 상태를 덮어쓰면 안 된다.

### 6. 콘텐츠 길이 규칙은 현재 Windows 의미를 그대로 따른다

비디오 길이 계산 규칙:
- configured duration과 actual duration을 둘 다 구한다.
- actual duration이 더 길면 timer cut으로 넘어간다.
- 정확히 같으면 natural end event를 쓴다.
- configured가 더 길고 exact multiple이면 loop 후 마지막 natural end로 닫는다.
- configured가 더 길고 remainder가 있으면 loop 유지 + timer cut으로 닫는다.

이미지 길이 계산 규칙:
- configured duration을 그대로 사용한다.
- 첫 이미지 표시 이후부터 시간이 흐른다.
- 가능한 경우 다음 이미지를 미리 decode/preload 한다.

제외 규칙:
- 재생 시간이 0인 콘텐츠 제외
- 기간 제한에서 벗어난 콘텐츠 제외
- 파일 미존재 제외
- 길이 0 파일 제외
- 파싱 실패 콘텐츠 제외

### 7. 전환 타이밍 정책을 그대로 유지한다

반드시 아래 3정책을 지원한다.
- Immediately
- ContentEnd
- PageEnd

정책 의미:
- Immediately: 준비가 끝나면 즉시 전환 허용
- ContentEnd: 현재 재생 중인 콘텐츠 경계에서만 전환 허용
- PageEnd: 현재 페이지 전체 재생 완료 시점에서만 전환 허용

스케줄 전환과 플레이리스트 리로드 모두 이 정책을 동일하게 적용한다.

### 8. warmup은 실제 전환 전에 reserve layout을 미리 준비하는 행위다

반드시 아래를 구현한다.
- 다음 스케줄이 1분 이내면 reserve layout에 첫 페이지 선준비
- 필요한 미디어 파일이 없으면 전환하지 않고 다운로드/동기화 재시도만 트리거
- reserve layout이 준비되기 전에는 active를 건드리지 않음
- 허용 타이밍이 오면 reserve layout을 active로 승격

### 9. active → next 전환은 atomic해야 한다

전환 시 반드시 아래 순서를 지킨다.

1. next layout 준비 완료 확인
2. next layout 첫 화면 또는 첫 프레임 준비 확인
3. next layout을 front로 올림
4. next layout show
5. next layout start
6. active layout hide/pause
7. active layout을 standby 또는 reusable 상태로 되돌림
8. 비워진 레이아웃에 다음 target warmup 시작

전환 중 새 레이아웃 인스턴스를 만들지 않는다.

### 10. 콘텐츠 경계와 페이지 경계를 분리해 계산한다

콘텐츠 경계:
- 같은 페이지 내부에서 현재 콘텐츠에서 다음 콘텐츠로 넘어가는 시점
- 이미지면 configured duration 종료 시점
- 비디오면 timer cut 또는 natural end 규칙으로 결정

페이지 경계:
- 현재 페이지 전체 재생 종료 시점
- 레이아웃 단위 완료 이벤트

콘텐츠 경계와 페이지 경계는 각각 독립 이벤트로 다뤄야 하며, 전환 정책이 이를 사용해야 한다.

### 11. sync 리더 기준을 유지한다

현재 코드처럼 primary visible slot 또는 primary video slot의 상태를 sync 리더 상태로 사용한다.
반드시 아래 정보를 계산한다.
- 현재 콘텐츠 이름
- 다음 콘텐츠 이름
- 현재 인덱스
- 다음 인덱스
- 현재 콘텐츠 경과 시간
- 현재 콘텐츠 총 길이
- 가시 여부

이 정보는 heartbeat, remote sync, 디버그 뷰에서 재사용 가능해야 한다.

### 12. heartbeat와 현재 페이지 상태를 끊지 않는다

다음 정보는 전환 중에도 일관되게 유지해야 한다.
- 현재 playlist 이름
- 현재 page 이름
- 다음 page 이름
- 현재 페이지 경과 시간
- 현재 페이지 총 시간
- 재생 active 여부

준비 중인 레이아웃이 active 상태 정보를 덮어쓰면 안 된다.

### 13. prepare는 background, show/start는 UI thread에서 한다

금지:
- UI thread에서 파일 시스템 스캔
- UI thread에서 재생 계획 전체 계산
- UI thread에서 미디어 메타데이터 대량 계산

허용:
- UI thread에서 surface attach
- UI thread에서 visibility/opacity/z-order 변경
- UI thread에서 player seek/play/pause 호출
- UI thread에서 first-frame 표시

### 14. 다른 OS에서도 media backend만 바꾸고 의미는 유지한다

플랫폼별 권장 backend 예시:
- Windows: libmpv/mpv
- macOS: libmpv 또는 AVFoundation
- Linux: libmpv 또는 GStreamer
- Android: 현재 MediaView/TurtleVideoView 또는 ExoPlayer 계열

하지만 어떤 backend를 쓰더라도 아래 인터페이스 의미는 고정이다.
- load playlist
- switch to index
- seek to start
- play
- pause
- stop
- set loop
- set muted
- report media loaded
- report first frame visible
- report natural end

### 15. 첫 프레임 준비 기준을 명확히 정의한다

이미지:
- 디코드가 완료되고 지정 surface 또는 image layer에 실제 배치 가능해야 Ready다.

비디오:
- 아래 중 하나를 만족해야 Ready다.
  - 첫 프레임이 surface에 준비됨
  - 즉시 start 시 첫 프레임 표시가 보장됨

단순히 파일 open 성공만으로 Ready 처리하면 안 된다.

### 16. 이미지 전환은 이중 버퍼를 유지한다

현재 Android 구현처럼 이미지는 2개의 image buffer를 두고 아래를 따른다.
- 현재 표시 버퍼
- 숨김 preload 버퍼
- 다음 이미지가 같으면 재사용
- 다르면 숨김 버퍼에 preload
- 전환 시 alpha crossfade 가능
- crossfade는 시각적 보조일 뿐, 준비가 안 된 이미지를 보여주는 수단으로 사용 금지

### 17. reserve layout과 standby layout은 충돌 없이 공존해야 한다

현재 활성 페이지의 다음 페이지 선준비와 다음 스케줄 선준비는 서로 다른 layout 역할로 공존해야 한다.
따라서 3개 layout 역할 배정은 반드시 explicit해야 한다.

예시:
- Layout A = ACTIVE
- Layout B = STANDBY for next normal page
- Layout C = RESERVED for upcoming schedule

전환 후 역할은 rotate될 수 있지만, 의미는 항상 유지해야 한다.

### 18. 로컬 재생 복구 의미를 유지한다

현재 Windows 구현처럼 재생 가능한 콘텐츠가 있는 플레이리스트를 찾는 복구 경로를 유지한다.

우선순위:
1. 현재 플레이리스트
2. 기본 플레이리스트
3. 로컬에 실제 재생 가능한 첫 플레이리스트

단, 이 복구는 구조 실패를 덮는 fallback이 아니라 데이터 부재 복구다.

### 19. 구현 산출물은 코어/플랫폼 경계가 보이게 만든다

반드시 아래 수준의 파일 경계를 만든다.
- playback engine / coordinator
- page plan builder
- layout runtime
- slot runtime
- media surface adapter
- contracts / state models
- diagnostics / debug snapshot
- platform integration bridge

파일명은 플랫폼에 맞게 조정 가능하지만 역할은 섞지 않는다.

### 20. 금지 사항

아래는 금지한다.
- 준비되지 않은 layout 활성화
- active layout을 먼저 내리고 next 준비를 시작하는 방식
- runtime마다 view/surface/player를 새로 생성하는 방식
- schedule switch 때 전체 엔진을 재기동하는 방식
- old callback이 new state를 덮어쓰는 구조
- UI thread에서 블로킹 sleep
- 실패 시 구식 다른 재생 경로로 몰래 우회
- 구조를 이해하지 못한 채 임시 if 분기 누적

## 반드시 구현해야 할 알고리즘

### A. 페이지 재생 요청 처리
1. 현재 target page와 next target page를 계산한다.
2. 두 페이지의 page plan을 background에서 만든다.
3. current plan을 담을 activation target layout을 선택한다.
4. 이미 동일 plan이 Ready면 재사용한다.
5. 아니면 prepare한다.
6. current plan이 활성화되면, 남는 layout 하나에 next plan을 standby 준비한다.
7. 별도로 upcoming schedule이 있으면 reserve layout을 warmup한다.

### B. 레이아웃 준비
1. layout 상태를 Preparing으로 바꾼다.
2. canvas/viewport/scale 정책을 반영한다.
3. slot별 plan을 주입한다.
4. 모든 slot prepare 완료를 기다린다.
5. 모든 prepare가 현재 generation에 속하면 Ready로 바꾼다.
6. 하나라도 실패하면 Error 또는 Ready-with-empty-slot 정책을 명시적으로 적용한다.

### C. 슬롯 준비
1. playable item list를 구성한다.
2. 비어 있으면 Idle 또는 Ready(empty)로 둔다.
3. 첫 item을 준비한다.
4. 이미지면 현재 이미지 준비 + 가능하면 다음 이미지 preload
5. 비디오면 첫 프레임 준비 또는 즉시 표출 가능 상태 확보
6. 준비 완료 후 Ready로 둔다.

### D. 콘텐츠 진행
1. layout timer 또는 monotonic clock으로 layout elapsed를 계산한다.
2. slot은 layout elapsed로 자신의 현재 item index와 item elapsed를 계산한다.
3. current index가 바뀌면 switchToIndex 또는 equivalent API로 전환한다.
4. loop on/off는 configured duration과 actual duration 규칙으로 조절한다.
5. primary slot 상태로 second tick / content boundary pulse를 만든다.

### E. 스케줄 평가
1. 평가 주기를 제한해 과도한 중복 계산을 막는다.
2. 현재 유지인지, 전환 예약인지, 다음 warmup 대상이 있는지 계산한다.
3. 현재와 다른 playlist면 pending schedule switch로 기록한다.
4. 1분 이내 upcoming switch면 reserve layout warmup을 시작한다.
5. 준비가 끝나고 허용 타이밍이 오면 active 전환을 시도한다.

### F. 플레이리스트 리로드
1. pending reload playlist를 기록한다.
2. switch timing 정책에 따라 허용 시점까지 보류한다.
3. 현재 playlist와 일치하고 허용 시점이면 reload 적용 후 첫 페이지부터 다시 준비한다.

### G. pending activation
1. active runtime이 아직 content boundary 이전이면 pending activation으로 둔다.
2. content boundary 또는 page boundary에서 다시 activation을 시도한다.
3. onMediaContentComplete 또는 equivalent hook은 pending activation 소비에 사용한다.

## 테스트와 검증

아래 검증을 반드시 자동 또는 반자동으로 수행한다.

### 1. 상태 머신 검증
- stale generation callback이 현재 상태를 덮지 않는지
- pending activation이 정확히 한 번만 소비되는지
- active/standby/reserved 역할이 동시에 충돌하지 않는지

### 2. 콘텐츠 길이 검증
- configured < actual video
- configured == actual video
- configured > actual and divisible
- configured > actual and non-divisible
- single image
- image sequence
- mixed image/video sequence

### 3. 스케줄 검증
- 현재 유지
- 즉시 전환
- 콘텐츠 종료 전환
- 페이지 종료 전환
- warmup 완료 전 도달
- warmup 완료 후 도달
- 파일 누락으로 전환 보류

### 4. 시각 검증
- active 전환 시 검은 화면이 끼지 않는지
- 이미지 → 이미지 전환 시 다음 이미지가 미리 준비되는지
- 이미지 → 비디오 전환 시 비디오 첫 프레임이 준비된 뒤 전환되는지
- 비디오 → 이미지 전환 시 이미지가 준비된 뒤 전환되는지

### 5. 리소스 검증
- steady state에서 view/surface/player 개수가 늘지 않는지
- 페이지 반복 전환 중 메모리 누수가 없는지
- stop/pause/clear 후 다음 prepare가 정상 재사용되는지

## 최종 산출물 요구

최종 보고에는 반드시 아래를 포함한다.
1. 변경한 상태 머신 요약
2. 생성/수정한 파일 목록
3. 플랫폼 독립 코어와 플랫폼 어댑터 경계 설명
4. 준비/전환/스케줄/warmup 구현 방식 요약
5. 검증한 시나리오와 결과
6. 남은 리스크

중요:
- 기존 구조를 부분적으로 감싼 척하지 말고 실제 메인 경로가 새 구조를 사용하게 만든다.
- 다른 OS 포팅 시에도 동일 의미를 유지할 수 있게, 비즈니스 로직과 media backend 의존을 분리한다.
- 구조를 단순하게 유지하되, 상태 전이와 준비 기준은 절대 모호하게 두지 않는다.
```

---

## 구현 시 추가 권고

### 1. 시간 기준은 monotonic clock을 사용한다

페이지 경과 시간, 콘텐츠 경과 시간, pending activation deadline, warmup deadline은 시스템 wall-clock이 아니라 monotonic clock 기반으로 계산하는 편이 안전하다.

단, 스케줄 시각 비교 자체는 wall-clock이 필요하다.

### 2. first-frame-ready 이벤트가 플랫폼마다 다를 수 있음을 고려한다

예:

- mpv/libmpv: file-loaded, playback-restart, video-reconfig, rendered frame 시점 조합
- AVFoundation: preroll completion + first video output readiness
- GStreamer: preroll-handoff 또는 state change + first sample
- Android: onPrepared + rendering start 조합

중요한 것은 API 이름이 아니라 **실제 화면에 올릴 준비가 되었는가**다.

### 3. 디버그 스냅샷은 반드시 남긴다

현재 코드처럼 아래 정보는 항상 모을 수 있어야 한다.

- layout 이름
- layout 상태
- slot index
- slot 상태
- 현재 콘텐츠 이름
- 다음 콘텐츠 이름
- 현재 콘텐츠 elapsed/duration
- visible 여부

이 데이터는 포팅 시 문제를 잡는 핵심 도구다.

### 4. 준비 중 실패한 단일 슬롯 때문에 전체 페이지를 깨지 말지 여부를 명시한다

제품 요구에 따라 아래 중 하나를 명시적으로 선택해야 한다.

- 정책 A: 하나라도 실패하면 layout 전체 전환 금지
- 정책 B: 실패 슬롯만 비활성화하고 나머지 슬롯으로 page 진행 허용

현재 코드 의미상은 빈 슬롯 허용 경향이 있으나, 구현 시 어느 정책을 택했는지 반드시 명시하고 테스트해야 한다.

---

## 이 문서를 사용하는 방법

- 실제 구현을 시킬 때는 위의 `코딩 에이전트에게 그대로 전달할 최종 프롬프트` 블록을 그대로 전달한다.
- 구현 전에 반드시 현재 코드를 다시 읽게 한다.
- 다른 OS 구현이어도 동작 의미는 이 문서를 우선 기준으로 잡는다.
- 플랫폼별 미디어 API 차이는 허용하지만, 상태 전이와 준비 기준 차이는 허용하지 않는다.
