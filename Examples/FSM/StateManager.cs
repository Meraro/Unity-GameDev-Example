using System.Collections.Generic;

namespace Controller.State
{
    // 게임의 한 "상태"를 표현하는 추상 클래스.
    // Push/Pop/Replace 세 가지 전이 연산만으로 복잡한 흐름을 표현합니다.
    public abstract class GameState
    {
        protected StateManager _manager;

        public GameState(StateManager manager) { _manager = manager; }

        // 스택에 Push될 때 호출 — 진입 초기화
        public virtual void OnEnter() { }
        // 스택에서 Pop될 때 호출 — 정리
        public virtual void OnExit() { }
        // 매 프레임 호출. 스택 최상단 상태에만 전달됩니다.
        public virtual void Update() { }
        // 플레이어 입력이 발생했을 때 호출. 스택 최상단 상태에만 전달됩니다.
        public virtual void HandleInput(object inputEvent) { }
    }

    // GameState들을 스택으로 관리하는 FSM 엔진.
    //
    // 이 게임의 전투 흐름은 상태가 중첩되는 경우가 많습니다.
    //   기본 메뉴 → 스킬 선택 → 타겟 지정 → (취소) → 스킬 선택으로 복귀
    //
    // 단순 Replace FSM은 "취소" 시 이전 상태를 직접 기억해야 합니다.
    // 스택 기반은 Pop 하나로 이전 상태로 돌아갈 수 있습니다.
    //
    // 이 클래스는 중첩해서 사용할 수 있습니다.
    // CombatState는 외부 StateManager에 속하면서 내부에 별도의 StateManager를 가져
    // 전투 흐름과 던전 흐름을 완전히 분리합니다.
    public class StateManager
    {
        private readonly Stack<GameState> _stack = new();

        public GameState CurrentState => _stack.Count > 0 ? _stack.Peek() : null;

        // 이전 상태를 보존하고 새 상태를 쌓습니다. PopState()로 되돌아올 수 있습니다.
        public void PushState(GameState state)
        {
            _stack.Push(state);
            state.OnEnter();
        }

        // 현재 상태를 제거하고 이전 상태로 복귀합니다.
        public GameState PopState()
        {
            if (_stack.Count == 0) return null;
            var state = _stack.Pop();
            state.OnExit();
            return state;
        }

        // 현재 상태를 교체합니다. 이전 상태로 돌아갈 수 없습니다.
        public void ReplaceState(GameState newState)
        {
            PopState();
            PushState(newState);
        }

        public void Update() => CurrentState?.Update();
        public void HandleInput(object inputEvent) => CurrentState?.HandleInput(inputEvent);
    }
}
