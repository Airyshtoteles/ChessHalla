using UnityEngine;
using UnityEngine.UI;

// Simple health bar UI that works with FighterHealth.
// Usage:
//  - Place this under a Canvas (Screen Space - Camera/Overlay) or a World Space Canvas as a child of the fighter.
//  - Assign 'fill' to an Image set to type = Filled (Horizontal).
//  - Call Attach(health, target, offset) or set fields in Inspector.
public class HealthBarUI : MonoBehaviour
{
    [Header("Wiring")]
    public FighterHealth health;
    public Image fill; // Image type = Filled (Horizontal)

    [Header("Follow Target (optional)")]
    public Transform target; // fighter root/head to follow
    public Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);
    public bool followInLateUpdate = true; // follow every frame

    [Header("Auto Hide on Death")] 
    public bool hideWhenDead = true;

    private Canvas cachedCanvas;
    private Camera uiCamera;

    private void Awake()
    {
        cachedCanvas = GetComponentInParent<Canvas>();
        if (cachedCanvas != null && cachedCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            uiCamera = cachedCanvas.worldCamera != null ? cachedCanvas.worldCamera : Camera.main;
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnHealthChanged += OnHealthChanged;
            OnHealthChanged(health.currentHealth, health.maxHealth);
            health.OnDeath += OnDeath;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= OnHealthChanged;
            health.OnDeath -= OnDeath;
        }
    }

    public void Attach(FighterHealth hp, Transform follow, Vector3 offset)
    {
        health = hp;
        target = follow;
        worldOffset = offset;

        // Re-subscribe safely
        OnDisable();
        OnEnable();
    }

    private void Update()
    {
        if (!followInLateUpdate) Follow();
    }

    private void LateUpdate()
    {
        if (followInLateUpdate) Follow();
    }

    private void Follow()
    {
        if (target != null)
        {
            Vector3 worldPos = target.position + worldOffset;
            if (cachedCanvas != null)
            {
                if (cachedCanvas.renderMode == RenderMode.WorldSpace)
                {
                    transform.position = worldPos;
                }
                else
                {
                    // Screen Space (Overlay/Camera)
                    var cam = uiCamera != null ? uiCamera : Camera.main;
                    Vector3 screenPos = cam != null ? cam.WorldToScreenPoint(worldPos) : worldPos;
                    transform.position = screenPos;
                }
            }
            else
            {
                // No canvas parent, treat as world object
                transform.position = worldPos;
            }
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        if (fill != null && max > 0)
        {
            fill.fillAmount = Mathf.Clamp01((float)current / max);
        }
    }

    private void OnDeath()
    {
        if (hideWhenDead) gameObject.SetActive(false);
    }
}
