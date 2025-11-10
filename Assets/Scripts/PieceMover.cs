using UnityEngine;

// NAMA FILE: PieceMover.cs
// PASANG INI DI SEMUA 12 PREFAB BIDAK (White_Pawn, Black_King, dll)

// Enum untuk mendefinisikan tipe bidak
public enum PieceType { King, Queen, Bishop, Knight, Rook, Pawn }
// Enum untuk mendefinisikan tim
public enum PieceTeam { White, Black }

[RequireComponent(typeof(Collider2D))] // Otomatis minta Collider
public class PieceMover : MonoBehaviour
{
    // --- PENGATURAN BIDAK (Atur di Prefab!) ---
    [Header("Identitas Bidak")]
    public PieceType pieceType;
    public PieceTeam pieceTeam;
    // -----------------------------------------

    private BoardLogic boardLogic;
    private Vector2Int originalGridPos; // Posisi awal saat di-klik
    private Vector3 mouseOffset;
    private SpriteRenderer sRenderer;
    private int defaultSortOrder = 0;
    private TurnManager turnManager;
    private BattleManager battleManager;

    // Variabel untuk menyimpan posisi kita di grid
    public Vector2Int currentGridPos;

    void Start()
    {
        // --- INI ADALAH PERBAIKANNYA ---
        // Menggunakan FindFirstObjectByType (lebih baru)
        // sebagai ganti FindObjectOfType (usang)
        boardLogic = FindFirstObjectByType<BoardLogic>();
        // -------------------------------
        
        sRenderer = GetComponent<SpriteRenderer>();
        if (sRenderer != null)
        {
            defaultSortOrder = sRenderer.sortingOrder;
        }

        // Daftarkan diri ke BoardLogic saat pertama kali muncul
        if (boardLogic != null)
        {
            // Ambil posisi awal dari spawner
            currentGridPos = boardLogic.GetGridPositionFromWorld(transform.position);
            boardLogic.RegisterPiece(this, currentGridPos.x, currentGridPos.y);
        }
        else
        {
            Debug.LogError("PieceMover tidak bisa menemukan BoardLogic di scene!");
        }

        // Cari TurnManager
        turnManager = FindFirstObjectByType<TurnManager>();
        battleManager = FindFirstObjectByType<BattleManager>();
    }

    void OnMouseDown()
    {
        if (boardLogic == null) return;

        // Batasi hanya bisa digerakkan saat gilirannya dan hanya untuk pemain manusia (anggap White = manusia)
        if (turnManager != null)
        {
            if (!turnManager.IsTurnFor(pieceTeam) || !turnManager.IsHuman(pieceTeam) || turnManager.IsBusy())
                return;
        }
        if (battleManager != null && battleManager.IsBattling) return;

        // Simpan posisi ASAL kita
        originalGridPos = boardLogic.GetGridPositionFromWorld(transform.position);

        // Sisanya sama (untuk visual drag)
        mouseOffset = transform.position - GetMouseWorldPos();
        if (sRenderer != null) sRenderer.sortingOrder = 10;
    }

    void OnMouseDrag()
    {
        if (boardLogic == null) return;
        // Bidak mengikuti mouse
        transform.position = GetMouseWorldPos() + mouseOffset;
    }

