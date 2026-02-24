using Combat;
using UnityEngine;

public class PlayerCombatant : MonoBehaviour, IHitReceiver
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;

    public void ReceiveHit(HitData hit, GameObject attacker)
    {
        currentHealth = Mathf.Max(0, currentHealth - hit.damage);
        // TODO: hook into hit reactions, animations, and death handling.
    }
}
