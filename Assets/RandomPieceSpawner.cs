using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// NAMA FILE: RandomPieceSpawner.cs
// FUNGSI: Spawn 2 tim di wilayah masing-masing (3 baris bawah vs 3 baris atas)
// VERSI 3.1: Perbaikan typo GetWorldPositionFromGrid

public class RandomPieceSpawner : MonoBehaviour
{
    [Header("Referensi Wajib")]
    public BoardLogic boardLogic; 

    [Header("Pengaturan Spawner")]
    public int spawnCountPerTeam = 5;

    [Header("Daftar Prefab Tim")]
    public GameObject[] whitePiecePrefabs;
    public GameObject[] blackPiecePrefabs;

    [Header("Aturan Probabilitas (semakin tinggi weight, semakin sering muncul)")]
    [Tooltip("Jika kosong akan dibuat default otomatis. weight = bobot pemilihan; maxPerTeam = batas jumlah per tim.")]
    public List<PieceSpawnRule> pieceSpawnRules = new List<PieceSpawnRule>();

    [System.Serializable]
    public class PieceSpawnRule
    {
        public PieceType type;
        [Min(0)] public int weight = 1;        // Bobot relatif
        [Min(0)] public int maxPerTeam = 1;    // Batas jumlah spawn per tim
    }

    [Header("Opsi Khusus")]
    [Tooltip("Pastikan tiap tim punya King minimal 1; jika belum ada, akan mengganti Pawn menjadi King")]
    public bool ensureOneKingPerTeam = true;

    // Set ini melacak SEMUA kotak yang sudah terisi
    private HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>();
    // Cache layer & urutan sorting terdepan di scene agar bidak tidak ketutup board
    private int piecesSortingOrder = 10; // fallback jika tidak bisa deteksi
    private int piecesSortingLayerId = 0; // Default layer id

    [Header("Trigger")]
    [Tooltip("Jika true, spawn otomatis saat Start. Biarkan false bila ingin spawn via tombol Start Game.")]
    public bool spawnOnStart = false;
    private bool hasSpawned = false;

    void Start()
    {
        if (spawnOnStart)
        {
            TriggerSpawn();
        }
    }

    // Panggil dari menu Start Game atau dari script lain setelah scene permainan dimuat
    public void TriggerSpawn()
    {
        if (hasSpawned)
        {
            Debug.Log("[RandomPieceSpawner] Sudah pernah spawn, abaikan.");
            return;
        }

        if (boardLogic == null)
        {
            boardLogic = FindFirstObjectByType<BoardLogic>();
        }
        if (boardLogic == null)
        {
            Debug.LogError("REFERENSI 'Board Logic' DI SPAWNER MASIH KOSONG!");
            return;
        }
        if (whitePiecePrefabs == null || whitePiecePrefabs.Length == 0 || blackPiecePrefabs == null || blackPiecePrefabs.Length == 0)
        {
            Debug.LogError("Daftar 'Piece Prefabs' (White/Black) masih kosong!");
            return;
        }

        // Deteksi sorting layer/value & order tertinggi di scene, supaya bidak dirender di atas board
        DetectTopSortingInfo(out piecesSortingLayerId, out piecesSortingOrder);
        piecesSortingOrder += 1; // taruh 1 tingkat di atas elemen teratas saat ini

        // Validasi kapasitas
        int maxTiles = boardLogic.columns * boardLogic.rows;
        int totalToSpawn = spawnCountPerTeam * 2;
        int maxSpawnPerArea = (maxTiles / 2);
        if (totalToSpawn > maxTiles)
        {
            Debug.LogWarning("Jumlah total bidak > jumlah kotak. Mengurangi spawn count.");
            spawnCountPerTeam = maxTiles / 2;
        }
        else if (spawnCountPerTeam > maxSpawnPerArea)
        {
            Debug.LogWarning("Spawn count > kotak per area! Mengurangi jadi " + maxSpawnPerArea);
            spawnCountPerTeam = maxSpawnPerArea;
        }

        // Reset occupancy agar spawn ulang bersih (jaga-jaga)
        occupiedTiles.Clear();

        int totalRows = boardLogic.rows;
        int midRow = totalRows / 2;
        SpawnTeam(whitePiecePrefabs, "Putih", 0, midRow);
        SpawnTeam(blackPiecePrefabs, "Hitam", midRow, totalRows);

        hasSpawned = true;
    }

