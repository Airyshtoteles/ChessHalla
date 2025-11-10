using UnityEngine;

[RequireComponent(typeof(FighterHealth))]
public class FighterController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;

    [Header("Attack")]
    [SerializeField] private int attack1Damage = 25;
    [SerializeField] private int attack2Damage = 40;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 0.4f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string attack1Trigger = "Attack1";
    [SerializeField] private string attack2Trigger = "Attack2";
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string runBool = "Running";

    [Header("Facing (choose one method)")]
    [Tooltip("Jika diisi, transform visual (sprite/graphics) yang akan di-flip/rotate. Jika kosong, pakai child SpriteRenderer's transform.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Gunakan SpriteRenderer.flipX untuk menghadap kiri/kanan")] [SerializeField] private bool useSpriteFlipX = true;
    [Tooltip("Jika tidak pakai flipX, gunakan rotasi Y 180° untuk menghadap kiri")] [SerializeField] private bool rotateYForLeft = false;
    [Tooltip("Jika dua-duanya false, maka gunakan scale.x = -1 untuk menghadap kiri")] [SerializeField] private bool scaleXForLeft = false;
    [Tooltip("Untuk beberapa sprite, arah flipX mungkin terbalik—aktifkan untuk menukar logika flipX")] [SerializeField] private bool invertXFlipForLeft = false;

    private FighterHealth health;
    private FighterController2D opponent;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Vector3 visualDefaultScale;
    private Quaternion visualDefaultRot;

    private bool isPlayerControlled = true;
    private bool canAttack = true;
    private bool facingRight = true;
    private bool isAI = false;
    private PieceTeam team;

    private float aiAttackDelay = 0.6f;
    private float aiNextAttackTime = 0f;

    public void Initialize(PieceTeam team, bool isPlayerControlled)
    {
        this.team = team;
        this.isPlayerControlled = isPlayerControlled;
        this.isAI = !isPlayerControlled;
        health = GetComponent<FighterHealth>();
        if (health != null) health.Initialize(team);
    }

    public void SetOpponent(FighterController2D other)
    {
        opponent = other;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
        }
        sr = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (visualRoot == null)
        {
            visualRoot = sr != null ? sr.transform : transform;
        }
        visualDefaultScale = visualRoot.localScale;
        visualDefaultRot = visualRoot.localRotation;
    }

    private void Update()
    {
        if (isPlayerControlled)
        {
            HandlePlayerInput();
        }
        else if (isAI)
        {
            HandleAI();
        }
        UpdateFacing();
    }

    private void HandlePlayerInput()
    {
        float move = 0f;
        if (Input.GetKey(KeyCode.A)) move -= 1f;
        if (Input.GetKey(KeyCode.D)) move += 1f;

        Vector2 vel = rb.linearVelocity;
        vel.x = move * moveSpeed;
        rb.linearVelocity = vel;

        if (animator != null && !string.IsNullOrEmpty(runBool)) animator.SetBool(runBool, Mathf.Abs(move) > 0.01f);

        if ((Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space)) && IsGrounded())
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            if (animator != null && !string.IsNullOrEmpty(jumpTrigger)) animator.SetTrigger(jumpTrigger);
        }

        if (Input.GetMouseButtonDown(0)) TryAttack(1);
        if (Input.GetMouseButtonDown(1)) TryAttack(2);
    }

    private void HandleAI()
    {
        if (opponent == null) return;

        // Move towards player if far
        float dist = Mathf.Abs(opponent.transform.position.x - transform.position.x);
        if (dist > attackRange * 0.7f)
        {
            float dir = Mathf.Sign(opponent.transform.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(dir * moveSpeed * 0.7f, rb.linearVelocity.y);
            if (animator != null && !string.IsNullOrEmpty(runBool)) animator.SetBool(runBool, true);
        }
        else
        {
            if (animator != null && !string.IsNullOrEmpty(runBool)) animator.SetBool(runBool, false);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        if (Time.time >= aiNextAttackTime && dist <= attackRange + 0.2f)
        {
            int which = Random.value < 0.6f ? 1 : 2;
            TryAttack(which);
            aiNextAttackTime = Time.time + aiAttackDelay;
        }
    }

    private void TryAttack(int which)
    {
        if (!canAttack) return;
        StartCoroutine(AttackRoutine(which));
    }

    private System.Collections.IEnumerator AttackRoutine(int which)
    {
        canAttack = false;
        string trig = which == 1 ? attack1Trigger : attack2Trigger;
        int dmg = which == 1 ? attack1Damage : attack2Damage;

        if (animator != null && !string.IsNullOrEmpty(trig)) animator.SetTrigger(trig);

        // Small wind-up (sync with animation if needed)
        yield return new WaitForSeconds(0.15f);
        ApplyDamageToOpponent(dmg);

        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    private void ApplyDamageToOpponent(int dmg)
    {
        if (opponent == null) return;
        float dist = Vector2.Distance(transform.position, opponent.transform.position);
        if (dist <= attackRange)
        {
            var hp = opponent.GetComponent<FighterHealth>();
            if (hp != null) hp.ApplyDamage(dmg);
        }
    }

    private bool IsGrounded()
    {
        if (groundCheck == null) return true; // fallback: dianggap selalu di tanah kalau tidak di-set
        Collider2D hit = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
        return hit != null;
    }

    private void UpdateFacing()
    {
        if (opponent == null) return;
        bool shouldFaceRight = opponent.transform.position.x > transform.position.x;
        if (shouldFaceRight != facingRight)
        {
            facingRight = shouldFaceRight;
            ApplyFacing();
        }
    }

    public void FaceLeft()
    {
        facingRight = false;
        ApplyFacing();
    }

    public void FaceRight()
    {
        facingRight = true;
        ApplyFacing();
    }

    private void ApplyFacing()
    {
        // Prefer SpriteRenderer.flipX if chosen
        if (useSpriteFlipX && sr != null)
        {
            bool faceLeft = !facingRight;
            sr.flipX = invertXFlipForLeft ? !faceLeft : faceLeft;
            // Reset other transforms to defaults
            if (visualRoot != null)
            {
                visualRoot.localScale = visualDefaultScale;
                visualRoot.localRotation = visualDefaultRot;
            }
            return;
        }

        // Else use rotation Y
        if (rotateYForLeft && visualRoot != null)
        {
            bool faceLeft = !facingRight;
            float yRot = faceLeft ? 180f : 0f;
            visualRoot.localRotation = Quaternion.Euler(visualDefaultRot.eulerAngles.x, yRot, visualDefaultRot.eulerAngles.z);
            visualRoot.localScale = visualDefaultScale;
            if (sr != null) sr.flipX = false;
            return;
        }

        // Else use scale X mirroring
        if (scaleXForLeft && visualRoot != null)
        {
            bool faceLeft = !facingRight;
            Vector3 s = visualDefaultScale;
            s.x = Mathf.Abs(s.x) * (faceLeft ? -1f : 1f);
            visualRoot.localScale = s;
            visualRoot.localRotation = visualDefaultRot;
            if (sr != null) sr.flipX = false;
            return;
        }

        // Default fallback to flipX if nothing selected
        if (sr != null)
        {
            bool faceLeft = !facingRight;
            sr.flipX = invertXFlipForLeft ? !faceLeft : faceLeft;
        }
    }
}
