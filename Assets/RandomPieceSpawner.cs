using UnityEngine;
using System.Collections.Generic;

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

    /// <summary>
    /// Fungsi helper untuk men-spawn satu tim
    /// </summary>
    void SpawnTeam(GameObject[] teamPrefabList, string teamName, int minY, int maxY)
    {
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

            // 1. Pilih LOKASI Acak
            int randomX = Random.Range(0, boardLogic.columns);
            int randomY = Random.Range(minY, maxY); 
            
            Vector2Int gridPos = new Vector2Int(randomX, randomY);

            // 2. Cek apakah lokasi sudah terisi
            if (occupiedTiles.Contains(gridPos))
            {
                continue; 
            }

            // 3. Jika lokasi kosong:
            occupiedTiles.Add(gridPos); 

            // Logika "lotre"
            int randomPrefabIndex = Random.Range(0, teamPrefabList.Length);
            GameObject pieceToSpawn = teamPrefabList[randomPrefabIndex];

            // --- INI BARIS YANG DIPERBAIKI ---
            // Saya tambahkan "From" di nama fungsinya
            Vector2 spawnPos = boardLogic.GetWorldPositionFromGrid(randomX, randomY);
            // ---------------------------------

            // Spawn bidak
            var go = Instantiate(pieceToSpawn, new Vector3(spawnPos.x, spawnPos.y, 0f), Quaternion.identity, transform);

            // Pastikan semua SpriteRenderer bidak berada di atas board (sorting order lebih tinggi)
            TryRaiseSorting(go);

            spawnedCount++;
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