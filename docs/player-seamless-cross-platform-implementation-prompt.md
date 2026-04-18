# Windows 기준 플레이어 심리스 재생 크로스플랫폼 구현 명세

## 1. 문서 성격과 최우선 기준

이 문서는 `Player/Windows/NewHyOn_Player`의 현재 구현을 직접 다시 읽어 추출한 **실제 동작 의미**만을 기준으로 정리한, 심리스 재생 엔진의 **권위 있는 기준 문서**다.

이 문서의 목적은 두 가지다.

- 현재 Windows 플레이어가 이미 수행하는 심리스 재생 의미를 손상 없이 고정한다.
- 그 의미를 macOS, Linux, Android, 임베디드 Linux 등 다른 OS로 옮겨도 상태 전이, 준비 기준, 전환 기준, 예외 처리 기준이 달라지지 않게 만든다.

이 문서는 아이디어 메모가 아니다. 이 문서는 구현 통제 문서다.

따라서 다음 원칙을 강제한다.

- **권위 기준은 오직 Windows 구현이다.** Android 구현은 이 문서의 근거가 아니다.
- 다른 OS 구현은 UI 툴킷과 미디어 백엔드만 교체할 수 있다.
- 상태 머신 의미, 레이아웃 역할, 슬롯 풀 구조, 준비 완료 기준, 전환 순서는 바꾸면 안 된다.
- 준비되지 않은 화면을 보여서 검은 화면이나 점프를 숨기는 방식은 금지한다.
- 기존 구현을 감싼 척만 하는 우회 구조는 금지한다.

---

## 2. 실제 근거 코드

이 문서의 모든 기준은 아래 Windows 파일에서 직접 추출한다.

- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessPlaybackContainer.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessLayoutRuntime.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessLayoutHost.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessContentSlot.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessMpvSurface.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackCoordinator.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackContracts.cs`
- `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml.cs`
- `Player/Windows/NewHyOn_Player/MainWindow.xaml.cs`

다른 OS 구현자는 먼저 이 문서를 기준으로 구현하고, 이후 위 Windows 코드와 결과를 대조해 의미가 동일한지 검증해야 한다.

---

## 3. 한 줄 요약

Windows 심리스 재생은 다음 구조다.

- 레이아웃 3개를 앱 시작 시 한 번만 만든다.
- 각 레이아웃은 슬롯 6개를 앱 시작 시 한 번만 만든다.
- 각 슬롯은 surface 1개와 player 1개를 계속 재사용한다.
- 현재 페이지는 `ACTIVE`, 다음 일반 페이지는 `STANDBY`, 다음 스케줄 첫 페이지는 `RESERVED`에 준비한다.
- 페이지 plan은 백그라운드에서 만들고, 레이아웃 준비는 모든 슬롯의 첫 미디어 준비 완료를 기다린 뒤 `Ready`로 만든다.
- 활성 전환은 항상 `next 준비 완료 확인 → next를 앞으로 올림 → next 활성화 → 이전 active 비활성화` 순서로 수행한다.
- schedule switch와 playlist reload는 `Immediately`, `ContentEnd`, `PageEnd` 정책으로 동일하게 제어한다.
- stale 작업은 `playbackVersion`과 warmup state key로 무효화한다.

이 의미가 다른 OS에서도 동일하게 재현되어야 한다.

---

## 4. 용어 정의

### 4.1 레이아웃

페이지 하나를 재생하기 위한 전체 캔버스 런타임이다.

Windows 대응 객체:

- `SeamlessLayoutRuntime`
- `SeamlessLayoutHost`

### 4.2 슬롯

페이지 안의 개별 Media 영역을 재생하는 런타임이다.

Windows 대응 객체:

- `SeamlessContentSlot`
- `SeamlessMpvSurface`
- `MPVLibControl`

### 4.3 페이지 plan

페이지 정의를 실제 재생 가능한 슬롯 시퀀스로 바꾼 결과물이다.

Windows 대응 객체:

- `SeamlessPagePlan`
- `SeamlessSlotPlan`
- `SeamlessContentItem`

### 4.4 ACTIVE / STANDBY / RESERVED

레이아웃 역할은 항상 아래 셋 중 하나다.

- `ACTIVE`: 현재 화면에 노출되어 실제 시간 흐름이 진행 중인 레이아웃
- `STANDBY`: 다음 일반 페이지가 준비된 레이아웃
- `RESERVED`: 다음 스케줄 전환용 첫 페이지가 준비된 레이아웃

이 역할은 3-layout pool의 핵심이며, 다른 OS에서도 그대로 유지해야 한다.

---

## 5. Windows 구현에서 고정된 불변 조건

### 5.1 레이아웃 풀은 항상 3개다

Windows는 생성 시 아래 3개를 만든다.

- `Layout-A`
- `Layout-B`
- `Layout-C`

근거:

- `SeamlessPlaybackContainer` 생성자에서 `SeamlessLayoutRuntime` 3개를 즉시 생성한다.

규칙:

- 레이아웃은 앱 시작 후 한 번만 생성한다.
- 페이지 전환, 스케줄 전환, reload 중에 레이아웃을 새로 만들지 않는다.
- 역할만 교체한다.

### 5.2 슬롯 풀은 항상 레이아웃당 6개다

Windows는 각 `SeamlessLayoutRuntime` 생성 시 슬롯 6개를 즉시 만든다.

근거:

- `SeamlessLayoutRuntime` 생성자에서 `for (int i = 0; i < 6; i++)`로 슬롯을 생성한다.
- `PlaybackCoordinator.BuildPagePlan`도 Media 요소를 최대 6개까지만 사용하고, 부족하면 빈 슬롯으로 6개를 채운다.

규칙:

- 슬롯 수는 Windows 의미 기준으로 6이 고정이다.
- 다른 OS가 내부 상수를 바꾸려면 반드시 제품 상수로 고정하고, 런타임 중 동적 증가를 금지한다.
- 페이지마다 필요한 element를 기존 슬롯에 재바인딩해야 한다.

### 5.3 레이아웃과 슬롯은 빈 상태를 허용한다

Windows는 재생 가능한 콘텐츠가 없는 슬롯도 허용한다.

근거:

- `SeamlessContentSlot.PrepareAsync`는 `HasPlayableItems == false`이면 `Stop()` 후 `Ready`로 둔다.

규칙:

- 전체 페이지에서 일부 슬롯이 비어 있어도 나머지 슬롯으로 레이아웃을 준비할 수 있어야 한다.
- 빈 슬롯은 `Ready(empty)` 의미를 가진다.
- 빈 슬롯 때문에 전체 페이지를 실패 처리하지 않는다.

### 5.4 레이아웃 상태는 실제 코드 기준으로 아래 4개다

Windows 공개 상태:

- `Idle`
- `Preparing`
- `Ready`
- `Active`

근거:

- `PlaybackContracts.cs`의 `SeamlessLayoutState`

다른 OS 구현 규칙:

- 외부에 보이는 레이아웃 상태 의미는 이 4개를 유지한다.
- 내부 fault 추적이 필요하면 별도 진단 필드로 분리하고, 공개 전이 의미를 바꾸지 않는다.

### 5.5 슬롯 상태는 실제 코드 기준으로 아래 5개다

Windows 공개 상태:

- `Idle`
- `Preparing`
- `Ready`
- `Active`
- `Error`

근거:

- `PlaybackContracts.cs`의 `SeamlessSlotState`

---

## 6. Windows 페이지 plan 생성 규칙

Windows에서 페이지 plan 생성 책임은 `PlaybackCoordinator`가 가진다.

### 6.1 plan 기본값

- `PlaylistName`: 요청된 플레이리스트 이름, null이면 빈 문자열
- `PageName`: 페이지 이름, null이면 빈 문자열
- `CanvasWidth`: `PIC_CanvasWidth > 0`이면 그 값, 아니면 `1920`
- `CanvasHeight`: `PIC_CanvasHeight > 0`이면 그 값, 아니면 `1080`
- `DurationSeconds`: `page playtime`의 합, 최소 `1`

### 6.2 Media 요소 선택 규칙

페이지의 `PIC_Elements`에서 아래 조건을 만족하는 요소만 사용한다.

- `EIF_Type`가 파싱 가능해야 한다.
- `DisplayType.Media`여야 한다.
- `ScrollText`, `WelcomeBoard`는 제외한다.
- `EIF_ZIndex` 오름차순으로 정렬한다.
- 앞에서부터 최대 6개만 사용한다.

6개를 초과하면 로그를 남기고 앞 6개만 사용한다.

### 6.3 슬롯 geometry 규칙

각 슬롯 plan에는 아래 값이 그대로 들어간다.

- `ElementName`
- `IsMuted`
- `Width`
- `Height`
- `Left`
- `Top`
- `ZIndex`

### 6.4 콘텐츠 제외 규칙

아래 콘텐츠는 페이지 plan에 포함하면 안 된다.

- 콘텐츠 객체가 null인 경우
- `CIF_FileName`이 비어 있는 경우
- `CIF_PlayMinute == "00"` 이고 `CIF_PlaySec == "00"`인 경우
- 기간 제한에 의해 오늘 날짜가 허용 구간 밖인 경우
- `CIF_ContentType` 파싱 실패
- 실제 파일이 존재하지 않는 경우
- 파일 길이가 0인 경우

### 6.5 기간 제한 규칙

Windows는 `owner.TryGetContentPeriod`로 기간 정책을 조회한다.

- 시작일 파싱 실패 시 `DateTime.MinValue`
- 종료일 파싱 실패 시 `DateTime.MaxValue`
- 오늘 날짜가 `[start.Date, end.Date]` 밖이면 제외

### 6.6 콘텐츠 길이 계산 규칙

#### 이미지

- configured duration = `PlayMinute * 60 + PlaySec`
- 최소 1초
- actual duration은 별도로 계산하지 않는다.
- 상위 슬롯/레이아웃 타이머가 전환을 제어한다.

#### 비디오

- configured duration을 먼저 계산한다.
- `MediaTools.GetVideoDuration(filePath)`로 actual duration을 구한다.
- actual duration 계산 실패 시 로그를 남기고 configured duration을 actual duration 대용으로 사용한다.

### 6.7 비디오 loop / 종료 규칙

Windows는 `SeamlessContentItem`에 아래 제어값을 미리 계산한다.

- `ShouldLoop`
- `TransitionByTimer`
- `LoopDisableAfterEndCount`
- `TransitionEndEventCount`

이 값의 의미는 아래와 같다.

#### case 1. configured duration < actual duration

- `ShouldLoop = false`
- `TransitionByTimer = true`
- 비디오 natural end를 기다리지 않는다.
- configured duration 시점에서 타이머 기준으로 다음 콘텐츠로 넘어간다.

#### case 2. configured duration == actual duration

- `ShouldLoop = false`
- `TransitionByTimer = false`
- natural end 1회를 기준으로 닫는다.

#### case 3. configured duration > actual duration 이고 나머지가 0

- `ShouldLoop = true`
- `TransitionByTimer = false`
- `fullPlaybackCount = configured / actual`
- 마지막 한 바퀴 직전까지만 loop를 유지한다.
- 최종 종료는 natural end 횟수로 맞춘다.

#### case 4. configured duration > actual duration 이고 나머지가 0이 아님

- `ShouldLoop = true`
- `TransitionByTimer = true`
- configured duration 시점까지 loop 재생하다가 타이머 기준으로 다음 콘텐츠로 넘어간다.

다른 OS 구현도 이 의미를 그대로 따라야 한다.

---

## 7. Windows 준비 완료 기준

### 7.1 준비 완료는 단순 파일 open 성공이 아니다

Windows에서 슬롯 준비 완료 기준은 `surface.MediaLoaded`다.

근거:

- `MPVLibControl`이 `MpvPlayer.MediaLoaded`를 `FileLoadedEvent`로 내보낸다.
- `SeamlessMpvSurface`가 이를 `MediaLoaded` 이벤트로 전달한다.
- `SeamlessContentSlot`은 `Preparing` 상태에서 `Surface_MediaLoaded`를 받으면 `Ready`로 전이한다.

즉 다른 OS 구현도 아래를 만족해야만 `Ready`로 간주한다.

- 해당 첫 콘텐츠가 실제 백엔드에 로드되어 있어야 한다.
- 즉시 `seek/start/switch`가 가능한 상태여야 한다.
- 단순히 파일 경로 검증만 끝난 상태는 `Ready`가 아니다.

### 7.2 슬롯 준비 타임아웃

Windows는 슬롯 준비에 `5000ms` 타임아웃을 둔다.

근거:

- `SeamlessContentSlot.PrepareAsync`

규칙:

- 준비 완료 이벤트가 5초 안에 오지 않으면 오류로 간주한다.
- 준비 실패 시 slot은 `Error`, surface는 `Stop()` 처리한다.
- 다른 OS도 동일한 의미의 prepare watchdog을 둬야 한다.

### 7.3 이미지 준비 기준

Windows mpv 경로에서는 이미지도 player playlist로 다룬다.

- 단일 load 시 `item.DurationSeconds`를 `ImageDuration`으로 설정할 수 있다.
- playlist 제어 시 이미지의 내부 자동 종료를 막기 위해 `image-display-duration = 86400`을 사용한다.
- 실제 전환 시점은 슬롯/레이아웃 타이머가 통제한다.

따라서 다른 OS에서는 아래 규칙을 만족해야 한다.

- 이미지 첫 결과가 surface에 배치 가능한 상태여야 `Ready`
- 이미지 표시 길이는 상위 엔진이 통제해야 함
- 백엔드 내부 자동 종료에 재생 흐름을 맡기면 안 됨

### 7.4 비디오 준비 기준

Windows 문맥에서 비디오 `Ready`는 최소한 아래 의미를 가진다.

- 첫 아이템이 mpv에 로드되었다.
- 즉시 `SetPlaylistIndex`, `SeekToStart`, `Play`가 가능한 상태다.

다른 OS에서는 이와 동등한 조건을 만들어야 한다.

- 첫 프레임이 이미 surface에 준비되어 있거나
- 즉시 start 시 첫 프레임 표시가 보장되는 상태

---

## 8. 슬롯 런타임 명세

Windows에서 슬롯 책임은 `SeamlessContentSlot`이 가진다.

### 8.1 슬롯 생성 시 고정되는 것

- surface 1개 생성
- `MediaLoaded` 이벤트 연결
- 이후 surface를 재사용

다른 OS도 슬롯당 media surface와 player를 1개씩 고정 생성해야 한다.

### 8.2 `PrepareAsync(plan, preserveAspectRatio)` 의미

1. 현재 plan을 저장한다.
2. `currentItemIndex = 0`으로 초기화한다.
3. elapsed/duration, `isActive`, `playlistPrepared`, `appliedLoopState`를 초기화한다.
4. UI thread에서 아래 geometry를 적용한다.
   - `Canvas.Left`
   - `Canvas.Top`
   - `Canvas.ZIndex`
   - `Width`
   - `Height`
   - mute
   - aspect policy
   - hidden 상태
5. playable item이 없으면 `Stop()` 후 `Ready`로 종료한다.
6. playable item이 있으면 `Preparing`으로 전이한다.
7. UI thread에서 `LoadPlaylist(autoPlay: false)`를 호출한다.
8. `MediaLoaded` 또는 5초 timeout을 기다린다.
9. `MediaLoaded`가 먼저 오면 `Ready`
10. timeout이면 `Error` 후 `surface.Stop()`

### 8.3 `Activate()` 의미

활성화는 준비된 슬롯을 실제 재생 상태로 올리는 단계다.

Windows 순서:

- playable item이 없으면 surface를 숨기고 `Active`로 둔다.
- playable item이 있으면 `isActive = true`, 상태를 `Active`로 둔다.
- UI thread에서 `surface.ShowSurface()`를 먼저 호출한다.
- 아직 playlist 준비가 안 됐으면 `SwitchToCurrentItem(true)`로 즉시 진입한다.
- 이미 준비가 되어 있으면 `SeekToStart()`를 시도한다.
- seek 실패 시 `SwitchToCurrentItem(true)`로 복구한다.
- 성공 시 현재 아이템 loop 상태를 강제로 반영하고 `Play()`한다.

다른 OS 구현도 다음 의미를 보장해야 한다.

- 활성화 시점에 이미 준비된 첫 결과를 즉시 화면에 올릴 수 있어야 한다.
- 활성화 후에야 비로소 시간 흐름을 시작해야 한다.
- 이전 active를 내리기 전에 next를 먼저 보여줄 수 있어야 한다.

### 8.4 `Deactivate()` 의미

Windows 순서:

- `isActive = false`
- UI thread에서 `surface.HideSurface()`
- surface가 재생 중이면 `Pause()`
- slot state가 `Error`가 아니면 `Ready`

즉 deactivation은 destroy가 아니라 pause + hidden + ready 복귀다.

### 8.5 `ApplyPlaybackPosition(layoutElapsed)` 의미

슬롯은 독자 타이머를 갖지 않는다. 레이아웃 elapsed를 기준으로 현재 아이템을 계산한다.

Windows 순서:

1. 현재 슬롯이 playable이고 active가 아니면 무시한다.
2. `ResolvePlaybackPosition`으로 target index와 item elapsed를 계산한다.
3. `currentItemElapsedMilliseconds`, `currentItemDurationMilliseconds`를 갱신한다.
4. target index가 current index와 같으면 loop 상태만 갱신한다.
5. 다르면 `SwitchToCurrentItem(true)`를 호출한다.
6. 성공 시 `surface.ShowSurface()`, state를 `Active`로 유지한다.

### 8.6 인덱스 계산 규칙

Windows는 layout elapsed를 slot cycle duration으로 나눈 나머지를 사용한다.

- cycle duration = 모든 item duration 합
- 해당 cycle 안에서 누적 duration을 따라 target index를 찾는다.
- 끝까지 못 찾으면 마지막 item으로 clamp한다.

즉 slot은 페이지 전체 시간축 위에서 deterministic하게 움직인다.

### 8.7 loop on/off 실시간 제어 규칙

Windows는 아이템이 바뀔 때뿐 아니라 같은 아이템 재생 중에도 현재 남은 시간에 따라 loop 상태를 갱신한다.

핵심 규칙:

- 비디오가 아니면 loop 없음
- `ShouldLoop == false`면 loop 없음
- `TransitionByTimer == true`면 마지막까지 loop 유지
- `TransitionByTimer == false`이면 남은 configured 시간이 actual duration보다 클 때만 loop 유지

즉 exact multiple case에서는 마지막 한 바퀴 진입 전에 loop가 꺼져야 한다.

### 8.8 sync status 기준

슬롯은 아래 정보를 계산할 수 있어야 한다.

- `ElementName`
- `CurrentContentName`
- `NextContentName`
- `CurrentIndex`
- `NextIndex`
- `ElapsedSeconds`
- `DurationSeconds`
- `IsVisible`

이 값은 다른 OS에서도 동일 필드명 또는 동등 의미로 유지해야 한다.

---

## 9. 레이아웃 런타임 명세

Windows에서 레이아웃 책임은 `SeamlessLayoutRuntime`이 가진다.

### 9.1 생성 시 고정되는 것

- `SeamlessLayoutHost` 1개 생성
- 슬롯 6개 생성
- host에 슬롯 surface들을 attach
- `MultimediaTimer` 1개 생성
- 초기 상태 `Idle`

### 9.2 `PrepareAsync(plan, viewportWidth, viewportHeight, preserveAspectRatio)` 의미

Windows 순서:

1. playback timer를 정지한다.
2. `CurrentPlan = plan`
3. 상태를 `Preparing`으로 둔다.
4. plan이 null이면 `Clear()` 후 종료한다.
5. UI thread에서 host presentation을 구성한다.
   - canvas size 적용
   - viewport scaling 적용
   - host를 hidden + opacity 0으로 유지
6. 모든 슬롯에 대응 slot plan을 주입하고 병렬 prepare를 시작한다.
7. 모든 slot prepare가 끝날 때까지 기다린다.
8. 완료되면 `Ready`

### 9.3 presentation transform 규칙

Windows `SeamlessLayoutHost.ConfigurePresentation` 기준:

#### preserve aspect ratio = true

- `scale = min(viewportWidth / canvasWidth, viewportHeight / canvasHeight)`
- scale이 비정상이면 `1.0`
- `offsetX = (viewportWidth - canvasWidth * scale) / 2`
- `offsetY = (viewportHeight - canvasHeight * scale) / 2`
- `ScaleTransform(scale, scale)` 후 `TranslateTransform(offsetX, offsetY)`

#### preserve aspect ratio = false

- `scaleX = viewportWidth / canvasWidth`
- `scaleY = viewportHeight / canvasHeight`
- scale이 비정상이면 각각 `1.0`
- `ScaleTransform(scaleX, scaleY)`

공통 규칙:

- `RenderTransformOrigin = (0, 0)`
- hidden 상태에서 준비한다.

다른 OS도 동일한 geometry 의미를 재현해야 한다.

### 9.4 `Activate()` 의미

Windows 순서:

1. plan이 없으면 아무것도 하지 않는다.
2. UI thread에서 host를 `Visible`, `Opacity = 1.0`으로 만든다.
3. 모든 슬롯 `Activate()` 호출
4. 상태를 `Active`
5. `StartPlaybackFromOffset(0)` 호출

### 9.5 `Deactivate()` 의미

Windows 순서:

1. playback timer를 정지한다.
2. 모든 슬롯 `Deactivate()` 호출
3. UI thread에서 host를 hidden + opacity 0으로 만든다.
4. `CurrentPlan != null`이면 `Ready`, 아니면 `Idle`

### 9.6 `Clear()` 의미

Windows 순서:

- timer 정지
- `CurrentPlan = null`
- 모든 슬롯 `Stop()`
- host 숨김
- canvas size 0
- presentation reset
- 상태 `Idle`

### 9.7 시간축 규칙

Windows는 레이아웃 단위 단일 시계를 사용한다.

- timer period = `16ms`
- stopwatch + base offset으로 elapsed 계산
- page duration을 초과하면 clamp
- display용 elapsed는 `duration - 1ms`까지 허용하여 마지막 프레임에서 즉시 page complete가 나지 않게 한다.

즉 다른 OS도 슬롯마다 독립 시계를 두지 말고 레이아웃 공통 시간축을 사용해야 한다.

### 9.8 page complete 규칙

- elapsed >= page duration 이면 timer stop
- `PlaybackCompleted`는 한 번만 발생해야 한다.

### 9.9 pulse 규칙

Windows는 pulse를 아래 두 경우에만 발생시킨다.

- `IsSecondTick`: elapsed second가 변경된 경우
- `IsContentBoundary`: primary sync slot의 current index가 바뀐 경우

둘 다 아니면 pulse를 발생시키지 않는다.

### 9.10 primary sync slot 선택 규칙

Windows는 아래 우선순위로 primary sync status를 선택한다.

1. playable item이 있으면서 현재 콘텐츠가 비디오인 첫 슬롯
2. playable item이 있는 첫 슬롯
3. 없으면 null

이 우선순위는 다른 OS에서도 유지해야 한다.

---

## 10. 오케스트레이터 명세

Windows에서 상위 오케스트레이션 책임은 `SeamlessPlaybackContainer`가 가진다.

### 10.1 생성 시 고정되는 것

- `PlaybackCoordinator` 1개
- 레이아웃 3개
- 각 레이아웃의 `PlaybackCompleted`, `PlaybackPulse` 이벤트 연결

### 10.2 `Initialize()` 의미

Windows는 최초 한 번만 다음을 수행한다.

- host canvas children clear
- 3개 host를 canvas에 추가
- 각 host의 좌표를 `(0, 0)`으로 둔다.
- 초기 z-index를 부여한다.

즉 다른 OS도 3개 presentation host를 앱 시작 시점에 attach해야 한다.

### 10.3 stale 무효화 규칙

Windows는 orchestration 차원에서 `playbackVersion`을 사용한다.

- `ShowPage`, `PlayNextPage`, `HideAll`, `StopAll`에서 version이 증가한다.
- 비동기 prepare 완료 후 항상 현재 version과 비교한다.
- layout active 여부 판정 시 `layoutActivationVersions[layoutIndex]`와 현재 `playbackVersion`을 비교한다.
- warmup은 `requestedWarmScheduleStateKey`, `requestedWarmSchedulePlaylist`, `requestedWarmScheduleAutoSwitch`로 중복과 stale를 막는다.

다른 OS 구현 규칙:

- 최소한 orchestration level에서는 `playbackVersion`과 동등한 global generation이 필수다.
- 권장사항으로 page build, layout prepare, slot prepare generation을 더 세분화해도 좋다.
- 단, 오래된 callback이 현재 active를 덮어쓰지 못해야 한다는 의미는 반드시 동일해야 한다.

### 10.4 current page 표시 요청 처리

Windows 흐름:

1. `ShowPage` 또는 `PlayNextPage`가 `playbackVersion`을 증가시킨다.
2. current page plan과 next page plan을 백그라운드에서 병렬 생성한다.
3. current plan을 담을 activation target layout을 결정한다.
4. 그 레이아웃이 이미 같은 plan으로 `Ready`면 재사용한다.
5. 아니면 `PrepareLayoutAsync`로 준비한다.
6. target layout을 앞으로 올린다.
7. target layout을 `Activate()`한다.
8. 이전 active가 있으면 그 뒤에 `Deactivate()`한다.
9. 남은 레이아웃 하나에 next plan을 standby로 준비한다.
10. standby는 준비 후 `Deactivate()` 상태로 대기한다.

### 10.5 activation target 선택 규칙

Windows `GetActivationLayoutIndex` 순서:

1. `nextPageLayoutIndex`가 유효하고 active가 아니며 `Ready`이고 plan이 일치하면 그것을 사용
2. active도 nextPage도 아닌 레이아웃 중 `Ready`이고 plan 일치하는 것을 사용
3. active가 아직 없으면 index 0 사용
4. active와 reserved를 피할 수 있으면 그 레이아웃 사용
5. 그래도 없으면 active가 아닌 아무 레이아웃 사용

즉 이미 준비된 standby를 최우선 재사용한다.

### 10.6 standby 선택 규칙

Windows `GetStandbyLayoutIndex` 순서:

- target layout이 아니고 reserved도 아닌 첫 레이아웃
- 없으면 target이 아닌 첫 레이아웃

### 10.7 reserved 선택 규칙

Windows `GetReservedLayoutIndex` 순서:

- initialized 상태여야 함
- active가 있어야 함
- active와 nextPage가 아닌 남은 1개를 사용

이것이 실제 3-layout pool 의미다.

### 10.8 atomic 전환 순서

다른 OS 구현은 아래 순서를 반드시 지켜야 한다.

1. current plan을 담을 next layout 준비 완료 확인
2. next layout이 첫 아이템 ready 상태인지 확인
3. next layout host를 front로 올림
4. next layout 활성화
5. 이전 active layout 비활성화
6. current의 next page plan을 빈 레이아웃에 standby로 준비
7. 별도 reserved warmup이 있으면 남은 레이아웃 역할을 유지

금지:

- 이전 active를 먼저 내린 뒤 next 준비 시작
- 새 레이아웃 인스턴스를 임시 생성해 겹쳐 붙이는 방식

---

## 11. 스케줄 전환과 warmup 명세

### 11.1 스케줄 평가는 throttled polling이다

Windows는 `EvaluateSchedule(force)`를 사용하며 다음 제한이 있다.

- 강제 평가가 아니면 마지막 평가 후 `700ms` 이내 중복 평가 금지
- 현재 시각 `DateTime.Now` 기준으로 `ScheduleEvaluatorService.Evaluate` 호출

### 11.2 스케줄 결정 결과 해석 규칙

#### 결과가 없거나 playlist가 비어 있으면

- pending schedule clear
- requested warm state clear
- reserved layout state clear

#### 결과 playlist가 현재와 같으면

- pending schedule clear
- 상태 키를 `KEEP|scheduleId|playlist`로 갱신
- `NextPlaylistName`과 `NextSwitchAt`이 있으면 upcoming warmup 평가

#### 결과 playlist가 현재와 다르면

- `pendingSchedulePlaylist` 설정
- `pendingScheduleId` 설정
- 상태 키를 `PENDING|scheduleId|playlist`로 저장
- reserve warmup을 즉시 시작

### 11.3 warmup 조건

Windows는 아래 두 경우 reserve warmup을 수행한다.

#### 이미 다른 playlist로 전환 예약된 경우

- 즉시 warmup 시작
- 준비 완료 후 `autoSwitchWhenReady = true`

#### 아직 현재 playlist 유지 중이지만 다음 스케줄이 1분 이내인 경우

- `NextSwitchAt <= now + 1 minute`면 warmup 시작
- `autoSwitchWhenReady = false`
- 실제 전환 시점까지 reserve 상태 유지

### 11.4 warmup 내용

Windows `WarmupPendingScheduleLayout` 의미:

1. 대상 playlist가 비어 있으면 중단
2. playlist에 playable content가 없으면 중단
3. active가 아직 없으면 중단
4. 이미 같은 playlist의 prepared reserved layout이 있으면 재사용
5. 중복 요청이면 무시
6. playlist의 첫 페이지 plan을 생성
7. reserved layout을 선택
8. 해당 layout을 prepare
9. 준비 후 즉시 `Deactivate()`해서 hidden ready 상태로 유지
10. `reservedLayoutIndex`, `reservedLayoutStateKey`, `reservedLayoutPlaylist`를 기록
11. `autoSwitchWhenReady`면 dispatcher에서 전환 시도

### 11.5 prepared schedule switch 시작 조건

Windows는 다음 조건을 모두 만족해야 prepared schedule switch를 즉시 시작한다.

- `pendingSchedulePlaylist == target playlist`
- reserved layout이 해당 playlist로 `Ready`
- `ApplyPendingSchedulePlaylist()` 성공

성공하면 `PlayNextPage()`를 호출한다.

### 11.6 schedule switch 적용 조건

Windows `TryApplyScheduledSwitch`는 아래 조건을 모두 통과해야 한다.

1. pending schedule이 있어야 한다.
2. `SwitchTiming` 정책이 허용해야 한다.
3. initialized 상태에서 active가 있고, prepared reserved layout이 아직 없으면 전환하지 않고 warmup만 다시 요청한다.
4. playlist가 실제로 playable해야 한다.
5. 파일이 없으면 전환하지 않고 다운로드 재시도만 트리거한다.

### 11.7 missing content 처리 규칙

Windows는 스케줄 대상 playlist에 playable content가 없으면 전환하지 않는다.

- 로그를 남긴다.
- `owner.CommandService.EnsurePlaylistDownloadFromCache(playlistName)`를 호출해 재시도한다.
- active 재생은 유지한다.

즉 “전환 시점이 왔으니 일단 검은 화면으로 바꾸고 나중에 덮는다”는 방식은 금지다.

---

## 12. playlist reload 명세

Windows는 schedule switch와 별도로 pending playlist reload를 가진다.

### 12.1 reload 요청

- `pendingPlaylistReload`
- `pendingPlaylistReloadReason`

을 저장한다.

### 12.2 `SwitchTiming == Immediately`

- 예약하지 않고 즉시 현재 playlist를 다시 load
- `UpdateCurrentPageListName(playlistName)`
- `PlayNextPage()`

### 12.3 `ContentEnd`, `PageEnd`

- 예약만 해 둔다.
- 허용 시점이 오기 전에는 적용하지 않는다.

### 12.4 reload 적용 조건

Windows `TryApplyPendingPlaylistReload`는 아래를 요구한다.

- pending reload가 존재해야 함
- timing 정책이 허용해야 함
- `playerInfo.PIF_CurrentPlayList`와 pending reload playlist가 같아야 함

적용되면:

- pending reload clear
- 현재 playlist 다시 load
- 다음 페이지부터 재생을 시작

---

## 13. 페이지 경계와 콘텐츠 경계

이 문서는 페이지 경계와 콘텐츠 경계를 반드시 분리한다.

### 13.1 콘텐츠 경계

같은 페이지 내부에서 현재 콘텐츠에서 다음 콘텐츠로 넘어가는 시점이다.

Windows 기준:

- primary sync slot의 `CurrentIndex`가 바뀌는 순간
- 레이아웃 pulse의 `IsContentBoundary = true`

### 13.2 페이지 경계

현재 페이지 전체 재생이 끝나는 시점이다.

Windows 기준:

- `CurrentElapsedMilliseconds >= CurrentPlan.DurationSeconds * 1000`
- `PlaybackCompleted` 발생

### 13.3 전환 정책 적용 규칙

Windows는 `SwitchTiming`을 아래처럼 동일하게 사용한다.

- `Immediately`: 준비되면 다음 평가 pulse에서 즉시 허용
- `ContentEnd`: `IsContentBoundary == true`에서만 허용
- `PageEnd`: `PlaybackCompleted`에서만 허용

schedule switch와 playlist reload 모두 이 정책을 동일하게 따른다.

중요:

- Windows의 `Immediately`는 “언제든 임의의 중간 프레임에 즉시 갈아탄다”가 아니다.
- 실제로는 second tick 또는 content boundary pulse, 또는 page complete 처리 루프에서 허용된다.
- 다른 OS 구현도 이벤트 루프는 달라도 의미는 동일해야 한다.

---

## 14. sync 와 heartbeat 명세

### 14.1 sync 리더 정보

Windows는 active layout의 primary sync slot 상태를 sync 리더 기준으로 사용한다.

필수 필드:

- 현재 콘텐츠 이름
- 다음 콘텐츠 이름
- 현재 인덱스
- 다음 인덱스
- 현재 콘텐츠 경과 시간
- 현재 콘텐츠 총 길이
- 가시 여부

### 14.2 sync prepare / commit 규칙

Windows `MainWindow.HandleSyncLeaderTick` 기준:

- 남은 시간이 1초 이하이면 한 번만 `Prepare(nextIndex)`를 전송
- elapsed가 duration 이상이면 한 번만 `Commit(nextIndex)`를 전송

다른 OS 구현도 sync 리더 데이터가 같은 기준으로 생성되어야 한다.

### 14.3 heartbeat에 끊기면 안 되는 정보

Windows는 seamless 모드에서 아래 정보를 계속 노출한다.

- 현재 playlist 이름
- 현재 page 이름
- 다음 page 이름
- 현재 page elapsed seconds
- 현재 page duration seconds
- 한 페이지뿐인지 여부

준비 중인 standby 또는 reserved가 이 active 상태를 덮어쓰면 안 된다.

---

## 15. viewport 와 presentation 명세

Windows에서 viewport는 `MainWindow.GetSeamlessViewportSize()`가 제공한다.

우선순위:

1. `MainScrollViewer.ActualWidth/ActualHeight`
2. `DesignerCanvas.ActualWidth/ActualHeight`
3. `DesignerCanvas.Width/Height`
4. `g_FixedBaseWidth/Height`
5. 마지막 기본값 `1920x1080`

다른 OS 구현도 viewport 크기 결정을 명시적으로 해야 한다. 0 또는 음수 값에 기대면 안 된다.

---

## 16. 로컬 재생 복구 규칙

Windows는 데이터 부재 시 아래 순서로 playable playlist를 찾는다.

1. 현재 playlist
2. 기본 playlist
3. 로컬 DB에서 실제 재생 가능한 첫 playlist

근거:

- `EnsureLocalPlaybackReady()`

중요:

- 이것은 구조 실패를 덮는 fallback이 아니다.
- 이것은 로컬 데이터가 비었거나 현재 playlist에 playable content가 없는 상황에서의 데이터 복구다.
- 다른 OS 구현도 동일 우선순위를 유지해야 한다.

---

## 17. 절대 금지 사항

아래는 구현 금지다.

- 준비되지 않은 layout을 활성화하는 것
- 이전 active를 먼저 숨기고 다음 layout을 나중에 준비하는 것
- 페이지 전환마다 레이아웃을 새로 만드는 것
- 페이지 전환마다 슬롯 또는 player를 새로 만드는 것
- schedule switch 때 엔진 전체를 재기동하는 것
- 오래된 비동기 callback이 현재 active 상태를 덮는 구조
- UI thread에서 파일 스캔, 전체 plan 계산, 대량 메타데이터 계산을 수행하는 것
- 블로킹 `sleep`으로 재생 타이밍을 맞추는 것
- missing content 상황에서 검은 화면을 넣고 나중에 덮는 것
- 기존 구식 재생 경로로 몰래 우회하는 것
- Android 구현 의미를 Windows 기준보다 우선하는 것

---

## 18. 다른 OS로 옮길 때의 필수 구조

다른 OS 구현은 아래와 같이 분리한다.

### 18.1 플랫폼 독립 코어

책임:

- page plan 생성
- active / standby / reserved 역할 배정
- playback version 관리
- standby prepare / reserve warmup 스케줄링
- switch timing 정책 적용
- page boundary / content boundary 판단
- pending schedule / pending reload 관리
- sync leader 계산
- debug snapshot 수집

### 18.2 플랫폼 어댑터

책임:

- presentation host 생성과 z-order 변경
- viewport scale 적용
- slot surface attach / show / hide
- 미디어 load / playlist / seek / play / pause / stop / loop / mute
- `media loaded`, `first frame ready`, `natural end` 등 이벤트 전달

### 18.3 Windows 코드와 포팅용 역할 매핑

- `SeamlessPlaybackContainer` → playback engine / coordinator
- `PlaybackCoordinator` → page plan builder
- `SeamlessLayoutRuntime` → layout runtime
- `SeamlessLayoutHost` → presentation host
- `SeamlessContentSlot` → slot runtime
- `SeamlessMpvSurface` + `MPVLibControl` → media backend adapter
- `MainWindow` → platform integration bridge

### 18.4 권장 파일 경계

다른 OS에서도 최소한 아래 경계를 유지한다.

- `PlaybackEngine` 또는 `SeamlessPlaybackCoordinator`
- `PagePlanBuilder`
- `LayoutRuntime`
- `SlotRuntime`
- `PresentationHost`
- `MediaSurfaceAdapter`
- `Contracts`
- `DiagnosticsSnapshot`
- `PlatformBridge`

파일명은 OS에 맞게 조정해도 되지만 역할을 섞으면 안 된다.

### 18.5 media backend 어댑터 필수 기능

다른 OS 구현은 backend 종류와 무관하게 최소한 아래 의미를 제공해야 한다.

- `LoadPlaylist(paths, autoPlay)`
- `SwitchToIndex(index, autoPlay)`
- `SeekToStart()`
- `Play()`
- `Pause()`
- `Stop()`
- `SetLooping(bool)`
- `SetMuted(bool)`
- `ShowSurface()`
- `HideSurface()`
- `MediaLoaded` 또는 동등한 ready 이벤트

Windows 매핑은 아래와 같다.

- `SeamlessMpvSurface.LoadPlaylist`
- `SeamlessMpvSurface.SwitchToIndex`
- `SeamlessMpvSurface.SeekToStart`
- `SeamlessMpvSurface.Play`
- `SeamlessMpvSurface.Pause`
- `SeamlessMpvSurface.Stop`
- `SeamlessMpvSurface.SetLooping`
- `SeamlessMpvSurface.Configure`
- `SeamlessMpvSurface.ShowSurface`
- `SeamlessMpvSurface.HideSurface`
- `SeamlessMpvSurface.MediaLoaded`

다른 OS 구현은 메서드 이름이 달라도 되지만 의미는 이와 동일해야 한다.

### 18.6 thread model 분리 규칙

Windows 구현을 기준으로 다른 OS도 아래 분리를 지켜야 한다.

#### background 에서 해야 하는 것

- page plan 생성
- playable content 판정
- 파일 존재 확인
- 파일 길이 확인
- 비디오 actual duration 계산
- schedule evaluation
- warmup 대상 결정

#### UI thread 에서 해야 하는 것

- presentation host attach
- host visibility / opacity / z-order 변경
- viewport transform 적용
- slot geometry 적용
- surface show / hide
- backend load / switch / seek / play / pause / stop 호출

#### 금지

- UI thread에서 디스크 스캔
- UI thread에서 전체 page plan 계산
- UI thread에서 대량 duration 계산
- UI thread에서 blocking wait 또는 sleep

---

## 19. 모든 OS 구현자가 따라야 할 단계별 알고리즘

### A. 초기화

1. presentation host 3개를 만든다.
2. 각 host에 slot 6개를 attach한다.
3. 각 slot에 media surface 1개와 player 1개를 연결한다.
4. host는 모두 hidden 상태로 시작한다.
5. active, standby, reserved 인덱스를 아직 배정하지 않는다.

### B. 현재 페이지 표시 요청

1. global playback version을 증가시킨다.
2. current page와 next page를 결정한다.
3. 두 page plan을 백그라운드에서 병렬 생성한다.
4. current plan을 담을 activation target layout을 선택한다.
5. target이 이미 같은 plan으로 ready면 재사용한다.
6. 아니면 target layout을 prepare한다.
7. 준비 완료와 version 일치를 확인한다.
8. target layout을 front로 올린다.
9. target layout을 activate한다.
10. 이전 active를 deactivate한다.
11. 남는 일반 레이아웃에 next plan을 standby로 prepare한다.
12. standby는 hidden ready 상태로 둔다.
13. schedule warmup 대상이 있으면 reserved 레이아웃을 따로 유지한다.

### C. 레이아웃 준비

1. layout state를 `Preparing`
2. viewport transform 적용
3. slot plan 6개 주입
4. 각 slot prepare 병렬 실행
5. 모든 slot prepare 완료 대기
6. stale 아니면 `Ready`

### D. 슬롯 준비

1. geometry / mute / aspect 정책 적용
2. item list 비어 있으면 `Ready(empty)`
3. item이 있으면 `Preparing`
4. 첫 playlist load를 autoPlay false로 수행
5. media loaded 대기
6. 5초 timeout이면 `Error`
7. 성공이면 `Ready`

### E. 활성 전환

1. next layout ready 확인
2. next host를 front로 올림
3. next layout activate
4. old active deactivate
5. next를 active로 기록
6. old active가 비워졌으면 standby 또는 reserved로 재사용

### F. 레이아웃 시간 진행

1. monotonic 시계로 page elapsed 계산
2. 각 slot에 동일 elapsed 적용
3. primary slot 기준 pulse 생성
4. page duration 도달 시 page complete 발생

### G. 콘텐츠 전환

1. slot은 cycle duration에서 현재 item index를 계산
2. 인덱스가 바뀌면 backend playlist index를 전환
3. 같은 item이면 loop 상태만 필요 시 갱신
4. actual/configured duration 규칙으로 loop on/off를 조절

### H. schedule 평가

1. 평가 간격을 제한한다.
2. current schedule 유지인지, pending switch인지 판정한다.
3. 다른 playlist가 결정되면 pending schedule로 기록한다.
4. reserve layout warmup을 시작한다.
5. prepared reserve layout이 없으면 전환하지 않는다.
6. timing 정책이 허용될 때만 적용한다.

### I. playlist reload

1. pending reload를 기록한다.
2. timing 정책이 허용될 때까지 보류한다.
3. current playlist와 pending reload 대상이 일치할 때만 적용한다.
4. 첫 페이지부터 다시 준비한다.

### J. missing content

1. 대상 playlist에 playable content가 있는지 확인한다.
2. 없으면 현재 active를 유지한다.
3. 다운로드 또는 동기화 재시도만 요청한다.
4. 검은 화면이나 빈 layout으로 전환하지 않는다.

---

## 20. 진단 스냅샷 명세

다른 OS 구현도 아래 정보는 항상 수집 가능해야 한다.

레이아웃/슬롯별 필수 진단 필드:

- `LayoutName`
- `LayoutState`
- `SlotIndex`
- `SlotState`
- `ElementName`
- `CurrentContentName`
- `NextContentName`
- `ElapsedSeconds`
- `DurationSeconds`
- `IsVisible`

근거:

- `PlaybackDebugItem`

---

## 21. 테스트와 검증 기준

### 21.1 상태 전이 검증

- active / standby / reserved가 동시에 같은 인덱스를 가리키지 않는지
- stale version 작업이 현재 active를 덮지 않는지
- layout 준비 중 old callback이 new state를 오염시키지 않는지
- slot timeout 후 오류 surface가 남지 않는지

### 21.2 콘텐츠 길이 검증

- configured < actual video
- configured == actual video
- configured > actual 이고 divisible
- configured > actual 이고 non-divisible
- single image
- image sequence
- mixed image/video sequence

### 21.3 시각 검증

- active 전환 시 이전 active를 내리기 전에 next가 올라오는지
- black frame가 끼지 않는지
- image → image 전환 시 준비되지 않은 이미지를 노출하지 않는지
- image → video 전환 시 video ready 전 전환하지 않는지
- video → image 전환 시 image ready 전 전환하지 않는지

### 21.4 스케줄 검증

- 현재 schedule 유지
- 즉시 전환
- content end 전환
- page end 전환
- upcoming schedule 1분 이전 warmup
- prepared reserve layout이 없는 상태에서 switch 보류
- missing content로 switch 보류 + download retry

### 21.5 리소스 검증

- steady state에서 host 수가 늘지 않는지
- steady state에서 slot 수가 늘지 않는지
- steady state에서 player 수가 늘지 않는지
- 반복 전환 후 메모리 누수가 없는지
- `Deactivate`, `Clear`, 재준비 후 재사용이 정상인지

---

## 22. 코딩 에이전트에게 전달할 최종 구현 지시

아래 블록은 구현 작업을 맡길 때 그대로 사용한다.

```md
현재 저장소의 심리스 재생 의미는 오직 `Player/Windows/NewHyOn_Player`의 현재 구현을 기준으로 정의한다. Android 구현은 참고 근거로 사용하지 않는다.

