using Combat;
using UnityEngine;

public class PlayerCombatant : MonoBehaviour, IHitReceiver
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;
    
    [Header("Hit Reactions")]
    [SerializeField] private float lightHitStun = 0.2f;
    [SerializeField] private float heavyHitStun = 0.5f;
    [SerializeField] private float launcherForceY = 8f;
    [SerializeField] private float grabStun = 0.8f;
    [SerializeField] private float hitForceX = 5f;
    
    [Header("Knockback")]
    [SerializeField] private float lightKnockback = 3f;
    [SerializeField] private float heavyKnockback = 6f;
    [SerializeField] private float launcherKnockback = 4f;

    private PlayerMovement movement;
    private Rigidbody rb;
    private bool isInHitStun = false;
    private float hitStunTimer;
    private HitType currentHitType;

    public bool IsInHitStun => isInHitStun;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (isInHitStun)
        {
            hitStunTimer -= Time.deltaTime;
            if (hitStunTimer <= 0f)
            {
                EndHitStun();
            }
        }
    }

    public void ReceiveHit(HitData hit, GameObject attacker)
    {
        if (isInHitStun) return;
        
        currentHealth = Mathf.Max(0, currentHealth - hit.damage);
        
        Debug.Log($"Took {hit.damage} damage! HP: {currentHealth}/{maxHealth}");
        
        ApplyHitReaction(hit, attacker);
        
        if (currentHealth <= 0)
        {
            Debug.Log("PLAYER DIED");
        }
    }

    private void ApplyHitReaction(HitData hit, GameObject attacker)
    {
        isInHitStun = true;
        currentHitType = hit.hitType;

        if (movement != null)
            movement.SetHitStun(true);

        float knockbackX = 0f;
        float knockbackY = 0f;
        float stunDuration = lightHitStun;

        Vector3 attackerPos = attacker.transform.position;
        float hitDirection = transform.position.x < attackerPos.x ? -1f : 1f;

        switch (hit.hitType)
        {
            case HitType.Light:
                stunDuration = lightHitStun;
                knockbackX = (hitForceX + lightKnockback) * hitDirection;
                Debug.Log("Light hitstun!");
                break;
                
            case HitType.Heavy:
                stunDuration = heavyHitStun;
                knockbackX = (hitForceX + heavyKnockback) * hitDirection;
                Debug.Log("Heavy hitstun!");
                break;
                
            case HitType.Launcher:
                stunDuration = 0.6f;
                knockbackX = (hitForceX + launcherKnockback) * hitDirection;
                knockbackY = launcherForceY;
                Debug.Log("Launcher! Airborne!");
                break;
                
            case HitType.Grab:
                stunDuration = grabStun;
                knockbackX = 0f;
                Debug.Log("Grabbed!");
                break;
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector3(knockbackX, knockbackY, 0f);
        }

        hitStunTimer = stunDuration;
    }

    private void EndHitStun()
    {
        isInHitStun = false;
        currentHitType = HitType.Light;
        
        if (movement != null)
            movement.SetHitStun(false);
            
        Debug.Log("Hitstun ended, back to normal");
    }
}
