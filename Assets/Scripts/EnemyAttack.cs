using UnityEngine;
using System.Linq;

public class EnemyAI : MonoBehaviour
{
    [Header("Detection Settings")]
    public float detectionRange = 8f; // jarak deteksi hero

    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float stopDistance = 1.5f;

    [Header("Attack Settings")]
    public float attackRange = 1.5f;
    public int attackDamage = 20;
    public float attackRate = 1f;
    [Tooltip("Cooldown serangan dalam detik (override attackRate jika > 0)")] public float attackCooldown = 3f;
    public LayerMask heroLayer;

    [Header("Jump Settings")]
    public float minJumpForce = 4f;
    public float maxJumpForce = 7f;
    public Vector2 jumpIntervalRange = new Vector2(3f, 5f);

    private Transform target;
    private Animator anim;
    private Rigidbody2D rb;
    private Vector2 desiredMoveDir = Vector2.zero; // computed in Update, applied in FixedUpdate
    private bool wantsToMove = false;
    [Header("Debug")]
    public bool debugLogs = false;
    public bool debugDistance = false;
    private FighterHealth myHealth;

    [Header("Scale")]
    [Tooltip("Ukuran bot dasar (tanpa flip). X akan menjadi negatif saat menghadap kiri.")]
    public Vector2 botScale = new Vector2(4.5f, 4.5f);
    private float zScale = 1f;

    [Header("Grounding")]
    public LayerMask groundMask;
    [Tooltip("Offset titik cek tanah dari pusat")] public Vector2 groundCheckOffset = new Vector2(0f, -0.5f);
    public float groundCheckRadius = 0.15f;
    [Tooltip("Jarak pandang ke depan untuk cek ada tanah di tepi")] public float lookAheadDistance = 0.6f;
    public bool avoidLedges = true;
    [Tooltip("Jarak minimum secara horizontal agar tidak tumpang tindih dengan target")]
    public float separationDistance = 0.9f;
    [Tooltip("Toleransi beda tinggi agar separation dihitung")] public float separationVerticalTolerance = 0.6f;

    [Header("Anim Fallback")]
    public string runBool = "Jalan"; // tetap set jika ada
    public string runStateName = "jalan"; // fallback state
    public string attackTrigger = "Attack";
    public string idleStateName = "Idle";

    [Header("Facing Settings")]
    [Tooltip("True jika prefab ini menghadap kanan saat flipX=false. Jika salah arah, ubah nilai ini.")]
    public bool prefabFacesRight = true;
    [Tooltip("Balik visual dengan mengubah scale X pada visualRoot (disarankan ON)")] public bool mirrorByScaleX = true;
    public Transform visualRoot; // node grafis yang di-flip
    private SpriteRenderer sr;
    private bool initialFlipX;
    private Vector3 visualBaseScale = Vector3.one;
    private float visualBaseSignX = 1f;
    [Header("Facing Debug")]
    public bool facingDebug = false;

    [Header("Jump (optional)")]
    public bool allowRandomJump = false;

    [Header("Upright / Rotation")]
    [Tooltip("Bekukan rotasi Rigidbody2D supaya tidak berputar")] public bool freezeRotation = true;
    [Tooltip("Paksa rotasi Z = 0 setiap physics step (cadangan jika ada gaya yang memutar)")] public bool forceUpright = true;
    private float uprightZ = 0f;

    private float nextAttackTime = 0f;
    private float nextJumpTime = 0f;

    void Start()
    {
        GameObject hero = GameObject.FindGameObjectWithTag("Hero");
        if (hero != null)
        {
            target = hero.transform;
        }
        // Fallback ke pencarian berdasarkan tim jika tag tidak ditemukan
        if (target == null)
        {
            FindTargetByTeam();
        }

    anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        myHealth = GetComponent<FighterHealth>();
    sr = GetComponentInChildren<SpriteRenderer>();
    if (sr != null) initialFlipX = sr.flipX;
    if (visualRoot == null) visualRoot = sr != null ? sr.transform : transform;
    if (visualRoot != null)
    {
        visualBaseScale = visualRoot.localScale;
        visualBaseSignX = Mathf.Sign(visualBaseScale.x);
        if (Mathf.Approximately(visualBaseSignX, 0f)) visualBaseSignX = 1f;
    }
    zScale = transform.localScale.z;
    // set ukuran root konsisten (positif), flipping dilakukan di visualRoot agar tidak bentrok physics
    transform.localScale = new Vector3(Mathf.Abs(botScale.x), botScale.y, zScale);
    // matikan flipX agar tidak bentrok dengan animasi
    if (sr != null) sr.flipX = false;
    // set facing awal ke kanan
    ApplyFacingByDirection(+1f);
            if (rb != null && freezeRotation)
            {
                rb.freezeRotation = true;
                rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
            }

        SetNextJumpTime();
    }