반드시 먼저 아래 Windows 파일을 다시 읽고 시작하라.

- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessPlaybackContainer.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessLayoutRuntime.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessLayoutHost.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessContentSlot.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/SeamlessMpvSurface.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackCoordinator.cs`
- `Player/Windows/NewHyOn_Player/PlaybackModes/PlaybackContracts.cs`
- `Player/Windows/NewHyOn_Player/ContentControls/MPVLibControl.xaml.cs`
- `Player/Windows/NewHyOn_Player/MainWindow.xaml.cs`

구현 목표:

- Windows 구현의 심리스 재생 의미를 손상 없이 추출한다.
- 같은 의미를 macOS, Linux, Android, 임베디드 Linux 등 다른 OS에서도 재현 가능한 구조로 분리한다.
- 단, 권위 기준은 항상 Windows 구현 의미다.

절대 바꾸면 안 되는 것:

1. 레이아웃 풀은 항상 3개다.
2. 슬롯 풀은 항상 고정 생성이다.
3. 페이지 전환마다 레이아웃, 슬롯, player를 새로 만들면 안 된다.
4. current active, next standby, upcoming reserved 역할을 동시에 유지해야 한다.
5. prepare 완료 전에는 어떤 layout도 활성화하면 안 된다.
6. activation 순서는 반드시 `준비 확인 → front → activate → old deactivate`다.
7. schedule switch와 playlist reload는 동일한 `SwitchTiming` 정책으로 제어해야 한다.
8. stale 비동기 작업이 현재 active를 덮지 못하도록 generation/version guard를 둬야 한다.
9. missing content 상황에서 현재 active를 내리면 안 된다.
10. UI thread에서 파일 스캔, 전체 plan 계산, 대량 메타데이터 계산을 하면 안 된다.

반드시 구현해야 할 의미:

- page plan은 Media 요소를 Z-index 오름차순으로 최대 6개만 사용한다.
- 각 layout은 slot 6개를 고정 보유한다.
- 슬롯 준비 완료 기준은 실제 media loaded 또는 그와 동등한 backend ready signal이다.
- 레이아웃 시간축은 slot마다 따로 두지 말고 page 공통 monotonic time 하나로 계산한다.
- primary sync slot은 video 우선, 없으면 첫 playable slot이다.
- 비디오 configured/actual duration 규칙은 Windows의 `ShouldLoop`, `TransitionByTimer`, `LoopDisableAfterEndCount`, `TransitionEndEventCount` 의미를 그대로 유지한다.
- upcoming schedule이 1분 이내면 reserve layout에 첫 페이지를 미리 warmup해야 한다.
- prepared reserve layout이 없으면 스케줄 전환을 보류해야 한다.
- 로컬 재생 복구 우선순위는 `현재 playlist → 기본 playlist → 로컬에서 재생 가능한 첫 playlist`다.

구조 요구:

- playback engine / coordinator
- page plan builder
- layout runtime
- slot runtime
- presentation host
- media surface adapter
- contracts
- diagnostics snapshot
- platform bridge

최종 보고에는 반드시 아래를 포함하라.

1. Windows 의미를 기준으로 어떤 상태 머신을 구현했는지
2. 생성/수정한 파일 목록
3. Windows 의미와 플랫폼 어댑터 경계를 어떻게 분리했는지
4. prepare / activation / standby / reserve / warmup / schedule / reload를 어떻게 구현했는지
5. 어떤 검증 시나리오를 수행했고 결과가 어땠는지
6. 남은 리스크가 무엇인지
```

---

## 23. 문서 사용 규칙

- 다른 OS 구현자는 이 문서를 먼저 읽고 구현해야 한다.
- 구현 중 판단이 필요하면 항상 Windows 의미에 맞는 쪽을 선택한다.
- Android 또는 타 플랫폼의 기존 구현이 Windows 의미와 충돌하면 Windows 의미를 따른다.
- 이 문서와 실제 결과가 어긋나면 코드를 먼저 의심하고, 그 다음 문서를 교정한다.

이 문서의 기준은 “Windows 구현에서 직접 확인된 의미를 다른 OS에 오해 없이 옮기는 것”이다. 그 외의 임의 해석은 허용하지 않는다.
