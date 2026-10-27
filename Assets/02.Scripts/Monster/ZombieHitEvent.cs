using UnityEngine;

public class ZombieHitEvent : MonoBehaviour
{
    private Zombie _zombie;

    private void Start()
    {
        _zombie = GetComponentInParent<Zombie>();
    }

    public void HitEvent()
    {
        _zombie.AttackAction();
    }
}
