using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardLogic boardLogic;
    [SerializeField] private TurnManager turnManager;

    [Header("Arena Scene")]
    [Tooltip("Nama scene arena untuk duel (Additive)")]
    [SerializeField] private string arenaSceneName = "ArenaScene";

    [Header("Fallback Simulation (jika arena belum siap)")]
    [Tooltip("Delay sebelum hasil duel jika pakai simulasi fallback")] [SerializeField] private float duelDelay = 1.0f;
    [Tooltip("Tampilkan log debug duel")] [SerializeField] private bool debugLog = true;

    public bool IsBattling { get; private set; }

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
        try
        {
            loadOp = SceneManager.LoadSceneAsync(arenaSceneName, LoadSceneMode.Additive);
        }
        catch
        {
            loadOp = null;
        }

        if (loadOp != null)
        {
            while (!loadOp.isDone) yield return null;
            yield return null; // beri waktu Awake/Start di arena

            // Cari ArenaController di scene arena
            var arenaController = FindFirstObjectByType<ArenaController>(FindObjectsInactive.Include);
            if (arenaController != null)
            {
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

        // Tunggu sampai arena melaporkan duelOutcome
        yield return new WaitUntil(() => duelOutcome.HasValue);
        bool attackerWins = duelOutcome.Value;

        // Terapkan hasil ke papan
        ApplyDuelOutcome(attackerWins);

        // Unload arena bila berhasil di-load
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

        IsBattling = false;
        if (turnManager != null) turnManager.SetBusy(false);
        onFinishedRef?.Invoke();
    }

    // Dipanggil dari ArenaController ketika duel selesai
    public void ReportDuelResult(bool attackerWins)
    {
        duelOutcome = attackerWins;
    }

    private void ApplyDuelOutcome(bool attackerWins)
    {
        if (attackerWins)
        {
            // Defender kalah -> hapus defender; attacker pindah ke target
            if (defenderRef != null)
            {
                boardLogic.RemovePieceAt(defenderRef.currentGridPos);
                Destroy(defenderRef.gameObject);
            }
            var from = attackerRef.currentGridPos;
            attackerRef.currentGridPos = targetPosRef;
            attackerRef.transform.position = boardLogic.GetWorldPositionFromGrid(targetPosRef.x, targetPosRef.y);
            boardLogic.UpdatePiecePosition(attackerRef, from, targetPosRef);
        }
        else
        {
            // Attacker kalah
            if (attackerRef != null)
            {
                boardLogic.RemovePieceAt(attackerRef.currentGridPos);
                Destroy(attackerRef.gameObject);
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
}