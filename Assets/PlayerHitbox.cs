using System.Collections.Generic;
using System.Linq;
using Combat;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerHitbox : MonoBehaviour
{
    [Header("Hit Data")]
    [SerializeField] private HitData hitData = new HitData(10, HitType.Light, true);
    [Tooltip("Optional: restrict hits to these layers (should match EnemyHurtbox layer). 0 = no filter")] 
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private bool logHits = false;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.4f, 0.1f, 0.25f);
    [Tooltip("Clear hit memory each swing when you toggle only the collider via Animation Event.")]
    [SerializeField] private bool requireManualReset;
    [Tooltip("Time before same target can be hit again. 0 = once per activation only.")]
    [SerializeField] private float hitCooldown = 0.2f;
    [Header("Hit Effects")]
    [SerializeField] private float screenShakeAmount = 0.1f;
    [SerializeField] private float screenShakeDuration = 0.1f;

    private readonly HashSet<Collider> hitsThisActivation = new HashSet<Collider>();
    private readonly Dictionary<Collider, float> hitCooldownTimers = new Dictionary<Collider, float>();
    private Collider col;
    private ComboCounter combo;
    private Camera mainCamera;
    private Vector3 originalCamPos;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        combo = GetComponentInParent<ComboCounter>();
        mainCamera = Camera.main;
        if (mainCamera != null)
            originalCamPos = mainCamera.transform.position;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        var keys = new List<Collider>(hitCooldownTimers.Keys);
        foreach (var key in keys)
        {
            hitCooldownTimers[key] -= dt;
            if (hitCooldownTimers[key] <= 0)
            {
                hitCooldownTimers.Remove(key);
                hitsThisActivation.Remove(key);
            }
        }
    }

    private void OnEnable()
    {
        hitsThisActivation.Clear();
    }

    private void OnDisable()
    {
        hitsThisActivation.Clear();
    }

    /// <summary>Call from an Animation Event right before enabling the collider if you do not toggle the whole GameObject.</summary>
    public void ResetHitMemory()
    {
        hitsThisActivation.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!enabled || col == null) return;
        if (hitCooldownTimers.TryGetValue(other, out float remaining) && remaining > 0) return;
        if (hitsThisActivation.Contains(other)) return;
        if (targetLayers.value != 0 && ((1 << other.gameObject.layer) & targetLayers.value) == 0) return;

        EnemyHurtbox hurtbox = other.GetComponent<EnemyHurtbox>();
        if (hurtbox == null) return;

        IHitReceiver receiver = hurtbox.Receiver;
        if (receiver == null) return;

        receiver.ReceiveHit(hitData, transform.root.gameObject);
        hitsThisActivation.Add(other);
        hitCooldownTimers[other] = hitCooldown;
        combo?.Increment();

        if (screenShakeAmount > 0f)
            StartCoroutine(ScreenShake());

        if (logHits)
            Debug.Log($"PlayerHitbox hit {other.name} with {hitData.hitType} for {hitData.damage} dmg", this);
    }

    private void OnDrawGizmosSelected()
    {
        Collider gizmoCol = col != null ? col : GetComponent<Collider>();
        if (gizmoCol == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.matrix = gizmoCol.transform.localToWorldMatrix;

        if (gizmoCol is BoxCollider box)
        {
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (gizmoCol is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        else if (gizmoCol is CapsuleCollider capsule)
        {
            // approximate with wire cube using bounds
            Gizmos.DrawWireCube(capsule.center, new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f));
        }
    }

    private System.Collections.IEnumerator ScreenShake()
    {
        if (mainCamera == null) yield break;
        
        float elapsed = 0f;
        while (elapsed < screenShakeDuration)
        {
            Vector3 offset = Random.insideUnitSphere * screenShakeAmount;
            mainCamera.transform.position = originalCamPos + offset;
            elapsed += Time.deltaTime;
            yield return null;
        }
        mainCamera.transform.position = originalCamPos;
    }
}
