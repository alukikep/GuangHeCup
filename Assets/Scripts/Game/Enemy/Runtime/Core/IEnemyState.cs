namespace Game.Enemy
{
    public interface IEnemyState
    {
        void OnEnter();

        void OnTick();

        void OnExit();
    }
}
