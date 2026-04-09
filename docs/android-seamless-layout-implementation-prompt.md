# Android Seamless Layout Reuse 구현 프롬프트

## 목적

Android 플레이어의 기존 페이지 전환 구조를, `뷰 생성/제거/부착/분리` 없이 동작하는 **고정 레이아웃 재사용 방식**으로 전면 교체한다.

핵심 목표는 다음과 같다.

- 앱 시작 시 필요한 레이아웃과 뷰를 한 번만 생성한다.
- 이후 전환 시 `addView`, `removeView`, `removeAllViews`, 레이아웃 재생성, `MediaView` 재생성으로 전환하지 않는다.
- 모든 준비는 off-screen에서 끝낸다.
- 전환 시에는 이미 준비 완료된 레이아웃을 즉시 on-screen 위치로 이동시키고, 현재 레이아웃을 off-screen으로 내린다.
- 변경 가능한 속성은 `위치`, `크기`, `visibility`, `alpha`, `재생 상태(start/pause/stop/seek)`에 한정한다.
- 목적은 불필요한 객체 생성/제거를 없애고, 메모리 누수와 Surface 관련 크래시를 줄이며, 빠르고 안정적인 전환을 만드는 것이다.

---

## 구현 대상 코드 기준

현재 구현의 핵심 진입점과 충돌 지점은 아래 파일들이다.

- `Player/Android/Quber/Quber_Player/app/src/main/java/kr/co/turtlelab/andowsignage/AndoWSignage.java`
- `Player/Android/Quber/Quber_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/MediaView.java`
- `Player/Android/Krizer/Krizer_Player/app/src/main/java/kr/co/turtlelab/andowsignage/AndoWSignage.java`
- `Player/Android/Krizer/Krizer_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/MediaView.java`

현재 코드에서 특히 주의해야 할 지점:

- `AndoWSignage.java`
  - `activePageContainer`, `stagedPageContainer` 중심의 2-layout 구조가 이미 존재한다.
  - `createPageContainer()`, `buildPageRuntime()`, `schedulePrepareRuntime()`, `activatePreparedRuntime()` 계열이 현재 전환의 중심이다.
  - `runtime.container.removeAllViews()`와 `layout_root.removeAllViews()`가 아직 남아 있다.
  - `addBuiltElement()`에서 `MediaView`가 매번 새로 생성된다.
- `MediaView.java`
  - 이미 `prepareInitialContent()`, `showPreparedContent()`, `startPreparedPlayback()` 같은 “준비 후 시작” 개념이 일부 들어가 있다.
  - 이 구조를 버리지 말고 확장해, 완전한 고정 재사용 구조로 끌어올려야 한다.

중요:

- 구현 시 반드시 **현재 시점의 코드 전체를 다시 읽고** 작업한다.
- Quber와 Krizer는 동일 구조를 유지해야 한다.
- 한쪽만 바꾸고 다른 쪽을 놓치지 않는다.

---

## 절대 규칙

아래 규칙은 예외 없이 지켜야 한다.

1. 레이아웃은 총 3개를 앱 시작 시 한 번만 생성한다.
2. 각 레이아웃 안의 `MediaView` 4개도 앱 시작 시 한 번만 생성한다.
3. 전환 중 `addView/removeView/removeAllViews`로 페이지/컨텐츠 레이아웃을 갈아끼우지 않는다.
4. `MediaView` 인스턴스를 페이지 전환마다 새로 만들지 않는다.
5. 특별 스케줄은 별도 전용 페이지 엔진이 아니라, 3개 레이아웃 중 하나를 “미리 준비하는 버퍼 역할”로 돌리는 구조로 구현한다.
6. 특별 스케줄 시작 1분 전에 준비를 시작하고, 시작 시점에는 즉시 교체되어야 한다.
7. 특별 스케줄을 재생하기 시작한 레이아웃은 그 시점부터 일반 재생 레이아웃처럼 취급한다.
8. 전환 지연을 애니메이션이나 임의 fallback으로 숨기지 않는다.
9. 준비가 덜 된 레이아웃은 절대 on-screen으로 올리지 않는다.
10. 문제가 생기면 코드 구조를 먼저 의심하고, 임시 fallback을 추가하지 않는다.

---

## 목표 구조

### 레이아웃 풀

고정 레이아웃 3개를 유지한다.

- `layoutA`
- `layoutB`
- `layoutC`

각 레이아웃 내부에는 고정 `MediaView` 4개가 존재한다.