    // Izinkan Start Game ulang: reset flag internal agar bisa spawn lagi
    public void ResetSpawnFlag()
    {
        hasSpawned = false;
        occupiedTiles.Clear();
    }

    /// <summary>
    /// Fungsi helper untuk men-spawn satu tim
    /// </summary>
    void SpawnTeam(GameObject[] teamPrefabList, string teamName, int minY, int maxY)
    {
        // Siapkan aturan default bila list kosong
        if (pieceSpawnRules == null || pieceSpawnRules.Count == 0)
        {
            pieceSpawnRules = new List<PieceSpawnRule>
            {
                new PieceSpawnRule{ type = PieceType.King,   weight = 1,  maxPerTeam = 1 },
                new PieceSpawnRule{ type = PieceType.Queen,  weight = 1,  maxPerTeam = 1 },
                new PieceSpawnRule{ type = PieceType.Rook,   weight = 2,  maxPerTeam = 2 },
                new PieceSpawnRule{ type = PieceType.Bishop, weight = 3,  maxPerTeam = 2 },
                new PieceSpawnRule{ type = PieceType.Knight, weight = 3,  maxPerTeam = 2 },
                new PieceSpawnRule{ type = PieceType.Pawn,   weight = 12, maxPerTeam = 99 },
            };
        }

        // Buat lookup prefab per tipe
        Dictionary<PieceType, List<GameObject>> prefabsByType = new Dictionary<PieceType, List<GameObject>>();
        foreach (var go in teamPrefabList)
        {
            if (go == null) continue;
            var mover = go.GetComponent<PieceMover>();
            if (mover == null)
            {
                Debug.LogWarning($"[Spawner] Prefab {go.name} tidak punya PieceMover.");
                continue;
            }
            if (!prefabsByType.ContainsKey(mover.pieceType)) prefabsByType[mover.pieceType] = new List<GameObject>();
            prefabsByType[mover.pieceType].Add(go);
        }

        // Hitung spawn
        Dictionary<PieceType,int> counts = new Dictionary<PieceType,int>();
        List<GameObject> spawnedTeamPieces = new List<GameObject>();
        int spawnedCount = 0;
        int safetyBreak = 0; 

        while (spawnedCount < spawnCountPerTeam)
        {
            safetyBreak++;
            if (safetyBreak > 1000) 
            {
                Debug.LogError($"Tidak bisa menemukan posisi kosong untuk tim {teamName}! Papan penuh?");
                break; 
            }

            // 1. Pilih tipe berdasarkan bobot tersisa
            var availableRules = pieceSpawnRules.Where(r => {
                int current = counts.TryGetValue(r.type, out var c) ? c : 0;
                return current < r.maxPerTeam && prefabsByType.ContainsKey(r.type) && prefabsByType[r.type].Count > 0;
            }).ToList();
            if (availableRules.Count == 0)
            {
                Debug.LogWarning($"[Spawner] Tidak ada tipe lagi yang boleh di-spawn untuk tim {teamName}.");
                break;
            }
            int totalWeight = availableRules.Sum(r => r.weight);
            if (totalWeight <= 0)
            {
                // fallback: treat all equal
                totalWeight = availableRules.Count;
                foreach (var r in availableRules) if (r.weight <= 0) r.weight = 1;
            }
            float pick = Random.value * totalWeight;
            PieceType chosenType = availableRules[0].type;
            int cumulative = 0;
            foreach (var rule in availableRules)
            {
                cumulative += rule.weight;
                if (pick <= cumulative)
                {
                    chosenType = rule.type;
                    break;
                }
            }

            // 2. Pilih LOKASI acak dalam area tim
            int randomX = Random.Range(0, boardLogic.columns);
            int randomY = Random.Range(minY, maxY); 
            
            Vector2Int gridPos = new Vector2Int(randomX, randomY);

            // 3. Cek apakah lokasi sudah terisi
            if (occupiedTiles.Contains(gridPos))
            {
                continue; 
            }

            // 4. Jika lokasi kosong:
            occupiedTiles.Add(gridPos); 

            // 5. Ambil prefab sesuai tipe (random skin jika >1)
            var list = prefabsByType[chosenType];
            GameObject pieceToSpawn = list[Random.Range(0, list.Count)];

            // --- INI BARIS YANG DIPERBAIKI ---
            // Saya tambahkan "From" di nama fungsinya
            Vector2 spawnPos = boardLogic.GetWorldPositionFromGrid(randomX, randomY);
            // ---------------------------------

            // Spawn bidak
            var go = Instantiate(pieceToSpawn, new Vector3(spawnPos.x, spawnPos.y, 0f), Quaternion.identity, transform);

            // Pastikan semua SpriteRenderer bidak berada di atas board (sorting order lebih tinggi)
            TryRaiseSorting(go);

            spawnedCount++;
            counts[chosenType] = counts.TryGetValue(chosenType, out var tmp) ? tmp + 1 : 1;
            spawnedTeamPieces.Add(go);
        }

        // Pastikan ada King minimal 1 jika opsi aktif
        if (ensureOneKingPerTeam)
        {
            bool hasKing = spawnedTeamPieces.Any(p => {
                var m = p.GetComponent<PieceMover>();
                return m != null && m.pieceType == PieceType.King;
            });
            if (!hasKing)
            {
                // Cari pawn untuk diganti
                var pawnGO = spawnedTeamPieces.FirstOrDefault(p => {
                    var m = p.GetComponent<PieceMover>();
                    return m != null && m.pieceType == PieceType.Pawn;
                });
                if (pawnGO != null && prefabsByType.ContainsKey(PieceType.King) && prefabsByType[PieceType.King].Count > 0)
                {
                    // Ambil posisi grid pawn
                    var pawnMover = pawnGO.GetComponent<PieceMover>();
                    Vector2Int pawnGrid = pawnMover != null ? pawnMover.currentGridPos : Vector2Int.zero;
                    Vector3 pawnWorld = pawnGO.transform.position;
                    // Hapus pawn
                    Destroy(pawnGO);
                    spawnedTeamPieces.Remove(pawnGO);
                    // Spawn king di posisi itu
                    var kingPrefab = prefabsByType[PieceType.King][Random.Range(0, prefabsByType[PieceType.King].Count)];
                    var kingGO = Instantiate(kingPrefab, pawnWorld, Quaternion.identity, transform);
                    TryRaiseSorting(kingGO);
                    spawnedTeamPieces.Add(kingGO);
                    Debug.Log($"[Spawner] Tidak ada King untuk tim {teamName}, mengganti satu Pawn menjadi King.");
                }
            }
        }
    }

