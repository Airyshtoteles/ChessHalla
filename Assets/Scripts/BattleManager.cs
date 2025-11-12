using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardLogic boardLogic;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private Camera chessCamera; // kamera papan utama

    [Header("Arena Scene")]
    [Tooltip("Nama scene arena untuk duel (Additive)")]
    [SerializeField] private string arenaSceneName = "ArenaScene";
    [Tooltip("Nama kamera di dalam scene arena (kosongkan untuk ambil kamera pertama yang ditemukan)")]
    [SerializeField] private string arenaCameraName = "";
    private Camera arenaCamera; // di-resolve setelah scene arena diload
    private string chessPrevTag;
    private string arenaPrevTag;

    [Header("Fallback Simulation (jika arena belum siap)")]
    [Tooltip("Delay sebelum hasil duel jika pakai simulasi fallback")] [SerializeField] private float duelDelay = 1.0f;
    [Tooltip("Tampilkan log debug duel")] [SerializeField] private bool debugLog = true;
    [Tooltip("Langsung kembali ke chess camera saat KO (tidak menunggu jeda)")] [SerializeField] private bool instantReturnOnKO = false;
    [Tooltip("Tetap di arena selama N detik setelah KO sebelum kembali ke papan (aktif jika InstantReturnOnKO=false)")]
    [SerializeField] private float postKOStaySeconds = 5f;

    public bool IsBattling { get; private set; }
    public bool InstantReturnEnabled => instantReturnOnKO;
    public float PostKOStaySeconds => postKOStaySeconds;

    // Bobot sederhana per tipe (boleh di-tweak)
    private readonly Dictionary<PieceType, int> power = new Dictionary<PieceType, int>
    {
        { PieceType.King,  10 },
        { PieceType.Queen, 9  },
        { PieceType.Rook,  5  },
        { PieceType.Bishop,3  },
        { PieceType.Knight,3  },
        { PieceType.Pawn,  1  },
    };

    // State duel
    private PieceMover attackerRef;
    private PieceMover defenderRef;
    private Vector2Int targetPosRef;
    private System.Action onFinishedRef;
    private bool? duelOutcome; // null sampai Arena melaporkan hasil

    void Awake()
    {
        if (boardLogic == null) boardLogic = FindFirstObjectByType<BoardLogic>();
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
        if (chessCamera == null) chessCamera = ResolveChessCamera();
    }

    /// <summary>
    /// Mulai duel antara attacker dan defender (pada targetPos). Akan load scene arena secara additive,
    /// menyerahkan kontrol ke ArenaController, lalu menunggu hasil via ReportDuelResult().
    /// </summary>
    public void StartDuel(PieceMover attacker, PieceMover defender, Vector2Int targetPos, System.Action onFinished)
    {
        if (IsBattling) return;
        if (attacker == null || defender == null) return;

        attackerRef = attacker;
        defenderRef = defender;
        targetPosRef = targetPos;
        onFinishedRef = onFinished;
        duelOutcome = null;

        StartCoroutine(DuelSceneFlow());
    }

    private IEnumerator DuelSceneFlow()
    {
        IsBattling = true;
        if (turnManager != null) turnManager.SetBusy(true);
        if (debugLog)
            Debug.Log($"[Duel] {attackerRef.pieceTeam} {attackerRef.pieceType} vs {defenderRef.pieceTeam} {defenderRef.pieceType} -> load arena '{arenaSceneName}'");

        AsyncOperation loadOp = null;
        bool arenaAlreadyLoaded = false;
        try
        {
            var scn = SceneManager.GetSceneByName(arenaSceneName);
            if (scn.IsValid() && scn.isLoaded)
            {
                // Arena sudah ada di Hierarchy (misalnya saat testing di Editor)
                arenaAlreadyLoaded = true;
                loadOp = null;
            }
            else
            {
                loadOp = SceneManager.LoadSceneAsync(arenaSceneName, LoadSceneMode.Additive);
            }
        }
        catch
        {
            loadOp = null;
        }

        if (arenaAlreadyLoaded || loadOp != null)
        {
            if (loadOp != null)
            {
                while (!loadOp.isDone) yield return null;
                yield return null; // beri waktu Awake/Start di arena
            }

            // Cari ArenaController di scene arena
            var arenaController = FindFirstObjectByType<ArenaController>(FindObjectsInactive.Include);
            if (arenaController != null)
            {
                // Temukan kamera arena dan aktifkan tampilan arena
                arenaCamera = FindArenaCamera();
                SwitchToArenaCamera(true);
                arenaController.Setup(this, attackerRef, defenderRef);
            }
            else
            {
                if (debugLog) Debug.LogWarning("[Duel] ArenaController tidak ditemukan. Gunakan fallback simulasi.");
                // Fallback: simulasi
                yield return new WaitForSeconds(duelDelay);
                bool fallbackWin = RollWinner(attackerRef.pieceType, defenderRef.pieceType);
                duelOutcome = fallbackWin;
            }
        }
        else
        {
            if (debugLog) Debug.LogWarning("[Duel] Gagal load scene arena. Gunakan fallback simulasi.");
            yield return new WaitForSeconds(duelDelay);
            bool fallbackWin = RollWinner(attackerRef.pieceType, defenderRef.pieceType);
            duelOutcome = fallbackWin;
        }

        // Tunggu sampai arena melaporkan duelOutcome, dengan timeout untuk berjaga-jaga
        float maxWait = 30f; // detik
        float elapsed = 0f;
        while (!duelOutcome.HasValue && elapsed < maxWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (!duelOutcome.HasValue)
        {
            // Timeout -> fallback roll winner acak
            if (debugLog) Debug.LogWarning("[Duel] Timeout menunggu hasil duel dari arena. Gunakan fallback acak.");
            duelOutcome = RollWinner(attackerRef.pieceType, defenderRef.pieceType);
        }
        bool attackerWins = duelOutcome.Value;

        // Jika tidak instant return, tampilkan KO di arena selama beberapa detik sebelum kembali
        if (!instantReturnOnKO && postKOStaySeconds > 0f)
        {
            yield return new WaitForSeconds(postKOStaySeconds);
        }

        // Terapkan hasil ke papan
        ApplyDuelOutcome(attackerWins);

    // Setelah duel, cek kondisi game over
    if (GameOverManager.Instance != null)
        GameOverManager.Instance.CheckGameOver();

        // Unload arena jika kita yang meload-nya (bukan jika sudah ada sebelumnya)
        if (loadOp != null)
        {
            AsyncOperation unloadOp = null;
            try
            {
                unloadOp = SceneManager.UnloadSceneAsync(arenaSceneName);
            }
            catch { unloadOp = null; }
            if (unloadOp != null)
            {
                while (!unloadOp.isDone) yield return null;
            }
        }

        // Kembalikan kamera ke papan HANYA jika belum game over (jika game over, GameOverManager sudah memilih kamera akhir)
        if (GameOverManager.Instance == null || !GameOverManager.Instance.IsGameOver())
        {
            SwitchToArenaCamera(false);
        }
        arenaCamera = null;

        IsBattling = false;
        if (turnManager != null) turnManager.SetBusy(false);
        onFinishedRef?.Invoke();
    }

    // Dipanggil dari ArenaController ketika duel selesai
    public void ReportDuelResult(bool attackerWins)
    {
        if (duelOutcome.HasValue)
        {
            if (debugLog) Debug.LogWarning($"[Duel] ReportDuelResult dipanggil lagi (ignored). Existing={duelOutcome.Value}, Incoming={attackerWins}");
            return;
        }
        duelOutcome = attackerWins;
        if (instantReturnOnKO)
        {
            // Kamera balik dulu supaya pemain melihat hasil langsung di papan, kecuali sudah game over
            if (GameOverManager.Instance == null || !GameOverManager.Instance.IsGameOver())
            {
                SwitchToArenaCamera(false);
            }
        }
    }

    private void ApplyDuelOutcome(bool attackerWins)
    {
        if (debugLog) Debug.Log($"[Duel] ApplyDuelOutcome attackerWins={attackerWins} attacker={(attackerRef? attackerRef.name: "<null>")} defender={(defenderRef? defenderRef.name: "<null>")} targetPos={targetPosRef}");
        if (attackerWins)
        {
            // Defender kalah -> hapus defender; attacker pindah ke target
            if (defenderRef != null)
            {
                // Pastikan grid target dikosongkan (gunakan targetPosRef agar pasti benar) lalu hapus defender
                boardLogic.RemovePieceAt(targetPosRef);
                Destroy(defenderRef.gameObject);
            }
            // Pindahkan attacker ke kotak target dan tandai sebagai pemilik kotak
            var from = attackerRef.currentGridPos;
            attackerRef.currentGridPos = targetPosRef;
            attackerRef.transform.position = boardLogic.GetWorldPositionFromGrid(targetPosRef.x, targetPosRef.y);
            boardLogic.UpdatePiecePosition(attackerRef, from, targetPosRef);
            if (debugLog) Debug.Log($"[Duel] Attacker MENANG. Pindah {attackerRef.name} dari {from} ke {targetPosRef}.");
        }
        else
        {
            // Attacker kalah
            if (attackerRef != null)
            {
                boardLogic.RemovePieceAt(attackerRef.currentGridPos);
                Destroy(attackerRef.gameObject);
                if (debugLog) Debug.Log($"[Duel] Attacker KALAH. Hancurkan {attackerRef.name}.");
            }
            // Pastikan defender tetap dianggap menguasai kotak target
            if (defenderRef != null)
            {
                // Sinkronkan posisi dan grid (jaga-jaga jika grid terganggu oleh proses duel)
                defenderRef.currentGridPos = targetPosRef;
                defenderRef.transform.position = boardLogic.GetWorldPositionFromGrid(targetPosRef.x, targetPosRef.y);
                var occupant = boardLogic.GetPieceAt(targetPosRef);
                if (occupant != defenderRef)
                {
                    // Re-assert occupancy tanpa memindah: gunakan RegisterPiece agar slot target berisi defender
                    boardLogic.RegisterPiece(defenderRef, targetPosRef.x, targetPosRef.y);
                    if (debugLog) Debug.Log($"[Duel] Defender re-registered at {targetPosRef}.");
                }
                if (debugLog) Debug.Log($"[Duel] Defender MENANG. Tetapkan {defenderRef.name} di {targetPosRef}.");
            }
        }

        // Safety validation: ensure the correct piece owns target tile after applying outcome
        ValidateAndFixTargetOwnership(attackerWins);
    }

    private void ValidateAndFixTargetOwnership(bool attackerWins)
    {
        if (boardLogic == null) return;
        var occ = boardLogic.GetPieceAt(targetPosRef);
        if (attackerWins)
        {
            if (occ != attackerRef)
            {
                if (debugLog)
                {
                    Debug.LogWarning($"[Duel][Fixup] Expected attacker at {targetPosRef} but found {(occ? occ.name: "<empty>")}. Forcing attacker to occupy.");
                }
                // Clear target, then register attacker there
                boardLogic.RemovePieceAt(targetPosRef);
                if (attackerRef != null)
                {
                    attackerRef.currentGridPos = targetPosRef;
                    attackerRef.transform.position = boardLogic.GetWorldPositionFromGrid(targetPosRef.x, targetPosRef.y);
                    boardLogic.RegisterPiece(attackerRef, targetPosRef.x, targetPosRef.y);
                }
                // Ensure defender is not still referenced
                if (defenderRef != null)
                {
                    var defPos = defenderRef.currentGridPos;
                    var maybeDef = boardLogic.GetPieceAt(defPos);
                    if (maybeDef == defenderRef)
                    {
                        // If defender mistakenly still on board at any position, remove it
                        boardLogic.RemovePieceAt(defPos);
                    }
                }
            }
        }
        else
        {
            if (occ != defenderRef)
            {
                if (debugLog)
                {
                    Debug.LogWarning($"[Duel][Fixup] Expected defender at {targetPosRef} but found {(occ? occ.name: "<empty>")}. Forcing defender to occupy.");
                }
                // Clear target, then register defender there
                boardLogic.RemovePieceAt(targetPosRef);
                if (defenderRef != null)
                {
                    defenderRef.currentGridPos = targetPosRef;
                    defenderRef.transform.position = boardLogic.GetWorldPositionFromGrid(targetPosRef.x, targetPosRef.y);
                    boardLogic.RegisterPiece(defenderRef, targetPosRef.x, targetPosRef.y);
                }
                // Ensure attacker is removed from board
                if (attackerRef != null)
                {
                    var atkPos = attackerRef.currentGridPos;
                    var maybeAtk = boardLogic.GetPieceAt(atkPos);
                    if (maybeAtk == attackerRef)
                    {
                        boardLogic.RemovePieceAt(atkPos);
                    }
                }
            }
        }
    }

    private bool RollWinner(PieceType atk, PieceType def)
    {
        int a = power.TryGetValue(atk, out var ap) ? ap : 1;
        int d = power.TryGetValue(def, out var dp) ? dp : 1;
        float total = a + d;
        float r = Random.value;
        return r < (a / total);
    }

    // === Camera helpers ===
    private Camera ResolveChessCamera()
    {
        // Ambil kamera pertama yang TIDAK berasal dari scene arena
        var all = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var arenaScn = SceneManager.GetSceneByName(arenaSceneName);
        foreach (var c in all)
        {
            if (!arenaScn.IsValid() || c.gameObject.scene != arenaScn)
            {
                return c; // kamera non-arena pertama
            }
        }
        // fallback: jika tidak ketemu, kembalikan Camera.main
        return Camera.main;
    }

    private Camera FindArenaCamera()
    {
        var scn = SceneManager.GetSceneByName(arenaSceneName);
        if (!scn.IsValid() || !scn.isLoaded) return null;
        var roots = scn.GetRootGameObjects();
        Camera first = null;
        foreach (var go in roots)
        {
            var cams = go.GetComponentsInChildren<Camera>(true);
            foreach (var c in cams)
            {
                if (first == null) first = c;
                if (!string.IsNullOrEmpty(arenaCameraName) && c.name == arenaCameraName)
                {
                    return c;
                }
            }
        }
        return first;
    }

    private void SwitchToArenaCamera(bool toArena)
    {
        if (chessCamera == null)
        {
            // resolve ulang untuk berjaga-jaga (misal karena reference null saat play di Editor)
            chessCamera = ResolveChessCamera();
        }
        if (toArena)
        {
            if (chessCamera != null) chessCamera.enabled = false;
            if (arenaCamera != null) arenaCamera.enabled = true;
            // Pastikan Camera.main menunjuk ke kamera arena saat duel
            if (chessCamera != null)
            {
                chessPrevTag = chessCamera.gameObject.tag;
                chessCamera.gameObject.tag = "Untagged";
            }
            if (arenaCamera != null)
            {
                arenaPrevTag = arenaCamera.gameObject.tag;
                arenaCamera.gameObject.tag = "MainCamera";
            }
            if (debugLog) Debug.Log("[Camera] Switch TO arena camera: " + (arenaCamera != null ? arenaCamera.name : "<null>") + ", chessCamera disabled: " + (chessCamera != null ? chessCamera.name : "<null>"));
        }
        else
        {
            if (arenaCamera != null) arenaCamera.enabled = false;
            if (chessCamera != null) chessCamera.enabled = true;
            // Kembalikan MainCamera ke kamera papan
            if (arenaCamera != null)
            {
                // kembalikan tag asal, atau untagged jika kosong
                arenaCamera.gameObject.tag = string.IsNullOrEmpty(arenaPrevTag) ? "Untagged" : arenaPrevTag;
            }
            if (chessCamera != null)
            {
                chessCamera.gameObject.tag = "MainCamera"; // paksa jadi MainCamera agar Camera.main benar
            }
            if (debugLog) Debug.Log("[Camera] Switch BACK to chess camera: " + (chessCamera != null ? chessCamera.name : "<null>") + ", arenaCamera disabled.");
        }
    }
}