- `mediaSlot0`
- `mediaSlot1`
- `mediaSlot2`
- `mediaSlot3`

각 레이아웃은 시점에 따라 아래 역할 중 하나를 가진다.

- 현재 on-screen에서 재생 중
- 다음 일반 스케줄 페이지 준비 중
- 다음 특별 스케줄 페이지 준비 중
- 현재 대기 중

### 전환 원칙

- 모든 레이아웃은 부모에 붙어 있는 상태를 유지한다.
- off-screen 레이아웃도 실제 View tree 안에 존재한다.
- 레이아웃 전환은 아래만 사용한다.
  - `x/y` 또는 `translationX/translationY`
  - `layout params` 크기 갱신
  - `visibility`
  - `alpha`
  - `MediaView` 내부 재생 상태 제어

금지:

- 페이지 전환 시 컨테이너 삭제
- 페이지 전환 시 `MediaView` 새로 생성
- 전환 직후에 초기화부터 다시 시작하는 방식

---

## 특별 스케줄 동작 모델

### 기본 동작

- 현재 일반 스케줄이 재생 중일 때, 다른 한 레이아웃은 다음 일반 스케줄 준비에 사용한다.
- 나머지 한 레이아웃은 다음 특별 스케줄 준비 버퍼로 사용한다.

### 특별 스케줄 1분 전

- 특별 스케줄 시작 시각 기준 1분 전에 버퍼 레이아웃을 `PREPARING_SPECIAL` 상태로 전환한다.
- 특별 스케줄 페이지를 off-screen에서 완전히 준비한다.
- 준비 완료의 기준은:
  - 레이아웃 위치/크기 반영 완료
  - 4개 `MediaView`에 필요한 요소 매핑 완료
  - 이미지 첫 표시 가능 상태
  - 영상 첫 프레임 준비 완료 또는 즉시 start 가능한 상태

### 특별 스케줄 시작 시

- 준비 완료된 레이아웃을 즉시 on-screen으로 올린다.
- 이전 `ACTIVE` 레이아웃은 off-screen으로 내린다.
- 특별 스케줄을 시작한 레이아웃은 이제 `ACTIVE`가 된다.
- 이전 일반용 2개 중 하나를 다음 특별 스케줄 준비 버퍼로 재배정한다.

즉, “특별 스케줄 전용 레이아웃”이라는 고정 개념은 없고, 3개 레이아웃의 역할이 순환한다.

---

## 구현 프롬프트

아래 프롬프트를 그대로 사용해도 되고, 현재 코드 상태에 맞게 보강해서 사용해도 된다.

```md
현재 Android 플레이어의 페이지 전환 구조를 고정 레이아웃 재사용 구조로 전면 개편하라.

필수 요구사항:

1. 앱 시작 시 레이아웃 3개를 한 번만 생성한다.
2. 각 레이아웃 안의 MediaView 4개도 한 번만 생성한다.
3. 이후 어떤 페이지 전환에서도 addView, removeView, removeAllViews, MediaView 재생성 방식으로 처리하지 않는다.
4. 모든 다음 페이지/다음 스케줄 준비는 off-screen에서 끝낸다.
5. 전환 시에는 준비 완료된 레이아웃을 on-screen 위치로 이동시키고, 현재 레이아웃을 off-screen으로 내리는 방식으로만 처리한다.
6. 변경 가능한 것은 위치, 크기, visibility, alpha, 재생 상태뿐이다.
7. 특별 스케줄은 시작 1분 전에 off-screen에서 미리 준비한다.
8. 특별 스케줄 시작 시 즉시 교체하고 재생한다.
9. 특별 스케줄을 재생하기 시작한 레이아웃은 그 시점부터 일반 스케줄용 ACTIVE 레이아웃처럼 동작해야 한다.
10. 남은 일반 레이아웃 중 하나는 다시 다음 특별 스케줄 준비 버퍼 역할을 맡아야 한다.
11. fallback으로 기존 attach/detach/removeAllViews 흐름을 남기지 않는다.
12. Quber와 Krizer 양쪽에 동일한 구조를 반영한다.

현재 코드 기준으로 반드시 먼저 읽을 파일:

- Player/Android/Quber/Quber_Player/app/src/main/java/kr/co/turtlelab/andowsignage/AndoWSignage.java
- Player/Android/Quber/Quber_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/MediaView.java
- Player/Android/Krizer/Krizer_Player/app/src/main/java/kr/co/turtlelab/andowsignage/AndoWSignage.java
- Player/Android/Krizer/Krizer_Player/app/src/main/java/kr/co/turtlelab/andowsignage/views/MediaView.java

구현 지침:

- 기존 active/staged 2-container 개념을 3-layout pool 구조로 일반화한다.
- 레이아웃 역할 추적 구조를 도입한다.
- PageRuntime가 view list를 매번 새로 만드는 구조를 제거하거나 재사용 중심으로 치환한다.
- addBuiltElement()에서 MediaView를 매번 new 하지 말고, 고정 media slot에 데이터만 주입하는 방식으로 바꾼다.
- runtime.container.removeAllViews()와 layout_root.removeAllViews() 의존 흐름을 제거한다.
- 준비 완료 판정은 “첫 프레임/첫 표시 가능” 기준으로 엄격하게 잡는다.
- Surface 충돌과 detach 시점 정리를 고려해 MediaView stop/start 시퀀스를 명확하게 만든다.
- 특별 스케줄 예약, 취소, 변경, 일반 스케줄 복귀까지 상태 전이를 일관되게 유지한다.
- UI thread에서 무거운 작업을 몰아서 하지 말고, 준비 과정은 현재 코드의 staged prepare 흐름을 참고해 프레임 친화적으로 유지한다.

산출물:

1. 상태 머신 정의
2. 레이아웃 풀 구조
3. 현재 재생/다음 일반/다음 특별 준비 로직
4. 특별 스케줄 시작 1분 전 준비 로직
5. 전환 로직
6. 기존 removeAllViews/new MediaView 제거
7. Quber/Krizer 동시 반영
8. assembleDebug 검증
9. 변경 요약과 남은 리스크 보고
```