    void OnMouseUp()
    {
        if (boardLogic == null) return;

        // 1. Dapatkan grid tujuan
        Vector2Int targetGridPos = boardLogic.GetGridPositionFromWorld(transform.position);
        var targetPiece = boardLogic.GetPieceAt(targetGridPos);
        bool isCapture = targetPiece != null && targetPiece.pieceTeam != this.pieceTeam;

        // 2. CEK ATURAN!
        if (IsValidMove(targetGridPos))
        {
            if (!isCapture)
            {
                // GERAKAN VALID TANPA CAPTURE -> pindah biasa
                Vector2 newWorldPos = boardLogic.GetWorldPositionFromGrid(targetGridPos.x, targetGridPos.y);
                transform.position = newWorldPos;

                boardLogic.UpdatePiecePosition(this, originalGridPos, targetGridPos);
                currentGridPos = targetGridPos; // Simpan posisi baru

                // Beri tahu turn manager bahwa langkah selesai
                if (turnManager != null)
                {
                    turnManager.NotifyPieceMoved();
                }
            }
            else
            {
                // GERAKAN VALID DENGAN CAPTURE -> duel, bukan langsung hapus
                // Kembalikan posisi visual ke asal dulu agar papan tetap konsisten
                Vector2 originalWorldPos = boardLogic.GetWorldPositionFromGrid(originalGridPos.x, originalGridPos.y);
                transform.position = originalWorldPos;

                if (battleManager != null)
                {
                    if (turnManager != null) turnManager.SetBusy(true);
                    battleManager.StartDuel(
                        attacker: this,
                        defender: targetPiece,
                        targetPos: targetGridPos,
                        onFinished: () =>
                        {
                            if (turnManager != null)
                            {
                                turnManager.SetBusy(false);
                                turnManager.NotifyPieceMoved();
                            }
                        }
                    );
            }
            }
        }
        else
        {
            // GERAKAN TIDAK VALID
            // Kembalikan bidak ke posisi ASAL
            Vector2 originalWorldPos = boardLogic.GetWorldPositionFromGrid(originalGridPos.x, originalGridPos.y);
            transform.position = originalWorldPos;
        }
        
        // Kembalikan sorting order
        if (sRenderer != null) sRenderer.sortingOrder = defaultSortOrder;
    }

    /// <summary>
    /// OTAK ATURAN GERAKAN
    /// </summary>
    bool IsValidMove(Vector2Int targetPos)
    {
        if (boardLogic == null) return false;

        // 0) Batas papan dan tidak diam di tempat
        if (targetPos == originalGridPos) return false;
        if (!boardLogic.IsInsideBoard(targetPos)) return false;

        // 1) Tidak boleh capture bidak sendiri
        var targetPiece = boardLogic.GetPieceAt(targetPos);
        if (targetPiece != null && targetPiece.pieceTeam == this.pieceTeam) return false;

        // 2) Aturan per-bidak (menggunakan titik asal 'originalGridPos' saat drag pemain)
        switch (pieceType)
        {
            case PieceType.Pawn:
                return IsValidPawnMoveFrom(originalGridPos, targetPos, targetPiece);
            case PieceType.Rook:
                return IsValidRookMoveFrom(originalGridPos, targetPos);
            case PieceType.Bishop:
                return IsValidBishopMoveFrom(originalGridPos, targetPos);
            case PieceType.Queen:
                return IsValidQueenMoveFrom(originalGridPos, targetPos);
            case PieceType.Knight:
                return IsValidKnightMoveFrom(originalGridPos, targetPos);
            case PieceType.King:
                return IsValidKingMoveFrom(originalGridPos, targetPos);
            default:
                return false;
        }
    }

    // Versi publik untuk bot: validasi dari posisi tertentu
    public bool IsValidMoveFrom(Vector2Int from, Vector2Int targetPos)
    {
        if (boardLogic == null) return false;
        if (from == targetPos) return false;
        if (!boardLogic.IsInsideBoard(targetPos) || !boardLogic.IsInsideBoard(from)) return false;

        var targetPiece = boardLogic.GetPieceAt(targetPos);
        if (targetPiece != null && targetPiece.pieceTeam == this.pieceTeam) return false;

        switch (pieceType)
        {
            case PieceType.Pawn:
                return IsValidPawnMoveFrom(from, targetPos, targetPiece);
            case PieceType.Rook:
                return IsValidRookMoveFrom(from, targetPos);
            case PieceType.Bishop:
                return IsValidBishopMoveFrom(from, targetPos);
            case PieceType.Queen:
                return IsValidQueenMoveFrom(from, targetPos);
            case PieceType.Knight:
                return IsValidKnightMoveFrom(from, targetPos);
            case PieceType.King:
                return IsValidKingMoveFrom(from, targetPos);
            default:
                return false;
        }
    }

    // --- ATURAN PER BIDAK (berbasis 'from') ---