    void Update()
    {
        if (target == null)
        {
            TryReacquireHero();
            return;
        }

    float distance = Vector2.Distance(transform.position, target.position);
    if (debugDistance) Debug.Log($"{name} -> dist:{distance:F2}, detect:{detectionRange}, stop:{stopDistance}");

        // --- Cek apakah hero dalam jarak deteksi ---
        if (distance <= detectionRange)
        {
            // beri hysteresis 0.1 agar tidak jitter di batas
            if (distance > stopDistance + 0.1f)
            {
                MoveTowardsHero();
            }
            else
            {
                StopMoving();
                TryAttack();
            }
        }
        else
        {
            StopMoving(); // diam kalau hero terlalu jauh
        }

        // --- Loncat random setiap beberapa detik (opsional) ---
        if (allowRandomJump && IsGrounded() && Time.time >= nextJumpTime)
        {
            RandomJump();
            SetNextJumpTime();
        }

        // Selalu evaluasi facing bahkan saat berhenti / menyerang
        float xDelta = target.position.x - transform.position.x;
        ApplyFacingByDirection(xDelta);
        if (facingDebug)
        {
            float visX = visualRoot != null ? visualRoot.localScale.x : transform.localScale.x;
            bool fx = sr != null ? sr.flipX : false;
            Debug.Log($"[Facing] {name} xDelta={xDelta:F2} visualScaleX={visX:F2} flipX={fx}");
        }
    }

    void MoveTowardsHero()
    {
        if (anim) {
            if (!string.IsNullOrEmpty(runBool)) anim.SetBool(runBool, true);
            // fallback state play (tidak mengganggu jika transition belum di-setup)
            if (!string.IsNullOrEmpty(runStateName)) anim.Play(runStateName);
        }
        Vector2 direction = (target.position - transform.position).normalized;
        // Hindari jatuh dari tepi jika diminta
        if (avoidLedges && !HasGroundAhead(Mathf.Sign(direction.x)))
        {
            wantsToMove = false;
            desiredMoveDir = Vector2.zero;
        }
        else if (!IsGrounded())
        {
            // kalau di udara, jangan gerak horizontal agresif
            wantsToMove = false;
            desiredMoveDir = Vector2.zero;
        }
        else
        {
            // Jangan saling tumpang tindih terlalu dekat
            if (target != null)
            {
                float dx = Mathf.Abs(target.position.x - transform.position.x);
                float dy = Mathf.Abs(target.position.y - transform.position.y);
                if (dx <= separationDistance && dy <= separationVerticalTolerance)
                {
                    wantsToMove = false;
                    desiredMoveDir = Vector2.zero;
                }
                else
                {
                    desiredMoveDir = direction;
                    wantsToMove = true;
                }
            }
            else
            {
                desiredMoveDir = direction;
                wantsToMove = true;
            }
        }

        // Balik arah sprite biar hadap ke hero
        ApplyFacingByDirection(direction.x);
    }

    void StopMoving()
    {
        if (anim)
        {
            if (!string.IsNullOrEmpty(runBool)) anim.SetBool(runBool, false);
            if (!string.IsNullOrEmpty(idleStateName)) anim.Play(idleStateName);
        }
        wantsToMove = false;
        desiredMoveDir = Vector2.zero;
    }

