using UnityEngine;

// Versi sederhana untuk klik collider: saat objek ini di-klik, kamera awal dimatikan dan kamera chess diaktifkan.
// Tidak ada pergantian scene, hanya satu kali transisi.
// Pasang script ini pada GameObject yang punya Collider (2D atau 3D) sebagai "Start".
public class StartGameController : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("Kamera awal yang aktif saat aplikasi baru dibuka")] [SerializeField] private Camera startCamera;
    [Tooltip("Kamera gameplay (chess) yang diaktifkan setelah start")] [SerializeField] private Camera chessCamera;

    [Header("Spawn Control")]
    [Tooltip("Panggil RandomPieceSpawner.TriggerSpawn() sekali setelah start")] [SerializeField] private bool triggerSpawnOnStart = true;

    [Header("Behaviour")]
    [Tooltip("Sembunyikan/disable GameObject tombol ini setelah diklik")] [SerializeField] private bool hideThisObjectAfterStart = true;
    [Tooltip("Set kamera awal aktif & kamera chess nonaktif saat awal play")] [SerializeField] private bool initializeCameraStates = true;

    [Header("Animator (opsional)")]
    [Tooltip("Animator untuk memainkan animasi tombol saat diklik (opsional)")] [SerializeField] private Animator buttonAnimator;
    [Tooltip("Nama Trigger di Animator yang akan di-set saat klik")] [SerializeField] private string animatorTrigger = "Click";
    [Tooltip("Atau mainkan state tertentu secara langsung (kosongkan jika pakai Trigger)")] [SerializeField] private string animatorStateToPlay = "";
    [Tooltip("Tunggu beberapa detik setelah memicu animasi sebelum pindah kamera")] [SerializeField] private float waitAfterAnimSeconds = 0.15f;
    [Tooltip("Matikan Animator saat awal agar tidak autoplay, lalu nyalakan saat klik")] [SerializeField] private bool disableAnimatorUntilClick = true;

    [Header("Click Tanpa Collider (opsional)")]
    [Tooltip("Aktifkan jika tidak menggunakan Collider; klik diuji memakai area kotak lokal")] [SerializeField] private bool enableRectClick = false;
    [Tooltip("Area klik dalam koordinat lokal anchor (x,y dari pusat anchor). Misal: x=-1,y=-0.3,width=2,height=0.6")] [SerializeField] private Rect localClickRect = new Rect(-1f, -0.3f, 2f, 0.6f);
    [Tooltip("Transform yang jadi acuan rect (default: transform ini)")] [SerializeField] private Transform rectAnchor;
    [SerializeField] private bool drawRectGizmo = true;

    private bool started;

    private void Awake()
    {
        if (initializeCameraStates)
        {
            if (startCamera != null) startCamera.enabled = true;
            if (chessCamera != null) chessCamera.enabled = false;
        }

        // Cegah animasi autoplay (parameter default atau state default memutar sendiri)
        if (buttonAnimator != null && disableAnimatorUntilClick)
        {
            buttonAnimator.enabled = false;
        }
    }

    // Dipanggil oleh collider click (OnMouseDown) atau bisa juga dari UI Button/manual.
    public void StartGame()
    {
        if (started) return;
        started = true;

        StartCoroutine(DoStartFlow());
    }

    // Klik collider (pastikan ada Collider/Collider2D). Untuk UI Button, panggil StartGame() langsung.
    private void OnMouseDown()
    {
        StartGame();
    }

    private void Update()
    {
        if (!enableRectClick || started) return;
        if (!Input.GetMouseButtonDown(0)) return;

        var cam = Camera.main != null ? Camera.main : startCamera;
        if (cam == null) return;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;
        Transform anchor = rectAnchor != null ? rectAnchor : transform;
        Vector3 local = anchor.InverseTransformPoint(world);
        if (localClickRect.Contains(new Vector2(local.x, local.y)))
        {
            StartGame();
        }
    }

    private System.Collections.IEnumerator DoStartFlow()
    {
        // 1) Mainkan animasi tombol (jika ada)
        if (buttonAnimator != null)
        {
            if (!buttonAnimator.enabled) buttonAnimator.enabled = true;
            if (!string.IsNullOrEmpty(animatorTrigger))
            {
                buttonAnimator.ResetTrigger(animatorTrigger);
                buttonAnimator.SetTrigger(animatorTrigger);
            }
            else if (!string.IsNullOrEmpty(animatorStateToPlay))
            {
                buttonAnimator.Play(animatorStateToPlay, 0, 0f);
            }

            if (waitAfterAnimSeconds > 0f)
            {
                yield return new WaitForSeconds(waitAfterAnimSeconds);
            }
        }

        // 2) Pindah kamera
        if (startCamera != null) startCamera.enabled = false;
        if (chessCamera != null) chessCamera.enabled = true;

        // 3) Spawn sekali
        if (triggerSpawnOnStart)
        {
            var spawner = FindFirstObjectByType<RandomPieceSpawner>(FindObjectsInactive.Exclude);
            if (spawner != null)
            {
                spawner.TriggerSpawn();
            }
            else
            {
                Debug.LogWarning("[StartGameController] RandomPieceSpawner tidak ditemukan di scene ini.");
            }
        }

        // 4) Sembunyikan tombol (opsional)
        if (hideThisObjectAfterStart)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawRectGizmo || !enableRectClick) return;
        Transform anchor = rectAnchor != null ? rectAnchor : transform;
        Vector3 p0 = anchor.TransformPoint(new Vector3(localClickRect.xMin, localClickRect.yMin, 0));
        Vector3 p1 = anchor.TransformPoint(new Vector3(localClickRect.xMax, localClickRect.yMin, 0));
        Vector3 p2 = anchor.TransformPoint(new Vector3(localClickRect.xMax, localClickRect.yMax, 0));
        Vector3 p3 = anchor.TransformPoint(new Vector3(localClickRect.xMin, localClickRect.yMax, 0));

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f);
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);
    }
}
