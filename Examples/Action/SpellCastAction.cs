using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Model.GameAction
{
    // ---------------------------------------------------------------
    // 1. Action 인터페이스
    //
    // Apply()는 게임 상태를 변경하고, 변경 결과의 스냅샷(Command)을 반환합니다.
    // 처리 과정에서 파생 Action이 필요하면 queue에 직접 삽입합니다.
    // ---------------------------------------------------------------
    public interface ICombatAction
    {
        ActionCommand Apply(ActionManager<ICombatAction> queue);
    }

    // ---------------------------------------------------------------
    // 2. Action 구현 예시
    // ---------------------------------------------------------------

    // 스킬 시전 — 처리 시 Before/Mid/After 세 파생 Action을 앞에 삽입합니다.
    public class SpellCastAction : ICombatAction
    {
        private readonly string _spellName;
        private readonly string _casterName;

        public SpellCastAction(string spellName, string casterName)
        {
            _spellName = spellName;
            _casterName = casterName;
        }

        public ActionCommand Apply(ActionManager<ICombatAction> queue)
        {
            // 파생 Action들을 순서대로 큐 앞에 삽입합니다.
            // 이후 루프가 이들을 순서대로 처리합니다.
            queue.AddFirstRange(new List<ICombatAction>
            {
                new BeforeSpellCastAction(_casterName),
                new DamageAction("Enemy", 15),
                new AfterSpellCastAction(),
            });

            return new SpellCastCommand(_spellName, _casterName);
        }
    }

    public class BeforeSpellCastAction : ICombatAction
    {
        private readonly string _casterName;
        public BeforeSpellCastAction(string casterName) { _casterName = casterName; }

        public ActionCommand Apply(ActionManager<ICombatAction> queue)
            => new BeforeSpellCastCommand(_casterName);
    }

    // 피해 처리 — 변경 전/후 HP를 스냅샷으로 Command에 담습니다.
    // View는 이 스냅샷만 보고 HP 변화 애니메이션을 재생합니다.
    public class DamageAction : ICombatAction
    {
        private readonly string _targetName;
        private readonly int _amount;

        public DamageAction(string targetName, int amount)
        {
            _targetName = targetName;
            _amount = amount;
        }

        public ActionCommand Apply(ActionManager<ICombatAction> queue)
        {
            int hpBefore = 50;
            int hpAfter = hpBefore - _amount;
            return new DamageCommand(_targetName, hpBefore, hpAfter);
        }
    }

    public class AfterSpellCastAction : ICombatAction
    {
        public ActionCommand Apply(ActionManager<ICombatAction> queue)
            => new AfterSpellCastCommand();
    }

    // ---------------------------------------------------------------
    // 3. Action Queue 처리 루프 (Controller 역할)
    //
    // Queue에서 Action을 하나씩 꺼내 처리합니다.
    // Action이 파생 Action을 Queue 앞에 삽입하면, 다음 루프에서 즉시 처리됩니다.
    // 모든 Action이 처리될 때까지 반복합니다.
    // ---------------------------------------------------------------
    public class ActionProcessor
    {
        private readonly ActionManager<ICombatAction> _queue = new();

        // 플레이어가 스킬을 선택하면 Queue에 삽입합니다.
        public void EnqueuePlayerAction(ICombatAction action)
        {
            _queue.Add(action);
        }

        // Queue를 순서대로 처리합니다.
        // 실제 게임에서는 각 Action 처리 후 애니메이션을 기다리고 다음으로 넘어갑니다.
        public async Task ProcessAll(CommandExecutor executor)
        {
            while (!_queue.IsEmpty)
            {
                ICombatAction action = _queue.Pop();
                ActionCommand command = action.Apply(_queue);

                // Command 트리를 View에 전달해 애니메이션을 재생하고 완료를 기다립니다.
                await executor.Execute(command);
            }
        }
    }

    // ---------------------------------------------------------------
    // 4. Command (논리 결과 스냅샷)
    //
    // View는 Command 트리만 보고 애니메이션을 재생합니다.
    // Model을 직접 참조하지 않아도 됩니다.
    // ---------------------------------------------------------------
    public abstract class ActionCommand
    {
        public List<List<ActionCommand>> Children = new();

        // order가 낮은 그룹부터 재생됩니다. 같은 그룹은 병렬 재생됩니다.
        public void AddChild(ActionCommand child, int order)
        {
            while (order >= Children.Count) Children.Add(new());
            Children[order].Add(child);
        }
    }

    public sealed class SpellCastCommand : ActionCommand
    {
        public string SpellName;
        public string CasterName;
        public SpellCastCommand(string spell, string caster) { SpellName = spell; CasterName = caster; }
    }

    public sealed class BeforeSpellCastCommand : ActionCommand
    {
        public string CasterName;
        public BeforeSpellCastCommand(string caster) { CasterName = caster; }
    }

    public sealed class DamageCommand : ActionCommand
    {
        public string TargetName;
        public int HpBefore;
        public int HpAfter;
        public DamageCommand(string target, int before, int after)
        { TargetName = target; HpBefore = before; HpAfter = after; }
    }

    public sealed class AfterSpellCastCommand : ActionCommand { }

    // ---------------------------------------------------------------
    // 5. CommandExecutor — Command 트리를 비동기로 순회하며 애니메이션을 재생합니다.
    // ---------------------------------------------------------------
    public class CommandExecutor
    {
        public virtual async Task Execute(ActionCommand command)
        {
            Task self = PlayAnimation(command);
            foreach (var group in command.Children)
                await Task.WhenAll(group.Select(child => Execute(child)));
            await self;
        }

        protected virtual Task PlayAnimation(ActionCommand command)
        {
            return command switch
            {
                BeforeSpellCastCommand => Task.Delay(300),
                DamageCommand          => Task.Delay(200),
                AfterSpellCastCommand  => Task.Delay(400),
                _                      => Task.CompletedTask,
            };
        }
    }
}
