using UnityEngine;

namespace Game.Combat
{
    public enum WeaponType { Katana, Chain, Hammer, Bow }

    public interface IPlayerCombatContext
    {
        Transform Transform { get; }
        IHealth Health { get; }
        bool IsDead { get; }
        bool IsGuarding { get; }
        bool IsAttacking { get; }
        WeaponType CurrentWeapon { get; }

        bool TryBlock(in BlockContext context);

        /// <summary>0=W 1=A 2=S 3=D，转场期间由 IStanceService.TryChooseDirection 调用。</summary>
        void ExecuteDirectionalSkill(int direction);
    }
}
