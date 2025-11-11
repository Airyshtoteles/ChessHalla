using UnityEngine;

public class SpriteCharacterController : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;

    [Header("Animations")]
    public Sprite[] idleFrames;
    public Sprite[] walkFrames;
    public Sprite[] attack1Frames;
    public Sprite[] attack2Frames;

    public float frameRate = 0.15f;
    public float moveSpeed = 3f;

    private float frameTimer;
    private int currentFrame;
    private Sprite[] currentAnimation;

    private Vector2 moveInput;

    void Start()
    {
        currentAnimation = idleFrames;
    }

    void Update()
    {
        HandleMovement();
        HandleAttack();
        Animate();
    }

    void HandleMovement()
    {
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        transform.position += (Vector3)moveInput.normalized * moveSpeed * Time.deltaTime;

        if (moveInput.magnitude > 0)
        {
            currentAnimation = walkFrames;
            spriteRenderer.flipX = moveInput.x < 0; // balik arah
        }
        else
        {
            currentAnimation = idleFrames;
        }
    }

    void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0)) // klik kiri
        {
            currentAnimation = attack1Frames;
            currentFrame = 0;
        }

        if (Input.GetMouseButtonDown(1)) // klik kanan
        {
            currentAnimation = attack2Frames;
            currentFrame = 0;
        }
    }

    void Animate()
    {
        if (currentAnimation.Length == 0) return;

        frameTimer += Time.deltaTime;
        if (frameTimer >= frameRate)
        {
            frameTimer = 0f;
            currentFrame = (currentFrame + 1) % currentAnimation.Length;
            spriteRenderer.sprite = currentAnimation[currentFrame];
        }
    }
}
