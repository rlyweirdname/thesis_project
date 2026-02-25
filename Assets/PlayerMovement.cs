using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    private const float FramesPerSecond = 60f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float groundAcceleration = 120f;
    [SerializeField] private float groundDeceleration = 150f;

    [Header("Jump Timing (frames @ 60 FPS)")]
    [SerializeField] private int preJumpFrames = 3;
    [SerializeField] private int jumpBufferFrames = 4;
    [SerializeField] private int extraAirJumps = 1;

    [Header("Jump Feel — Asymmetric Gravity")]
    [Tooltip("Extra downward force while rising. Higher = snappier launch.")]
    [SerializeField] private float riseExtraGravity = 18f;
    [Tooltip("Extra downward force while falling. Higher = faster drop.")]
    [SerializeField] private float fallExtraGravity = 45f;
    [Tooltip("Extra downward force near jump apex. Lower = longer hang time.")]
    [SerializeField] private float apexExtraGravity = 4f;
    [Tooltip("Y velocity threshold to be considered at apex.")]
    [SerializeField] private float apexThreshold = 2.5f;
    [Tooltip("Initial upward velocity on jump. Tune alongside riseExtraGravity.")]
    [SerializeField] private float jumpImpulse = 13f;
    [Tooltip("Air jump height as a fraction of first jump. 0.5 = half height.")]
    [SerializeField] private float airJumpImpulseMultiplier = 0.5f;
    [Tooltip("Horizontal speed applied when jumping diagonally. 0 = pure up only.")]
    [SerializeField] private float jumpHorizontalSpeed = 6f;
    [Tooltip("Faster fall after airdash ends.")]
    [SerializeField] private float postAirDashFallExtraGravity = 65f;

    [Header("Dash Timing (frames @ 60 FPS)")]
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

    [Header("Dash Feel")]
    [Tooltip("Ease-in curve for dash velocity. X = time 0-1, Y = speed multiplier 0-1.")]
    [SerializeField] private AnimationCurve dashEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("How much dash speed carries over after airdash ends. 0 = full stop, 1 = full speed.")]
    [SerializeField] private float postDashInertiaMultiplier = 0.25f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.2f;
    [SerializeField] private LayerMask groundMask;

    [Header("Facing")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool flipVisualScaleX = true;

    private Rigidbody rb;
    private Collider playerCollider;
    private bool isGrounded;
    private float moveInput;
    private float lockedZ;
    private float dashCooldownLeft;
    private float dashDirection = 1f;
    private float jumpDirection = 0f;       // -1 up-left, 0 up, 1 up-right — locked at jump press
    private float lockedJumpVelocityX = 0f; // horizontal velocity locked for the entire jump duration
    private float jumpBufferTimer;
    private float dashBufferTimer;
    private float preJumpTimer;
    private float dashStartupTimer;
    private float dashActiveTimer;
    private float dashActiveDuration;   // total duration of the current dash for curve sampling
    private float landingRecoveryTimer;
    private float timeSinceJumpStart;

    // air jump burns airdash too — airdash does NOT lock out air jump
    private int airJumpsRemaining;
    private int airDashesRemaining;

    private bool usedAirDashThisAirborne;
    private bool isDashActive;
    private bool jumpPressedBuffered;
    private bool dashPressedBuffered;
    private bool attackPressedBuffered;
    private bool autoGroundCheckFromCollider;
    private float facingSign = 1f;
    private bool isInHitStun = false;
    private bool isAttacking = false;
    private float attackCooldownTimer = 0f;
    private float attackDurationTimer = 0f;
    [SerializeField] private float attackDuration = 0.15f;
    [SerializeField] private float attackCooldown = 0.3f;
    [SerializeField] private PlayerHitbox hitbox;
    [SerializeField] private float hitboxOffsetX = 1f;
    [SerializeField] private float hitboxOffsetY = 0f;

    public void SetHitStun(bool stunned)
    {
        isInHitStun = stunned;
        if (stunned)
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    public void ForceFlip()
    {
        facingSign *= -1f;
        Transform target = visualRoot != null ? visualRoot : transform;
        Vector3 s = target.localScale;
        s.x = Mathf.Abs(s.x) * facingSign;
        target.localScale = s;
    }

    private enum VerticalState
    {
        Grounded,
        PreJump,
        Rising,
        Apex,
        Falling
    }

    private VerticalState verticalState = VerticalState.Grounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        // movement logic expects a dynamic body (not kinematic) so we can write linearVelocity
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // disable built-in gravity — we drive it manually per-state for full control
        rb.useGravity = false;
        lockedZ = transform.position.z;

        if (groundCheck == null)
            groundCheck = transform;

        autoGroundCheckFromCollider = groundCheck == transform;

        airJumpsRemaining = Mathf.Max(0, extraAirJumps);
        airDashesRemaining = Mathf.Max(0, extraAirDashes);

        // Disable hitbox at start
        if (hitbox != null)
            hitbox.gameObject.SetActive(false);
    }

    private void Update()
    {
        moveInput = 0f;
        bool jumpPressed = false;
        bool dashPressed = false;

        if (isInHitStun) return;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
                moveInput -= 1f;

            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
                moveInput += 1f;

            jumpPressed = kb.spaceKey.wasPressedThisFrame
                       || kb.wKey.wasPressedThisFrame
                       || kb.upArrowKey.wasPressedThisFrame;

        dashPressed = kb.leftShiftKey.wasPressedThisFrame
                   || kb.rightShiftKey.wasPressedThisFrame;

        if (kb.fKey.wasPressedThisFrame)
            attackPressedBuffered = true;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            float stickX = pad.leftStick.x.ReadValue();
            if (Mathf.Abs(stickX) > Mathf.Abs(moveInput))
                moveInput = stickX;

            jumpPressed |= pad.buttonSouth.wasPressedThisFrame;
            dashPressed |= pad.buttonEast.wasPressedThisFrame
                        || pad.rightShoulder.wasPressedThisFrame;
            attackPressedBuffered |= pad.buttonWest.wasPressedThisFrame;
        }

        moveInput = Mathf.Clamp(moveInput, -1f, 1f);
        UpdateFacing(moveInput);
        // only update dash direction when not actively dashing — locks direction mid-dash
        if (Mathf.Abs(moveInput) > 0.01f && !isDashActive)
            dashDirection = Mathf.Sign(moveInput);

        if (jumpPressed)
        {
            jumpPressedBuffered = true;
            jumpBufferTimer = FramesToSeconds(jumpBufferFrames);
            // lock direction at press time — snap to -1, 0, or 1
            jumpDirection = moveInput > 0.1f ? 1f : moveInput < -0.1f ? -1f : 0f;
        }

        if (dashPressed)
        {
            dashPressedBuffered = true;
            dashBufferTimer = FramesToSeconds(dashBufferFrames);
        }

        jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        if (jumpBufferTimer <= 0f)
            jumpPressedBuffered = false;

        dashBufferTimer = Mathf.Max(0f, dashBufferTimer - Time.deltaTime);
        if (dashBufferTimer <= 0f)
            dashPressedBuffered = false;

        dashCooldownLeft = Mathf.Max(0f, dashCooldownLeft - Time.deltaTime);
        attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 v = rb.linearVelocity;
        bool groundedNow = IsGrounded();
        bool shouldStickToGround = groundedNow && v.y <= 0.1f && verticalState != VerticalState.Rising;

        // ── GROUNDED / AIRBORNE STATE ──────────────────────────────────────
        if (shouldStickToGround)
        {
            if (!isGrounded && usedAirDashThisAirborne)
                landingRecoveryTimer = FramesToSeconds(airDashLandingRecoveryFrames);

            isGrounded = true;
            airJumpsRemaining = Mathf.Max(0, extraAirJumps);
            airDashesRemaining = Mathf.Max(0, extraAirDashes);
            usedAirDashThisAirborne = false;
            isDashActive = false;
            timeSinceJumpStart = 0f;
            lockedJumpVelocityX = 0f;

            if (dashActiveTimer <= 0f && dashStartupTimer <= 0f && preJumpTimer <= 0f)
                verticalState = VerticalState.Grounded;
        }
        else
        {
            isGrounded = false;
            if (verticalState == VerticalState.Grounded)
                verticalState = VerticalState.Falling;
            timeSinceJumpStart += dt;
        }

        landingRecoveryTimer = Mathf.Max(0f, landingRecoveryTimer - dt);

        bool dashBusy = dashStartupTimer > 0f || dashActiveTimer > 0f;
        bool canControl = landingRecoveryTimer <= 0f && !dashBusy && !isAttacking;

        // ── ATTACK ─────────────────────────────────────────────────────────────
        if (attackPressedBuffered && attackCooldownTimer <= 0f && canControl && !isInHitStun)
        {
            isAttacking = true;
            attackDurationTimer = attackDuration;
            attackCooldownTimer = attackCooldown;
            attackPressedBuffered = false;
            
            if (hitbox != null)
            {
                hitbox.gameObject.SetActive(true);
                Vector3 pos = transform.position + new Vector3(hitboxOffsetX * facingSign, hitboxOffsetY, 0);
                hitbox.transform.position = pos;
            }
        }

        if (isAttacking)
        {
            attackDurationTimer -= dt;
            if (hitbox != null)
            {
                Vector3 pos = transform.position + new Vector3(hitboxOffsetX * facingSign, hitboxOffsetY, 0);
                hitbox.transform.position = pos;
            }
            if (attackDurationTimer <= 0f)
            {
                isAttacking = false;
                if (hitbox != null)
                    hitbox.gameObject.SetActive(false);
            }
        }

        // ── JUMP FROM GROUND ───────────────────────────────────────────────
        if (jumpPressedBuffered && isGrounded && verticalState == VerticalState.Grounded && canControl)
        {
            preJumpTimer = FramesToSeconds(preJumpFrames);
            verticalState = VerticalState.PreJump;
            jumpPressedBuffered = false;
        }

        if (verticalState == VerticalState.PreJump)
        {
            preJumpTimer -= dt;
            v.x = Mathf.MoveTowards(v.x, 0f, groundDeceleration * dt);
            v.y = -2f;
            if (preJumpTimer <= 0f)
            {
                verticalState = VerticalState.Rising;
                timeSinceJumpStart = 0f;
                v.y = jumpImpulse;
                // lock horizontal velocity for the entire jump — no steering mid-air
                lockedJumpVelocityX = jumpDirection * jumpHorizontalSpeed;
                v.x = lockedJumpVelocityX;
                usedAirDashThisAirborne = false;
            }
        }

        // ── AIR JUMP — burns airdash too, but airdash does NOT lock out air jump ──
        bool canAirJump = !isGrounded
            && airJumpsRemaining > 0
            && verticalState != VerticalState.PreJump;

        if (jumpPressedBuffered && canAirJump && !dashBusy)
        {
            verticalState = VerticalState.Rising;
            timeSinceJumpStart = 0f;
            v.y = jumpImpulse * airJumpImpulseMultiplier; // 50% of first jump by default
            // lock horizontal velocity for the entire jump
            lockedJumpVelocityX = jumpDirection * jumpHorizontalSpeed;
            v.x = lockedJumpVelocityX;
            airJumpsRemaining--;
            airDashesRemaining = 0;  // double jump locks out airdash
            jumpPressedBuffered = false;
            usedAirDashThisAirborne = false;
        }

        // ── DASH — independent of air jump ────────────────────────────────
        bool canGroundDash = allowGroundDash
            && isGrounded
            && dashCooldownLeft <= 0f
            && !dashBusy
            && canControl;

        bool canAirDash = !isGrounded
            && airDashesRemaining > 0
            && dashCooldownLeft <= 0f
            && !dashBusy
            && landingRecoveryTimer <= 0f;

        if (dashPressedBuffered && (canGroundDash || canAirDash))
        {
            bool iad = !isGrounded && timeSinceJumpStart <= FramesToSeconds(iadWindowFrames);
            dashActiveDuration = FramesToSeconds(airDashActiveFrames);
            dashActiveTimer = dashActiveDuration;
            dashCooldownLeft = dashCooldown;
            verticalState = isGrounded ? VerticalState.Grounded : VerticalState.Falling;
            isDashActive = true; // lock direction from this point

            if (iad)
            {
                // IAD: skip startup entirely — instant horizontal movement
                dashStartupTimer = 0f;
            }
            else
            {
                dashStartupTimer = FramesToSeconds(airDashStartupFrames);
            }

            if (canAirDash)
            {
                airDashesRemaining--;
                airJumpsRemaining = 0;   // airdash locks out double jump too
                usedAirDashThisAirborne = true;
                v.y = 0f; // zero vertical on all airdashes
            }

            dashPressedBuffered = false;
        }

        // ── DASH STARTUP ──────────────────────────────────────────────────
        if (dashStartupTimer > 0f)
        {
            dashStartupTimer -= dt;
            v.x = Mathf.MoveTowards(v.x, 0f, groundDeceleration * dt);
            if (lockVerticalVelocityDuringDash)
                v.y = 0f;
        }

        // ── DASH ACTIVE — flat constant speed, straight line ──────────────
        if (dashStartupTimer <= 0f && dashActiveTimer > 0f)
        {
            dashActiveTimer -= dt;
            v.x = dashDirection * dashSpeed;
            if (lockVerticalVelocityDuringDash)
                v.y = 0f;

            // when dash expires, hand off reduced velocity and release direction lock
            if (dashActiveTimer <= 0f && !isGrounded)
            {
                lockedJumpVelocityX = dashDirection * dashSpeed * postDashInertiaMultiplier;
                isDashActive = false;
            }
            else if (dashActiveTimer <= 0f)
            {
                isDashActive = false;
            }
        }

        // ── VERTICAL PHYSICS — asymmetric gravity ─────────────────────────
        if (dashActiveTimer <= 0f && dashStartupTimer <= 0f && verticalState != VerticalState.PreJump)
        {
            float vy = v.y;

            if (isGrounded)
            {
                // stick to ground with a small downward nudge
                if (vy < 0f)
                    v.y = -2f;
            }
            else
            {
                // classify arc position
                bool atApex = Mathf.Abs(vy) < apexThreshold && verticalState == VerticalState.Rising;

                if (atApex)
                {
                    verticalState = VerticalState.Apex;
                }
                else if (vy < -apexThreshold && (verticalState == VerticalState.Apex || verticalState == VerticalState.Rising))
                {
                    verticalState = VerticalState.Falling;
                }

                // apply base gravity manually
                float baseGravity = 9.81f;
                v.y -= baseGravity * dt;

                // apply extra gravity per state for the feel we want
                switch (verticalState)
                {
                    case VerticalState.Rising:
                        v.y -= riseExtraGravity * dt;
                        break;

                    case VerticalState.Apex:
                        // gentle pull — this is the hang time
                        v.y -= apexExtraGravity * dt;
                        break;

                    case VerticalState.Falling:
                        float extraFall = usedAirDashThisAirborne
                            ? postAirDashFallExtraGravity
                            : fallExtraGravity;
                        v.y -= extraFall * dt;
                        break;
                }
            }
        }

        // ── HORIZONTAL MOVEMENT ───────────────────────────────────────────
        if (canControl && !isInHitStun && !isAttacking && verticalState != VerticalState.PreJump && dashStartupTimer <= 0f && dashActiveTimer <= 0f)
        {
            if (isGrounded)
            {
                // on the ground — full movement control as normal
                float targetSpeed = moveInput * moveSpeed;
                float rate = Mathf.Abs(targetSpeed) > 0.01f ? groundAcceleration : groundDeceleration;
                v.x = Mathf.MoveTowards(v.x, targetSpeed, rate * dt);
            }
            else
            {
                // in the air — direction is locked at jump press, no steering allowed
                v.x = lockedJumpVelocityX;
            }
        }
        else if (!canControl && isGrounded && dashStartupTimer <= 0f && dashActiveTimer <= 0f)
        {
            v.x = Mathf.MoveTowards(v.x, 0f, groundDeceleration * dt);
        }

        rb.linearVelocity = v;

        // lock Z so the character stays on the 2.5D plane
        Vector3 p = rb.position;
        p.z = lockedZ;
        rb.MovePosition(p);
    }

    private static float FramesToSeconds(int frames)
    {
        return Mathf.Max(0, frames) / FramesPerSecond;
    }

    private bool IsGrounded()
    {
        if (playerCollider == null)
            return false;

        Vector3 checkPosition = groundCheck != null && !autoGroundCheckFromCollider
            ? groundCheck.position
            : new Vector3(playerCollider.bounds.center.x, playerCollider.bounds.min.y + 0.05f, transform.position.z);

        int mask = groundMask.value == 0 ? ~0 : groundMask.value;
        Collider[] hits = Physics.OverlapSphere(checkPosition, groundRadius, mask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;
            if (hit == playerCollider || hit.transform.IsChildOf(transform)) continue;
            return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (playerCollider == null)
            playerCollider = GetComponent<Collider>();

        Vector3 checkPosition = (groundCheck != null && groundCheck != transform)
            ? groundCheck.position
            : (playerCollider != null
                ? new Vector3(playerCollider.bounds.center.x, playerCollider.bounds.min.y + 0.05f, transform.position.z)
                : transform.position);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(checkPosition, groundRadius);
    }

    private void UpdateFacing(float input)
    {
        if (!flipVisualScaleX) return;
        
        // Flip based on input, or if moving opposite to facing direction (jumping over enemy)
        if (Mathf.Abs(input) > 0.01f)
        {
            facingSign = Mathf.Sign(input);
        }
        else if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 1f)
        {
            // When airborne and moving, face the movement direction (handles jumping over enemy)
            float velSign = Mathf.Sign(rb.linearVelocity.x);
            if (velSign != 0 && velSign != facingSign)
            {
                facingSign = velSign;
            }
        }

        Transform target = visualRoot != null ? visualRoot : transform;
        Vector3 s = target.localScale;
        s.x = Mathf.Abs(s.x) * facingSign;
        target.localScale = s;
    }
}
