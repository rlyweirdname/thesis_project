using Combat;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerHurtbox : MonoBehaviour
{
    private Collider col;
    private IHitReceiver receiver;

    public IHitReceiver Receiver => receiver ??= GetComponentInParent<IHitReceiver>();

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnDrawGizmosSelected()
    {
        Collider gizmoCol = col != null ? col : GetComponent<Collider>();
        if (gizmoCol == null) return;

        Gizmos.color = new Color(0.1f, 1f, 0.4f, 0.25f);
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
