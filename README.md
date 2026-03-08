# 게임 설계 예시

턴제 로그라이크 RPG를 개발하면서 고민했던 핵심 설계들을 정리한 레포지토리입니다.

실제 프로젝트는 비공개이며, 이 레포지토리에는 설계 의도를 설명하기 위한 핵심 코드가 정리되어 있습니다

---

## 프로젝트 개요

**장르**: 턴제 로그라이크 RPG  
**엔진**: Unity 6, C#  
**구조**: MVC 패턴 — Model(순수 C#), View(Unity), Controller(FSM)  
**팀**: 2인 개발

---

## 설계 1: Stack 기반 FSM

> [Examples/FSM/](Examples/FSM/)

### 문제

이 게임은 하나의 전투 화면 안에서도 여러 레벨의 흐름이 동시에 존재합니다.

```
마을 → 던전 탐사 → 전투 진입
                     └─ [플레이어 턴 → 적 턴 → ...] 반복
                           └─ 스킬 선택 → 타겟 지정 → (취소) → 스킬 선택으로 복귀
```

단순 Replace FSM이라면 전투 진입/종료, 턴 전환, 스킬 선택 취소처럼 서로 다른 깊이의 흐름을 모두 직접 기억해야 합니다. 상태가 늘어날수록 이 관리가 빠르게 복잡해집니다.

### 해결

스택 기반 FSM을 도입해 세 가지 전이 연산으로 모든 흐름을 표현합니다.


| 연산             | 의미                      |
| -------------- | ----------------------- |
| `PushState`    | 이전 상태를 보존하고 새 상태를 쌓음    |
| `PopState`     | 현재 상태를 제거하고 이전 상태로 복귀   |
| `ReplaceState` | 현재 상태를 교체 (뒤로 가기 없는 전진) |


`Update()`와 `HandleInput()`은 항상 스택 최상단에만 전달됩니다.

### 중첩 StateManager

`StateManager`는 중첩해서 사용할 수 있습니다. `CombatState`는 외부 StateManager에 속하면서도 내부에 별도 `_combatStateManager`를 가져, 전투 흐름과 던전 흐름을 완전히 분리합니다.

```
UniversalController.StateManager        (던전 레벨)
└─ CombatState
     └─ _combatStateManager             (전투 레벨)
          ├─ CombatBeginTurnState
          ├─ CombatAllyTurnState
          │    └─ _allyStateManager     (아군 입력 레벨)
          │         ├─ AllyPrimaryState
          │         ├─ AllySpellSelectState
          │         └─ AllySpellTargetState
          ├─ CombatProcessingActionState
          └─ CombatEndTurnState
```

---

## 설계 2: Action Queue와 처리 루프

> [Examples/Action/](Examples/Action/)

### 문제

게임의 모든 행동(스킬, 이동, 피해 등)은 연쇄적으로 파생 행동을 만들어냅니다.

예를 들어 플레이어가 스킬 하나를 선택하면, 실제 게임 내부에서는 다음과 같은 여러 단계가 뒤따릅니다.

```
스킬 시전
→ 시전 전 처리
→ 피해 처리
→ 자원 변동
→ 시전 후 처리
```

이 흐름을 재귀 호출이나 거대한 분기문으로 처리하면, 파생 행동이 많아질수록 전체 실행 순서를 추적하기 어려워집니다.

### 해결

핵심은 두 단계로 나누는 것이었습니다.

1. **Action 처리 루프**

플레이어의 선택을 Action으로 변환해 Queue에 넣고, 모든 행동을 같은 방식으로 순서대로 처리합니다.

이후 Controller가 Queue를 계속 확인하면서 Action을 하나씩 꺼내 실행합니다. Action이 처리 중 파생 Action을 만들면 Queue 앞에 다시 넣고, 루프는 이 새 Action들을 이어서 처리합니다.

```
플레이어가 SpellCastAction을 Queue에 삽입
→ 처리 루프가 SpellCastAction을 Pop
→ SpellCastAction.Apply()
   → BeforeSpellCastAction, DamageAction, AfterSpellCastAction을 Queue 앞에 삽입
→ 처리 루프가 새 Action들을 순서대로 Pop & Apply
```

이 구조 덕분에 게임의 논리 처리는 "Queue에 넣고, 하나씩 꺼내고, 파생 Action을 다시 넣는다"는 일관된 규칙으로 설명할 수 있습니다.


2. **Action이 끝난 뒤: Command 스냅샷**

각 Action은 처리 결과를 `ActionCommand`로 반환합니다. 이 Command는 "무슨 일이 일어났는지"에 대한 스냅샷이며, Controller는 이것을 View에 전달해 애니메이션을 재생합니다.

예를 들어 `DamageAction`은 HP를 직접 그리지 않고, `DamageCommand(target, before, after)` 같은 결과만 반환합니다. View는 이 정보를 보고 HP 변화 애니메이션을 재생합니다.

즉 구조를 나누면 다음과 같습니다.

- **Action Queue**: 무엇을 어떤 순서로 처리할지
- **ActionProcessor**: Queue를 계속 확인하며 실제로 처리하는 루프
- **ActionCommand**: 처리 결과를 View에 넘기기 위한 스냅샷

---