    // Naikkan sorting order semua SpriteRenderer pada GO agar tidak ketutup board
    private void TryRaiseSorting(GameObject go)
    {
        // Jika ada SortingGroup di root, naikkan juga
        var sg = go.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg != null)
        {
            if (piecesSortingLayerId != 0) sg.sortingLayerID = piecesSortingLayerId;
            sg.sortingOrder = piecesSortingOrder;
        }

        var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in renderers)
        {
            // Pertahankan layer yang sama, hanya naikkan ordernya
            if (piecesSortingLayerId != 0) r.sortingLayerID = piecesSortingLayerId;
            r.sortingOrder = Mathf.Max(r.sortingOrder, piecesSortingOrder);
        }
    }

    // Dapatkan kombinasi layer dan order 'teratas' (berdasar layer value lalu sortingOrder)
    private void DetectTopSortingInfo(out int topLayerId, out int topOrder)
    {
        topLayerId = 0;
        topOrder = 0;

        int bestLayerValue = int.MinValue;
        int bestOrder = int.MinValue;

        var renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r == null) continue;

            int layerValue = SortingLayer.GetLayerValueFromID(r.sortingLayerID);
            int order = r.sortingOrder;

            // Bandingkan dulu layer, lalu order
            if (layerValue > bestLayerValue || (layerValue == bestLayerValue && order > bestOrder))
            {
                bestLayerValue = layerValue;
                bestOrder = order;
                topLayerId = r.sortingLayerID;
                topOrder = order;
            }
        }

        // Fallback jika tidak ada renderer ditemukan
        if (bestLayerValue == int.MinValue)
        {
            topLayerId = SortingLayer.NameToID("Default");
            topOrder = 0;
        }
    }
}