    bool IsValidPawnMoveFrom(Vector2Int from, Vector2Int targetPos, PieceMover pieceAtTarget)
    {
        // Arah maju: White ke +Y, Black ke -Y
        int moveDir = (pieceTeam == PieceTeam.White) ? 1 : -1;
        int dx = targetPos.x - from.x;
        int dy = targetPos.y - from.y;

        // Tentukan baris awal pion serupa catur normal namun adaptif ukuran papan:
        // - White: baris 1 jika ada (bukan 0) agar bisa double move dari rank kedua.
        // - Black: baris rows-2 jika ada (bukan rows-1).
        int startRow = (pieceTeam == PieceTeam.White)
            ? Mathf.Min(1, boardLogic.rows - 1) // jika rows==1 akan jadi 0 otomatis
            : Mathf.Max(boardLogic.rows - 2, 0);

        // 1) Maju 1: kotak depan harus kosong
        if (dx == 0 && dy == moveDir)
        {
            if (boardLogic.GetPieceAt(targetPos) == null) return true;
            return false;
        }

        // 2) Maju 2 dari baris awal: jalur dan target harus kosong
        if (dx == 0 && dy == 2 * moveDir && from.y == startRow)
        {
            Vector2Int mid = new Vector2Int(from.x, from.y + moveDir);
            if (boardLogic.GetPieceAt(mid) != null) return false;
            if (boardLogic.GetPieceAt(targetPos) != null) return false;
            return true;
        }

        // 3) Capture diagonal 1
        if (Mathf.Abs(dx) == 1 && dy == moveDir)
        {
            return (pieceAtTarget != null && pieceAtTarget.pieceTeam != this.pieceTeam);
        }

        // En passant tidak diimplementasi
        return false;
    }

    bool IsValidRookMoveFrom(Vector2Int from, Vector2Int targetPos)
    {
        if (!(targetPos.x == from.x || targetPos.y == from.y)) return false;
        return IsPathClear(from, targetPos);
    }

    bool IsValidBishopMoveFrom(Vector2Int from, Vector2Int targetPos)
    {
        int dx = Mathf.Abs(targetPos.x - from.x);
        int dy = Mathf.Abs(targetPos.y - from.y);
        if (dx != dy) return false;
        return IsPathClear(from, targetPos);
    }

    bool IsValidQueenMoveFrom(Vector2Int from, Vector2Int targetPos)
    {
        bool rookLike = (targetPos.x == from.x) || (targetPos.y == from.y);
        bool bishopLike = Mathf.Abs(targetPos.x - from.x) == Mathf.Abs(targetPos.y - from.y);
        if (!(rookLike || bishopLike)) return false;
        return IsPathClear(from, targetPos);
    }

    bool IsValidKnightMoveFrom(Vector2Int from, Vector2Int targetPos)
    {
        int dx = Mathf.Abs(targetPos.x - from.x);
        int dy = Mathf.Abs(targetPos.y - from.y);
        return (dx == 2 && dy == 1) || (dx == 1 && dy == 2);
    }

    bool IsValidKingMoveFrom(Vector2Int from, Vector2Int targetPos)
    {
        int dx = Mathf.Abs(targetPos.x - from.x);
        int dy = Mathf.Abs(targetPos.y - from.y);
        if (dx <= 1 && dy <= 1) return true; // tidak cek check / castling di versi ini
        return false;
    }
    
    // --- Helper (Sama seperti sebelumnya) ---
    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }

    // Cek jalur kosong dari 'from' ke 'to' (tidak termasuk 'to')
    private bool IsPathClear(Vector2Int from, Vector2Int to)
    {
        int stepX = Mathf.Clamp(to.x - from.x, -1, 1);
        int stepY = Mathf.Clamp(to.y - from.y, -1, 1);
        Vector2Int p = new Vector2Int(from.x + stepX, from.y + stepY);
        while (p != to)
        {
            var blocker = boardLogic.GetPieceAt(p);
            if (blocker != null) return false; // ada penghalang di jalur
            p.x += stepX;
            p.y += stepY;
        }
        return true;
    }

    // Enumerasi semua langkah legal dari posisi saat ini (untuk bot / hint)
    public System.Collections.Generic.List<Vector2Int> GetLegalMoves()
    {
        var list = new System.Collections.Generic.List<Vector2Int>();
        for (int x = 0; x < boardLogic.columns; x++)
        {
            for (int y = 0; y < boardLogic.rows; y++)
            {
                var to = new Vector2Int(x, y);
                if (IsValidMoveFrom(currentGridPos, to)) list.Add(to);
            }
        }
        return list;
    }
}