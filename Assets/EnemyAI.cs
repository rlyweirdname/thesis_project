using System.Collections;
using Combat;
using UnityEngine;

#pragma warning disable CS0414 // Serialized tuning fields are intentionally editor-only data.
public class EnemyAI : MonoBehaviour, IHitReceiver
{
    private Rigidbody rb;

    [Header("Movement (unused placeholder)")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float groundAcceleration = 120f;
    [SerializeField] private float groundDeceleration = 150f;

    [Header("Jump Timing (frames @ 60 FPS) – placeholders")]
    [SerializeField] private int preJumpFrames = 3;
    [SerializeField] private int jumpBufferFrames = 4;
    [SerializeField] private int extraAirJumps = 1;

    [Header("Jump Feel – placeholders")]
    [SerializeField] private float riseExtraGravity = 18f;
    [SerializeField] private float fallExtraGravity = 45f;
    [SerializeField] private float apexExtraGravity = 4f;
    [SerializeField] private float apexThreshold = 2.5f;
    [SerializeField] private float jumpImpulse = 13f;
    [SerializeField] private float airJumpImpulseMultiplier = 0.5f;
    [SerializeField] private float jumpHorizontalSpeed = 6f;
    [SerializeField] private float postAirDashFallExtraGravity = 65f;

    [Header("Dash Timing (frames @ 60 FPS) – placeholders")]
    [SerializeField] private bool allowGroundDash = true;
    [SerializeField] private int airDashStartupFrames = 3;
    [SerializeField] private int airDashActiveFrames = 16;
    [SerializeField] private int airDashLandingRecoveryFrames = 4;
    [SerializeField] private int iadWindowFrames = 4;
    [SerializeField] private int dashBufferFrames = 4;
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashCooldown = 0.3f;
    [SerializeField] private bool lockVerticalVelocityDuringDash = true;
    [SerializeField] private int extraAirDashes = 1;

    [Header("Dash Feel – placeholders")]
    [SerializeField] private AnimationCurve dashEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float postDashInertiaMultiplier = 0.25f;

    [Header("Ground Check – placeholders")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.2f;
    [SerializeField] private LayerMask groundMask;

    [Header("Combat State")]
    [SerializeField] private float staggerDuration = 0.6f;

    public enum AIState { Patrol, Aggro, Stagger }
    [SerializeField] private AIState state = AIState.Patrol;

    private Coroutine staggerRoutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;                // allow gravity and physics
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
    }

    public void ReceiveHit(HitData hit, GameObject attacker)
    {
        // In this simple prototype we only react with stagger; extend with health/damage as needed.
        EnterStagger();
    }

    private void EnterStagger()
    {
        if (staggerRoutine != null)
            StopCoroutine(staggerRoutine);

        state = AIState.Stagger;
        staggerRoutine = StartCoroutine(RecoverFromStagger());
    }

    private IEnumerator RecoverFromStagger()
    {
        yield return new WaitForSeconds(staggerDuration);
        state = AIState.Aggro;
        staggerRoutine = null;
    }
}
#pragma warning restore CS0414
