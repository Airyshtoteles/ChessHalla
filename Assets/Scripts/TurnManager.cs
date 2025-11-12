using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [Header("Turn State")]
    [SerializeField] private PieceTeam currentTurn = PieceTeam.White;
    [SerializeField] private bool whiteIsHuman = true;
    [SerializeField] private bool blackIsHuman = false; // jika false -> bot

    [Header("Bot Settings")]
    [SerializeField] private float botMoveDelay = 0.4f;
    [SerializeField] private bool enableBot = true;

    private bool busy = false; // lock saat duel atau animasi
    private bool botThinking = false;
    private BoardLogic boardLogic;
    private BattleManager battleManager;

    void Awake()
    {
        boardLogic = FindFirstObjectByType<BoardLogic>();
        battleManager = FindFirstObjectByType<BattleManager>();
    }

    void Start()
    {
        TryStartBotTurn();
    }

    public bool IsTurnFor(PieceTeam team) => currentTurn == team;
    public bool IsHuman(PieceTeam team) => (team == PieceTeam.White) ? whiteIsHuman : blackIsHuman;
    public bool IsBusy() => busy;
    public void SetBusy(bool v) { busy = v; }

    // Dipakai GameOverManager untuk menentukan tim pemain manusia (fallback ke White jika dua-duanya bot)
    public PieceTeam GetHumanTeamFallbackWhite()
    {
        if (whiteIsHuman && !blackIsHuman) return PieceTeam.White;
        if (blackIsHuman && !whiteIsHuman) return PieceTeam.Black;
        // Jika dua-duanya manusia atau dua-duanya bot, anggap White sebagai pemain utama
        return PieceTeam.White;
    }

    public void NotifyPieceMoved()
    {
        if (busy) return;
        currentTurn = (currentTurn == PieceTeam.White) ? PieceTeam.Black : PieceTeam.White;
        // Cek kondisi game over setiap kali selesai langkah
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.CheckGameOver();
        TryStartBotTurn();
    }

    private void TryStartBotTurn()
    {
        if (!enableBot) return;
        if (currentTurn == PieceTeam.Black && !IsHuman(PieceTeam.Black) && !botThinking && !busy)
        {
            StartCoroutine(BotRoutine());
        }
    }

    private IEnumerator BotRoutine()
    {
        botThinking = true;
        yield return new WaitForSeconds(botMoveDelay);
        if (boardLogic == null)
        {
            botThinking = false; yield break;
        }
        // Kumpulkan semua gerakan legal
        var moves = new List<(PieceMover piece, Vector2Int from, Vector2Int to, bool capture)>();
        foreach (var p in boardLogic.GetAllPieces())
        {
            if (p == null || p.pieceTeam != PieceTeam.Black) continue;
            var legal = p.GetLegalMoves();
            foreach (var to in legal)
            {
                var target = boardLogic.GetPieceAt(to);
                bool cap = target != null && target.pieceTeam != p.pieceTeam;
                moves.Add((p, p.currentGridPos, to, cap));
            }
        }
        if (moves.Count == 0)
        {
            currentTurn = PieceTeam.White;
            botThinking = false;
            yield break;
        }
        // Prioritaskan capture
        var captureList = moves.FindAll(m => m.capture);
        var choice = (captureList.Count > 0)
            ? captureList[Random.Range(0, captureList.Count)]
            : moves[Random.Range(0, moves.Count)];

        // Jika capture -> duel, bukan langsung hapus
        if (choice.capture && battleManager != null)
        {
            busy = true;
            battleManager.StartDuel(
                choice.piece,
                boardLogic.GetPieceAt(choice.to),
                choice.to,
                () => {
                    busy = false;
                    botThinking = false;
                    currentTurn = PieceTeam.White; // setelah langkah bot
                    if (GameOverManager.Instance != null) GameOverManager.Instance.CheckGameOver();
                    TryStartBotTurn(); // mungkin lanjut kalau multi-bot scenario
                }
            );
        }
        else
        {
            // Langkah normal
            choice.piece.transform.position = boardLogic.GetWorldPositionFromGrid(choice.to.x, choice.to.y);
            boardLogic.UpdatePiecePosition(choice.piece, choice.from, choice.to);
            choice.piece.currentGridPos = choice.to;
            botThinking = false;
            currentTurn = PieceTeam.White;
            if (GameOverManager.Instance != null) GameOverManager.Instance.CheckGameOver();
            TryStartBotTurn();
        }
    }
}
