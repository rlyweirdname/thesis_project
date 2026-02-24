using System.Collections.Generic;
using Combat;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyHitbox : MonoBehaviour
{
    [Header("Hit Data")]
    [SerializeField] private HitData hitData = new HitData(8, HitType.Light, true);
    [Tooltip("Optional: restrict hits to these layers (should match PlayerHurtbox layer). 0 = no filter")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private bool logHits = false;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.7f, 1f, 0.25f);
    [Tooltip("Clear hit memory each swing when you toggle only the collider via Animation Event.")]
    [SerializeField] private bool requireManualReset;

    private readonly HashSet<Collider> hitsThisActivation = new HashSet<Collider>();
    private Collider col;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
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
        if (hitsThisActivation.Contains(other)) return;
        if (targetLayers.value != 0 && ((1 << other.gameObject.layer) & targetLayers.value) == 0) return;

        PlayerHurtbox hurtbox = other.GetComponent<PlayerHurtbox>();
        if (hurtbox == null) return;

        IHitReceiver receiver = hurtbox.Receiver;
        if (receiver == null) return;

        receiver.ReceiveHit(hitData, transform.root.gameObject);
        hitsThisActivation.Add(other);

        if (logHits)
            Debug.Log($"EnemyHitbox hit {other.name} with {hitData.hitType} for {hitData.damage} dmg", this);
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
            Gizmos.DrawWireCube(capsule.center, new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f));
        }
    }
}
