using UnityEngine;
using System.Collections.Generic;

// NAMA FILE: BoardLogic.cs
// PASANG INI DI GameObject "Board"

public class BoardLogic : MonoBehaviour
{
    [Header("Board Settings")]
    public int rows = 6;
    public int columns = 6;
    public float cellSize = 1f;

    // Array 2D untuk menyimpan data bidak di setiap kotak [x, y]
    private PieceMover[,] pieceGrid;

    /// <summary>
    /// Awake dipanggil sebelum Start.
    /// </summary>
    void Awake()
    {
        // Buat array logikanya
        pieceGrid = new PieceMover[columns, rows];
    }
    
    // --- FUNGSI MANAJEMEN BIDAK ---

    /// <summary>
    /// Dipanggil oleh Bidak saat 'Start'
    /// </summary>
    public void RegisterPiece(PieceMover piece, int x, int y)
    {
        if (x < 0 || x >= columns || y < 0 || y >= rows)
        {
            Debug.LogError($"Posisi di luar batas: {x},{y}");
            return;
        }
        pieceGrid[x, y] = piece;
    }

    /// <summary>
    /// Dipanggil oleh Bidak setelah berhasil bergerak.
    /// </summary>
    public void UpdatePiecePosition(PieceMover piece, Vector2Int oldPos, Vector2Int newPos)
    {
        // Capture jika ada bidak musuh di posisi baru
        var existing = GetPieceAt(newPos);
        if (existing != null && existing != piece && existing.pieceTeam != piece.pieceTeam)
        {
            // Hapus GameObject bidak yang dimakan
            if (existing.gameObject != null)
            {
                Destroy(existing.gameObject);
            }
        }

        // Kosongkan posisi lama (jika valid)
        if (IsInsideBoard(oldPos))
            pieceGrid[oldPos.x, oldPos.y] = null;

        // Set posisi baru
        if (IsInsideBoard(newPos))
            pieceGrid[newPos.x, newPos.y] = piece;
    }
    
    // --- FUNGSI KALKULASI POSISI (TIDAK BERUBAH) ---

    public Vector2 GetWorldPositionFromGrid(int x, int y)
    {
        float startX = -(columns / 2f) * cellSize + cellSize / 2f;
        float startY = -(rows / 2f) * cellSize + cellSize / 2f;
        return new Vector2(
            transform.position.x + startX + x * cellSize,
            transform.position.y + startY + y * cellSize
        );
    }
    
    public Vector2Int GetGridPositionFromWorld(Vector2 worldPos)
    {
        float cellSize = this.cellSize;
        int columns = this.columns;
        int rows = this.rows;
        float startX = -(columns / 2f) * cellSize + cellSize / 2f;
        float startY = -(rows / 2f) * cellSize + cellSize / 2f;
        float offsetX = (worldPos.x - transform.position.x) - startX;
        float offsetY = (worldPos.y - transform.position.y) - startY;
        int x = Mathf.RoundToInt(offsetX / cellSize);
        int y = Mathf.RoundToInt(offsetY / cellSize);
        x = Mathf.Clamp(x, 0, columns - 1);
        y = Mathf.Clamp(y, 0, rows - 1);
        return new Vector2Int(x, y);
    }

    // === Helper occupancy / batas papan ===
    public bool IsInsideBoard(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < columns && pos.y >= 0 && pos.y < rows;
    }

    public PieceMover GetPieceAt(Vector2Int pos)
    {
        if (!IsInsideBoard(pos)) return null;
        return pieceGrid[pos.x, pos.y];
    }

    public IEnumerable<PieceMover> GetAllPieces()
    {
        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                var p = pieceGrid[x, y];
                if (p != null) yield return p;
            }
        }
    }

    // Hapus referensi bidak pada grid, tanpa memanggil Destroy (agar bisa diatur pihak pemanggil)
    public void RemovePieceAt(Vector2Int pos)
    {
        if (!IsInsideBoard(pos)) return;
        pieceGrid[pos.x, pos.y] = null;
    }
}