    void TryAttack()
    {
        if (Time.time < nextAttackTime) return;
        // Attack target by distance (tidak tergantung layer mask)
        if (target != null)
        {
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= attackRange)
            {
                if (anim && !string.IsNullOrEmpty(attackTrigger)) anim.SetTrigger(attackTrigger);
                // Try to find FighterHealth on target, its parents or children
                FighterHealth fh = null;
                if (target != null)
                {
                    fh = target.GetComponent<FighterHealth>();
                    if (fh == null) fh = target.GetComponentInParent<FighterHealth>();
                    if (fh == null) fh = target.GetComponentInChildren<FighterHealth>();
                }
                if (fh != null)
                {
                    if (fh.IsAlive)
                    {
                        fh.ApplyDamage(attackDamage);
                        if (debugLogs) Debug.Log($"{name} attacked {fh.name} for {attackDamage}");
                    }
                    else if (debugLogs) Debug.Log($"{name} attack: target {fh.name} already dead");
                }
                else
                {
                    if (debugLogs) Debug.LogWarning($"{name} attack: no FighterHealth found on target {target.name}");
                }
                nextAttackTime = Time.time + (attackCooldown > 0f ? attackCooldown : (1f / Mathf.Max(0.01f, attackRate)));
                return;
            }
        }
        // fallback ke overlap circle jika target tidak ada
        Collider2D hitHero = Physics2D.OverlapCircle(transform.position, attackRange, heroLayer);
        if (hitHero != null)
        {
            if (anim && !string.IsNullOrEmpty(attackTrigger)) anim.SetTrigger(attackTrigger);
            FighterHealth fh = hitHero.GetComponent<FighterHealth>();
            if (fh == null) fh = hitHero.GetComponentInParent<FighterHealth>();
            if (fh == null) fh = hitHero.GetComponentInChildren<FighterHealth>();
            if (fh != null && fh.IsAlive)
            {
                fh.ApplyDamage(attackDamage);
                if (debugLogs) Debug.Log($"{name} overlap attacked {fh.name} for {attackDamage}");
            }
            else if (debugLogs) Debug.LogWarning($"{name} overlap attack found no FighterHealth or target dead");
            nextAttackTime = Time.time + (attackCooldown > 0f ? attackCooldown : (1f / Mathf.Max(0.01f, attackRate)));
        }
    }

    void TryReacquireHero()
    {
        GameObject hero = GameObject.FindGameObjectWithTag("Hero");
        if (hero != null)
        {
            target = hero.transform;
            if (debugLogs) Debug.Log($"{name} reacquired target: {hero.name}");
            return;
        }

        // Fallback: cari lawan berdasarkan FighterHealth.Team
        FindTargetByTeam();
    }

    void FindTargetByTeam()
    {
        var all = FindObjectsOfType<FighterHealth>();
        if (all == null || all.Length == 0) return;
        FighterHealth mine = myHealth != null ? myHealth : GetComponent<FighterHealth>();
        var candidates = all.Where(h => h != null && h != mine && h.IsAlive);
        if (mine != null)
            candidates = candidates.Where(h => h.Team != mine.Team);
        var best = candidates
            .OrderBy(h => Vector2.Distance(transform.position, h.transform.position))
            .FirstOrDefault();
        if (best != null)
        {
            target = best.transform;
            if (debugLogs) Debug.Log($"{name} target by team: {best.name} (team {best.Team})");
        }
    }

    void RandomJump()
    {
        float jumpForce = Random.Range(minJumpForce, maxJumpForce);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

        if (anim) anim.SetTrigger("Jump");
    }

    void SetNextJumpTime()
    {
        nextJumpTime = Time.time + Random.Range(jumpIntervalRange.x, jumpIntervalRange.y);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, stopDistance);
    }

    void OnValidate()
    {
        if (detectionRange < stopDistance) detectionRange = stopDistance + 0.5f;
        if (stopDistance < attackRange * 0.5f) stopDistance = attackRange * 0.6f; // jaga supaya tidak terlalu kecil
    }

    void FixedUpdate()
    {
        // Terapkan gerak di physics step supaya konsisten
        if (!wantsToMove) {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            return;
        }
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            // Gerak via velocity supaya tidak kebentur constraint posisi
            rb.linearVelocity = new Vector2(desiredMoveDir.x * moveSpeed, rb.linearVelocity.y);
            if ((rb.constraints & RigidbodyConstraints2D.FreezePositionX) != 0 && debugLogs)
            {
                Debug.LogWarning($"{name}: Rigidbody2D FreezePositionX aktif—bot tidak bisa jalan ke sumbu X");
            }
        }
        else
        {
            // Fallback bila bukan Dynamic
            transform.position += (Vector3)(desiredMoveDir * moveSpeed * Time.fixedDeltaTime);
        }
    }

    void LateUpdate()
    {
        if (!forceUpright) return;
        var e = transform.eulerAngles;
        if (Mathf.Abs(Mathf.DeltaAngle(e.z, uprightZ)) > 0.01f)
        {
            transform.rotation = Quaternion.Euler(e.x, e.y, uprightZ);
        }
    }

    bool IsGrounded()
    {
        Vector2 origin = (Vector2)transform.position + groundCheckOffset;
        return Physics2D.OverlapCircle(origin, groundCheckRadius, groundMask) != null;
    }

    bool HasGroundAhead(float dirSign)
    {
        Vector2 start = (Vector2)transform.position + new Vector2(lookAheadDistance * dirSign, 0.1f);
        RaycastHit2D hit = Physics2D.Raycast(start, Vector2.down, 1.5f, groundMask);
        return hit.collider != null;
    }

    // ===== Facing helpers =====
    void ApplyFacingByDirection(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.001f) return;
        bool faceRight = dirX > 0f;

        // Flip visual deterministik via scale X pada visualRoot
        if (mirrorByScaleX && visualRoot != null)
        {
            float sign = faceRight ? visualBaseSignX : -visualBaseSignX;
            Vector3 s = visualBaseScale;
            s.x = Mathf.Abs(s.x) * sign;
            visualRoot.localScale = s;
        }
        // Pastikan Animator tidak menimpa flipX
        if (sr != null) sr.flipX = false;
    }
}