---

## 작업 체크리스트

### 1. 사전 분석

- [x] Quber `AndoWSignage.java` 전체를 읽는다.
- [x] Quber `MediaView.java` 전체를 읽는다.
- [x] Krizer 동일 파일을 읽어 차이가 있는지 확인한다.
- [x] 현재 `removeAllViews`, `addView`, `new MediaView`, `createPageContainer`, `buildPageRuntime`, `activatePreparedRuntime` 호출 지점을 모두 목록화한다.
- [x] 현재 특별 스케줄 판정과 일반 스케줄 판정 흐름을 분리해서 추적한다.

### 2. 역할 추적 구조 설계

- [x] 레이아웃 3개의 고정 식별자를 정의한다.
- [ ] 레이아웃 역할 추적 구조를 정의한다.
- [x] 현재 활성 레이아웃, 다음 일반 준비 레이아웃, 다음 특별 준비 레이아웃을 추적하는 구조를 만든다.
- [x] 역할 재배치 규칙을 문서화하고 코드에 반영한다.

### 3. 레이아웃 초기화

- [x] 앱 시작 시 레이아웃 3개를 생성한다.
- [x] 각 레이아웃 안에 `MediaView` 4개를 미리 생성한다.
- [x] 이 레이아웃들은 모두 부모에 붙인 채 유지한다.
- [x] 초기 상태는 1개 on-screen, 2개 off-screen으로 둔다.
- [x] 레이아웃 이동 유틸을 만든다.
  - `moveOnScreen`
  - `moveOffScreen`
  - `applyLayoutBounds`
  - `setLayoutVisible`

### 4. MediaView 재사용 구조

- [x] `MediaView`가 “데이터를 받아 자기 내부 상태만 갱신”하는 구조인지 점검한다.
- [x] 페이지 전환마다 `new MediaView(...)`가 호출되는 경로를 제거한다.
- [x] 각 `MediaView` slot에 element/content를 바인딩하는 API를 만든다.
- [x] slot 재사용 시 이전 재생 상태, 콜백, 핸들러, runnable이 남지 않도록 reset 규약을 만든다.
- [x] `MediaView` 내부에서 prepare와 start를 분리한 현재 구조를 유지/확장한다.

### 5. 페이지 준비 구조

- [x] 페이지 spec만 계산하고, 실제 View 인스턴스는 재사용하도록 분리한다.
- [x] 각 레이아웃의 4개 slot에 어떤 element가 들어갈지 매핑한다.
- [x] off-screen 준비 시 위치/크기/visibility까지 적용한다.
- [ ] 준비 완료 조건을 명확히 정의한다.
  - 이미지: 로드 완료
  - 영상: 첫 프레임 준비 완료 또는 즉시 시작 가능
- [ ] 준비가 덜 끝난 레이아웃은 절대 활성화하지 않는다.

### 6. 일반 스케줄 전환

