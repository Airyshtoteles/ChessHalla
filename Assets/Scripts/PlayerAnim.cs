using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;
    public float jumpForce = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;      // Empty di bawah kaki
    public float groundRadius = 0.15f;
    public LayerMask groundMask;

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sprite;

    private float moveInput;
    private bool isGrounded;
    private float jumpCooldown = 1f;   // Durasi cooldown loncat
    private float jumpTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();

        rb.gravityScale = 1f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        // === Cek apakah menyentuh tanah ===
        if (groundCheck)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundMask);
        else
            isGrounded = true; // fallback kalau lupa assign di Inspector

        // === Input gerakan horizontal ===
        moveInput = Input.GetAxisRaw("Horizontal");

        // === Flip arah karakter ===
        if (moveInput < 0) sprite.flipX = true;
        else if (moveInput > 0) sprite.flipX = false;

        // === Update animasi jalan / idle ===
        bool isMoving = Mathf.Abs(moveInput) > 0.01f;
        anim.SetBool("Jalan", isMoving);

        // === Attack ===
        if (Input.GetMouseButtonDown(0))
        {
            // Reset animasi jalan agar tidak bentrok
            anim.ResetTrigger("Attack");
            anim.SetTrigger("Attack");
        }

        // === Jump (dengan cooldown) ===
        if ((Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space)) && 
            Time.time >= jumpTimer && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpTimer = Time.time + jumpCooldown;
        }
    }

    void FixedUpdate()
    {
        // === Gerak horizontal ===
        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
    }

    // === Debug visual untuk ground check ===
    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
    }
}
