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
    [Header("Restart")]
    [Tooltip("Boleh klik berkali-kali untuk restart board dan spawn ulang")] [SerializeField] private bool allowRestart = true;
    [Tooltip("Bersihkan semua bidak sebelum spawn ulang")] [SerializeField] private bool clearBoardOnRestart = true;
    [Tooltip("Reset kondisi GameOver agar kamera win/lose kembali ke chess")] [SerializeField] private bool resetGameOverState = true;

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
        if (!allowRestart && started)
            return;

        StartCoroutine(DoStartFlow());
    }

    // Klik collider (pastikan ada Collider/Collider2D). Untuk UI Button, panggil StartGame() langsung.
    private void OnMouseDown()
    {
        StartGame();
    }

    private void Update()
    {
        if (!enableRectClick) return;
        if (!allowRestart && started) return;
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
        bool wasStarted = started;

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

        // 2) Pastikan referensi chessCamera ada (fallback cari berdasarkan nama)
        if (chessCamera == null)
        {
            chessCamera = FindChessCameraFallback();
        }
        // Aktifkan kamera chess dan set tag MainCamera (selalu aman dilakukan)
        if (chessCamera != null)
        {
            ActivateCamera(chessCamera);
        }

        // 3) Reset/game over state jika restart
        if (resetGameOverState && GameOverManager.Instance != null)
        {
            GameOverManager.Instance.ResetState(switchToChessCam: true);
        }

        // 4) Bersihkan board jika restart
        var board = FindFirstObjectByType<BoardLogic>(FindObjectsInactive.Exclude);
        if (allowRestart && wasStarted && clearBoardOnRestart && board != null)
        {
            board.ClearAll(destroyObjects: true);
        }

        // 4.5) Pastikan turn manager tidak busy
        var tm = FindFirstObjectByType<TurnManager>(FindObjectsInactive.Exclude);
        if (tm != null)
        {
            tm.SetBusy(false);
        }

        // 5) Spawn (trigger setiap klik jika allowRestart; kalau tidak, hanya sekali)
        var spawner = FindFirstObjectByType<RandomPieceSpawner>(FindObjectsInactive.Exclude);
        if (triggerSpawnOnStart && spawner != null)
        {
            if (allowRestart && wasStarted)
            {
                spawner.ResetSpawnFlag();
                spawner.TriggerSpawn();
            }
            else if (!wasStarted)
            {
                spawner.TriggerSpawn();
            }
        }
        else if (spawner == null)
        {
            Debug.LogWarning("[StartGameController] RandomPieceSpawner tidak ditemukan di scene ini.");
        }

        // 6) Sembunyikan tombol (opsional; kalau restart aktif biasanya tidak disembunyikan)
        if (hideThisObjectAfterStart && !allowRestart)
        {
            gameObject.SetActive(false);
        }

        // Set flag bahwa sudah pernah start minimal sekali
        if (!wasStarted) started = true;
    }

    // Cari kamera dengan nama yang mengandung "chess" atau "board" sebagai fallback
    private Camera FindChessCameraFallback()
    {
        var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Camera best = null;
        foreach (var c in cams)
        {
            string n = c.gameObject.name.ToLowerInvariant();
            if (n.Contains("chess") || n.Contains("board"))
            {
                best = c; break;
            }
        }
        // Jika tidak ditemukan berdasarkan nama, pilih kamera yang bukan startCamera
        if (best == null)
        {
            foreach (var c in cams)
            {
                if (startCamera != null && c == startCamera) continue;
                best = c; break;
            }
        }
        return best;
    }

    // Aktifkan 1 kamera dan matikan yang lain, serta set tag MainCamera
    private void ActivateCamera(Camera target)
    {
        var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in cams)
        {
            bool on = (c == target);
            c.enabled = on;
            if (on)
            {
                c.gameObject.tag = "MainCamera";
            }
            else if (c.gameObject.tag == "MainCamera")
            {
                c.gameObject.tag = "Untagged";
            }
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
