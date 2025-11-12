using UnityEngine;
using System.Linq;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }
    [Header("Cameras")]
    [Tooltip("Kamera utama papan (akan dimatikan saat menampilkan hasil)")]
    [SerializeField] private Camera chessCamera;
    [Tooltip("Kamera yang dipakai saat YOU WIN")] [SerializeField] private Camera youWinCamera;
    [Tooltip("Kamera yang dipakai saat YOU LOSE")] [SerializeField] private Camera youLoseCamera;

    [Header("UI (optional)")]
    [SerializeField] private bool showBanner = true;
    [SerializeField] private string winText = "YOU WIN";
    [SerializeField] private string loseText = "YOU LOSE";

    private BoardLogic board;
    private TurnManager turnMgr;
    private bool gameOver = false;
    private bool playerWon = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (board == null) board = FindFirstObjectByType<BoardLogic>();
        if (turnMgr == null) turnMgr = FindFirstObjectByType<TurnManager>();
        if (chessCamera == null)
        {
            var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            chessCamera = cams.FirstOrDefault(c => c.enabled);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsGameOver() => gameOver;

    public void CheckGameOver()
    {
        if (gameOver || board == null) return;
        int white = 0, black = 0;
        foreach (var p in board.GetAllPieces())
        {
            if (p == null) continue;
            if (p.pieceTeam == PieceTeam.White) white++; else black++;
        }

        if (white == 0 && black == 0) return; // empty board, ignore
        if (white == 0 || black == 0)
        {
            // tentukan tim pemain (anggap White jika ambigu)
            PieceTeam playerTeam = PieceTeam.White;
            if (turnMgr != null)
            {
                playerTeam = turnMgr.GetHumanTeamFallbackWhite();
            }
            PieceTeam winner = (white > 0) ? PieceTeam.White : PieceTeam.Black;
            playerWon = (winner == playerTeam);
            if (playerWon)
            {
                Debug.Log("[GameOver] Player team menang, switch ke kamera win.");
            }
            else
            {
                Debug.Log("[GameOver] Player team kalah, switch ke kamera lose.");
            }
            ShowEnd(playerWon);
        }
    }

    private void ShowEnd(bool win)
    {
        gameOver = true;
        // switch camera
        Camera target = win ? youWinCamera : youLoseCamera;
        if (target != null)
        {
            SwitchToCamera(target);
            // Paksa Camera.main menunjuk ke kamera hasil
            target.gameObject.tag = "MainCamera";
        }
        else
        {
            // fallback: just keep current camera and log
            Debug.Log($"[GameOver] {(win ? "YOU WIN" : "YOU LOSE")}, no end camera assigned.");
        }
    }

    private void SwitchToCamera(Camera target)
    {
        var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in cams)
        {
            if (c == target)
            {
                c.enabled = true;
            }
            else
            {
                c.enabled = false;
                if (c.gameObject.tag == "MainCamera") c.gameObject.tag = "Untagged";
            }
        }
    }

    // Reset state game over agar bisa main ulang
    public void ResetState(bool switchToChessCam = true)
    {
        gameOver = false;
        playerWon = false;

        if (!switchToChessCam) return;

        // Pastikan referensi chessCamera valid, jangan gunakan win/lose camera sebagai fallback
        if (chessCamera == null || chessCamera == youWinCamera || chessCamera == youLoseCamera)
        {
            chessCamera = FindChessCameraFallback();
        }

        if (chessCamera == null)
        {
            Debug.LogWarning("[GameOver] ResetState: Chess camera tidak ditemukan.");
            return;
        }

        var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in cams)
        {
            bool isChess = (c == chessCamera);
            c.enabled = isChess;
            if (isChess)
            {
                c.gameObject.tag = "MainCamera";
            }
            else if (c.gameObject.tag == "MainCamera")
            {
                c.gameObject.tag = "Untagged";
            }
        }
    }

    private Camera FindChessCameraFallback()
    {
        var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        // 1) Cari berdasarkan nama
        foreach (var c in cams)
        {
            string n = c.gameObject.name.ToLowerInvariant();
            if (n.Contains("chess") || n.Contains("board"))
            {
                if (c != youWinCamera && c != youLoseCamera) return c;
            }
        }
        // 2) Ambil kamera pertama yang bukan win/lose
        foreach (var c in cams)
        {
            if (c != youWinCamera && c != youLoseCamera) return c;
        }
        return null;
    }

    void OnGUI()
    {
        if (!showBanner || !gameOver) return;
        var txt = playerWon ? winText : loseText;
        var style = new GUIStyle(GUI.skin.label);
        style.fontSize = 48;
        style.alignment = TextAnchor.UpperCenter;
        style.normal.textColor = playerWon ? Color.green : Color.red;
        var rect = new Rect(0, 20, Screen.width, 80);
        GUI.Label(rect, txt, style);
    }
}
