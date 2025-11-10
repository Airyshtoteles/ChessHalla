using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Controller yang hidup di scene arena. Tugasnya:
// - Menerima peserta duel dari BattleManager
// - Memilih prefab fighter berdasarkan tipe & tim bidak
// - Memasang mereka di spawn point
// - Menjalankan pertarungan (sementara: simulasi), lalu melaporkan hasil
public class ArenaController : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform attackerSpawn;
    [SerializeField] private Transform defenderSpawn;

    [Header("Fighter Prefabs - White Team")]
    [SerializeField] private GameObject whiteKing;
    [SerializeField] private GameObject whiteQueen;
    [SerializeField] private GameObject whiteRook;
    [SerializeField] private GameObject whiteBishop;
    [SerializeField] private GameObject whiteKnight;
    [SerializeField] private GameObject whitePawn;

    [Header("Fighter Prefabs - Black Team")]
    [SerializeField] private GameObject blackKing;
    [SerializeField] private GameObject blackQueen;
    [SerializeField] private GameObject blackRook;
    [SerializeField] private GameObject blackBishop;
    [SerializeField] private GameObject blackKnight;
    [SerializeField] private GameObject blackPawn;

    [Header("Simulasi (sementara)")]
    [SerializeField] private float fightDuration = 1.5f;

    private BattleManager battleManager;
    private PieceMover attacker;
    private PieceMover defender;

    private GameObject attackerFighter;
    private GameObject defenderFighter;
    private bool hasStarted = false;
    [SerializeField] private bool simulateIfNoController = true;

    // Dipanggil oleh BattleManager segera setelah scene arena di-load
    public void Setup(BattleManager bm, PieceMover atk, PieceMover def)
    {
        battleManager = bm;
        attacker = atk;
        defender = def;

        // 1) Spawn fighter sesuai mapping
        SpawnFighters();

        // 2) Wiring controller & health
        bool wired = WireControllersAndHealth();

        // 3) Jalankan duel: jika tidak ada controller/health, fallback simulasi
        if (!hasStarted)
        {
            hasStarted = true;
            if (!wired && simulateIfNoController)
                StartCoroutine(SimulateDuel());
        }
    }

    private void SpawnFighters()
    {
        if (attacker != null)
        {
            var atkPrefab = GetFighterPrefab(attacker.pieceTeam, attacker.pieceType);
            if (atkPrefab != null)
            {
                attackerFighter = Instantiate(atkPrefab, attackerSpawn != null ? attackerSpawn.position : Vector3.left * 2f, Quaternion.identity);
                attackerFighter.name = $"Attacker_{attacker.pieceTeam}_{attacker.pieceType}";
            }
            else
            {
                Debug.LogWarning($"[Arena] Prefab attacker untuk {attacker.pieceTeam} {attacker.pieceType} tidak ditemukan.");
            }
        }

        if (defender != null)
        {
            var defPrefab = GetFighterPrefab(defender.pieceTeam, defender.pieceType);
            if (defPrefab != null)
            {
                defenderFighter = Instantiate(defPrefab, defenderSpawn != null ? defenderSpawn.position : Vector3.right * 2f, Quaternion.identity);
                defenderFighter.name = $"Defender_{defender.pieceTeam}_{defender.pieceType}";
                // Balik hadap ke kiri jika perlu (2D side view)
                var sr = defenderFighter.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.flipX = true;
            }
            else
            {
                Debug.LogWarning($"[Arena] Prefab defender untuk {defender.pieceTeam} {defender.pieceType} tidak ditemukan.");
            }
        }
    }

    private GameObject GetFighterPrefab(PieceTeam team, PieceType type)
    {
        if (team == PieceTeam.White)
        {
            switch (type)
            {
                case PieceType.King:   return whiteKing;
                case PieceType.Queen:  return whiteQueen;
                case PieceType.Rook:   return whiteRook;
                case PieceType.Bishop: return whiteBishop;
                case PieceType.Knight: return whiteKnight;
                case PieceType.Pawn:   return whitePawn;
            }
        }
        else // Black
        {
            switch (type)
            {
                case PieceType.King:   return blackKing;
                case PieceType.Queen:  return blackQueen;
                case PieceType.Rook:   return blackRook;
                case PieceType.Bishop: return blackBishop;
                case PieceType.Knight: return blackKnight;
                case PieceType.Pawn:   return blackPawn;
            }
        }
        return null;
    }

    private bool WireControllersAndHealth()
    {
        var atkCtrl = attackerFighter != null ? attackerFighter.GetComponentInChildren<FighterController2D>() : null;
        var defCtrl = defenderFighter != null ? defenderFighter.GetComponentInChildren<FighterController2D>() : null;
        var atkHP  = attackerFighter != null ? attackerFighter.GetComponentInChildren<FighterHealth>() : null;
        var defHP  = defenderFighter != null ? defenderFighter.GetComponentInChildren<FighterHealth>() : null;

        if (atkCtrl != null)
        {
            atkCtrl.Initialize(team: attacker.pieceTeam, isPlayerControlled: true);
        }
        if (defCtrl != null)
        {
            defCtrl.Initialize(team: defender.pieceTeam, isPlayerControlled: false);
            // Simple orientation for defender to face left
            defCtrl.FaceLeft();
        }
        if (atkCtrl != null && defCtrl != null)
        {
            atkCtrl.SetOpponent(defCtrl);
            defCtrl.SetOpponent(atkCtrl);
        }

        bool wired = false;
        if (atkHP != null)
        {
            atkHP.OnDeath += () =>
            {
                // Attacker mati -> attacker kalah
                if (battleManager != null) battleManager.ReportDuelResult(false);
            };
            wired = true;
        }
        if (defHP != null)
        {
            defHP.OnDeath += () =>
            {
                // Defender mati -> attacker menang
                if (battleManager != null) battleManager.ReportDuelResult(true);
            };
            wired = true;
        }
        return wired;
    }

    private IEnumerator SimulateDuel()
    {
        // TODO: ganti dengan logika arena sebenarnya (input, damage, KO)
        yield return new WaitForSeconds(fightDuration);

        // Contoh: pemenang random 50:50 (silakan ganti cara menentukan pemenang)
        bool attackerWins = Random.value < 0.5f;

        if (battleManager != null)
        {
            battleManager.ReportDuelResult(attackerWins);
        }
        else
        {
            Debug.LogWarning("[ArenaController] BattleManager null, tidak bisa melaporkan hasil duel.");
        }
    }
}
