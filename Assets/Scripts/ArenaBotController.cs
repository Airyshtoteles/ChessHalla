using UnityEngine;
using System.Linq;

// Attach this to the enemy fighter root (same GameObject that has Rigidbody2D & FighterHealth)
// Animator expectations:
//  - Trigger: "Attack" (fires attack animation)
//  - Bool: "Jalan" (true when moving / chasing)
//  - Idle is a state name (optional direct Play fallback if parameters fail)
// Behaviour:
//  - Finds nearest opposing FighterHealth within detectRange.
//  - Moves horizontally toward target until within stopDistance.
//  - When within attackRange and cooldown elapsed, triggers attack.
//  - Damage can be applied via animation event calling PerformDamageEvent() or immediate after delay.
//  - If no target found: stays Idle.
//  - Stops all action when this health or target dies.
// Optional: You can wire animation event on the attack clip to call PerformDamageEvent for frame-perfect damage.
public class ArenaBotController : MonoBehaviour
{
    [Header("References")] public Animator animator; // auto-resolved if null
    public FighterHealth myHealth; // auto-resolved if null
    public Rigidbody2D rb; // auto-resolved if null

    [Header("Combat")]
    public float detectRange = 6f;
    public float attackRange = 1.6f;
    public float stopDistance = 1.2f; // distance to stop before overlapping
    public float moveSpeed = 3f;
    public int attackDamage = 10;
    public float attackCooldown = 1.25f;
    public float attackDamageDelay = 0.25f; // seconds after trigger to apply damage if not using animation event

    [Header("Animation Parameters")]
    public string attackTrigger = "Attack";
    public string runBool = "Jalan"; // moving
    public string idleStateName = "Idle"; // optional fallback

    [Header("Facing")]
    public bool flipViaScaleX = true; // if true scale.x flips, else spriteRenderer flipX if available
    public SpriteRenderer spriteRenderer; // optional

    [Header("Debug")]
    public bool debugLogs = false;

    private FighterHealth currentTarget;
    private float lastAttackTime = -999f;
    private bool pendingDamageRoutine;

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!myHealth) myHealth = GetComponent<FighterHealth>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (myHealth != null) myHealth.OnDeath += OnSelfDeath;
    }

    private void OnDisable()
    {
        if (myHealth != null) myHealth.OnDeath -= OnSelfDeath;
        if (currentTarget != null) currentTarget.OnDeath -= OnTargetDeath;
    }

    private void Update()
    {
        if (myHealth == null || !myHealth.IsAlive) return;
        AcquireOrValidateTarget();
        HandleMovementAndFacing();
        TryAttack();
    }

    private void AcquireOrValidateTarget()
    {
        if (currentTarget != null && (!currentTarget.IsAlive || Vector2.Distance(transform.position, currentTarget.transform.position) > detectRange * 1.2f))
        {
            currentTarget.OnDeath -= OnTargetDeath;
            currentTarget = null;
        }
        if (currentTarget != null) return;

        // Find all FighterHealth with different team
    var all = FindObjectsOfType<FighterHealth>();
    var opponents = all.Where(h => h != myHealth && h.IsAlive && h.Team != myHealth.Team);
        float bestDist = float.MaxValue;
        FighterHealth best = null;
        foreach (var opp in opponents)
        {
            float d = Vector2.Distance(transform.position, opp.transform.position);
            if (d <= detectRange && d < bestDist)
            {
                bestDist = d; best = opp;
            }
        }
        if (best != null)
        {
            currentTarget = best;
            currentTarget.OnDeath += OnTargetDeath;
            if (debugLogs) Debug.Log(name + " target acquired: " + best.name);
        }
    }

    private void HandleMovementAndFacing()
    {
        bool moving = false;
        if (currentTarget != null)
        {
            Vector2 pos = transform.position;
            Vector2 targetPos = currentTarget.transform.position;
            float dx = targetPos.x - pos.x;
            float absDx = Mathf.Abs(dx);

            if (absDx > stopDistance)
            {
                float dir = Mathf.Sign(dx);
                Vector2 vel = rb.linearVelocity;
                vel.x = dir * moveSpeed;
                rb.linearVelocity = vel;
                moving = true;
            }
            else
            {
                // slow / stop
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }

            // Facing
            if (flipViaScaleX)
            {
                Vector3 s = transform.localScale;
                s.x = dx < 0 ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
                transform.localScale = s;
            }
            else if (spriteRenderer)
            {
                spriteRenderer.flipX = dx < 0;
            }
        }
        else
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        // Animation run bool
        if (animator && !string.IsNullOrEmpty(runBool))
        {
            animator.SetBool(runBool, moving);
        }
        else if (animator && !moving && !string.IsNullOrEmpty(idleStateName))
        {
            // fallback play idle if parameters missing
            animator.Play(idleStateName);
        }
    }

    private void TryAttack()
    {
        if (currentTarget == null) return;
        if (Time.time < lastAttackTime + attackCooldown) return;
        float dist = Vector2.Distance(transform.position, currentTarget.transform.position);
        if (dist > attackRange) return;

        lastAttackTime = Time.time;
        if (animator && !string.IsNullOrEmpty(attackTrigger))
        {
            animator.SetTrigger(attackTrigger);
        }
        else if (animator && !string.IsNullOrEmpty("attack"))
        {
            // fallback play
            animator.Play("attack");
        }

        if (attackDamageDelay > 0f)
        {
            if (!pendingDamageRoutine) StartCoroutine(DelayedDamage());
        }
        else
        {
            ApplyDamageNow();
        }
    }

    private System.Collections.IEnumerator DelayedDamage()
    {
        pendingDamageRoutine = true;
        yield return new WaitForSeconds(attackDamageDelay);
        ApplyDamageNow();
        pendingDamageRoutine = false;
    }

    // Call this from animation event if you prefer frame-accurate damage
    public void PerformDamageEvent()
    {
        ApplyDamageNow();
    }

    private void ApplyDamageNow()
    {
        if (currentTarget == null || !currentTarget.IsAlive) return;
        currentTarget.ApplyDamage(attackDamage);
        if (debugLogs) Debug.Log(name + " dealt " + attackDamage + " to " + currentTarget.name);
    }

    private void OnTargetDeath()
    {
        if (debugLogs) Debug.Log(name + " target died");
        if (currentTarget != null) currentTarget.OnDeath -= OnTargetDeath;
        currentTarget = null;
    }

    private void OnSelfDeath()
    {
        if (debugLogs) Debug.Log(name + " died; disabling controller");
        rb.linearVelocity = Vector2.zero;
        enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, stopDistance);
    }

    private void OnValidate()
    {
        if (detectRange < attackRange) detectRange = attackRange + 0.5f;
        if (stopDistance > attackRange) stopDistance = attackRange * 0.75f; // keep stop < attackRange usually
    }
}
