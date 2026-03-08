using System.Collections.Generic;

namespace Model.GameAction
{
    // 게임에서 발생하는 모든 행동(Action)을 순서대로 관리하는 큐.
    //
    // 하나의 행동은 처리 과정에서 파생 행동을 만들어냅니다.
    //   스킬 시전 → 피해 처리 → SP 변동 → 상태이상 적용 → ...
    //
    // 재귀 호출 대신 큐에 넣고 하나씩 꺼내 처리하면,
    // Controller가 각 행동의 처리와 뷰 재생을 명확하게 분리할 수 있습니다.
    public class ActionManager<T> where T : class
    {
        private readonly LinkedList<T> _queue = new();

        public bool IsEmpty => _queue.Count == 0;

        // 큐 맨 뒤에 삽입합니다.
        public void Add(T action) => _queue.AddLast(action);

        // 큐 맨 앞에 삽입합니다. 파생 행동처럼 즉시 처리되어야 할 때 사용합니다.
        public void AddFirst(T action) => _queue.AddFirst(action);

        public void AddRange(List<T> actions)
        {
            if (actions is null) return;
            foreach (var action in actions) Add(action);
        }

        // 순서를 유지하며 큐 맨 앞에 여러 행동을 삽입합니다.
        public void AddFirstRange(List<T> actions)
        {
            if (actions is null) return;
            for (int i = actions.Count - 1; i >= 0; i--)
                AddFirst(actions[i]);
        }

        // 큐 맨 앞의 행동을 꺼냅니다. 비어있으면 null을 반환합니다.
        public T Pop()
        {
            if (_queue.Count == 0) return null;
            var first = _queue.First.Value;
            _queue.RemoveFirst();
            return first;
        }

        public void Clear() => _queue.Clear();
    }
}