- [ ] 현재 `ACTIVE` 레이아웃은 계속 재생한다.
- [ ] 다른 한 레이아웃에서 다음 일반 스케줄 페이지를 준비한다.
- [ ] 준비 완료되면 전환 시점까지 off-screen에서 대기한다.
- [ ] 전환 시 기존 `ACTIVE`를 off-screen으로 내리고 준비된 레이아웃을 on-screen으로 올린다.
- [ ] 전환 직후 역할을 재정렬한다.

### 7. 특별 스케줄 준비 및 전환

- [ ] 특별 스케줄 시작 시각 1분 전 준비 트리거를 넣는다.
- [ ] 버퍼 레이아웃을 `PREPARING_SPECIAL`로 지정한다.
- [ ] 특별 스케줄 페이지를 off-screen에서 완전히 준비한다.
- [ ] 시작 시각에 즉시 on-screen 전환한다.
- [ ] 특별 스케줄 레이아웃은 전환 직후 `ACTIVE`가 된다.
- [ ] 이전 일반용 레이아웃 중 하나를 다음 특별 준비용으로 다시 지정한다.
- [ ] 특별 스케줄이 취소/변경되면 준비 중 레이아웃을 안전하게 재사용 상태로 되돌린다.

### 8. 기존 구조 제거

- [ ] 페이지 전환 중심의 `removeAllViews` 흐름을 제거한다.
- [ ] `runtime.container.removeAllViews()` 의존 로직을 제거한다.
- [ ] `layout_root.removeAllViews()`에 의존한 플레이백 초기화 흐름을 제거한다.
- [ ] `addBuiltElement()`의 동적 View 생성 흐름을 제거하거나, 초기 1회 생성 후 재사용 구조로 치환한다.
- [ ] 기존 active/staged 2-container 전용 가정이 남아 있지 않도록 정리한다.

### 9. 안전성

- [ ] 전환 시 `SurfaceView`가 detach되며 크래시 나지 않도록 stop/start 시퀀스를 정리한다.
- [ ] prepare 중인 레이아웃과 재생 중인 레이아웃이 같은 자원을 충돌시키지 않는지 확인한다.
- [ ] handler/runnable/callback 누수가 없는지 확인한다.
- [ ] slot 재사용 시 이전 페이지의 콜백이 다음 페이지에 섞이지 않도록 차단한다.
- [ ] 빠른 스케줄 변경이 연속 발생해도 상태 머신이 깨지지 않도록 한다.

### 10. 특수 시나리오

- [ ] 앱 첫 실행
- [ ] 일반 스케줄만 존재
- [ ] 특별 스케줄 존재
- [ ] 특별 스케줄 연속 2개 존재
- [ ] 특별 스케줄 직전 내용 변경
- [ ] 특별 스케줄 취소
- [ ] 네트워크 갱신 후 페이지 데이터 변경
- [ ] USBP 진입/복귀
- [ ] 화면 크기 변경
- [ ] 플레이어 재시작 후 즉시 복구

### 11. 금지 체크

- [ ] 페이지 전환 경로에 `new MediaView(...)`가 남아 있지 않다.
- [ ] 페이지 전환 경로에 `removeAllViews()`가 남아 있지 않다.
- [ ] 레이아웃 전환 경로에 `addView/removeView`가 남아 있지 않다.
- [ ] “안 되면 기존 방식 fallback” 코드가 남아 있지 않다.

### 12. 검증

- [x] `Quber_Player assembleDebug` 성공
- [x] `Krizer_Player assembleDebug` 성공
- [ ] 일반 스케줄 전환 시 깜빡임 없는지 확인
- [ ] 특별 스케줄 1분 전 준비 로그 확인
- [ ] 특별 스케줄 시작 즉시 전환 확인
- [ ] 전환 후 이전 레이아웃이 off-screen으로 정상 대기하는지 확인
- [ ] 여러 번 전환 후 메모리/Surface 관련 크래시 없는지 확인

---

## 완료 조건

아래 조건이 모두 만족되어야 완료로 본다.

- 페이지 전환이 더 이상 attach/detach/removeAllViews 기반이 아니다.
- 레이아웃 3개와 각 `MediaView` 4개는 앱 시작 후 계속 유지된다.
- 일반/특별 스케줄 모두 off-screen 준비 후 on-screen 위치 전환으로 동작한다.
- 특별 스케줄 레이아웃은 고정 전용이 아니라 역할이 순환한다.
- Quber/Krizer 모두 동일하게 동작한다.
- 기존 방식 fallback 없이 구조적으로 일관되게 동작한다.
