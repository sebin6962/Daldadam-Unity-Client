using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TableSlotSave
{
    public string tableId;
    public bool hasItem;
    public string itemSpriteName;
}

[Serializable]
public class TableSaveData
{
    public List<TableSlotSave> tables = new List<TableSlotSave>();
}

public class TableManager : MonoBehaviour
{
    private readonly HashSet<string> restoredTableIds =
        new HashSet<string>();

    private string loadedServer = "";
    private bool isRestoring;

    private void Start()
    {
        if (!SaveService.HasCurrentSave &&
            !SaveService.LoadSelectedSave())
        {
            Debug.LogWarning(
                "[TableManager] 불러올 저장 슬롯이 없습니다."
            );

            return;
        }

        loadedServer = SaveService.CurrentServer;

        LoadTableState();
        SpawnInitialItemsUnique();

        // 새 게임과 기존 파일 이전 직후에도 빈 테이블을 포함한
        // 현재 씬의 전체 상태를 확정해 둔다.
        SaveTableState();
    }

    private void OnDisable()
    {
        SaveCurrentSceneStateIfSafe();
    }

    private void OnApplicationQuit()
    {
        SaveCurrentSceneStateIfSafe();
    }

    private void SpawnInitialItemsUnique()
    {
        TableInfo[] tablesInScene =
            FindObjectsOfType<TableInfo>();

        HashSet<string> existingSpriteNames =
            new HashSet<string>();

        foreach (TableInfo table in tablesInScene)
        {
            if (table == null ||
                table.currentPlacedObject == null)
            {
                continue;
            }

            SpriteRenderer renderer =
                table.currentPlacedObject.GetComponent<SpriteRenderer>();

            if (renderer != null && renderer.sprite != null)
                existingSpriteNames.Add(renderer.sprite.name);
        }

        foreach (TableInfo table in tablesInScene)
        {
            if (table == null ||
                string.IsNullOrWhiteSpace(table.tableId) ||
                restoredTableIds.Contains(table.tableId) ||
                !table.spawnInitialItemOnStart ||
                string.IsNullOrWhiteSpace(table.initialItemSpriteName) ||
                table.currentPlacedObject != null ||
                existingSpriteNames.Contains(table.initialItemSpriteName))
            {
                continue;
            }

            if (table.TrySpawnInitialItem())
                existingSpriteNames.Add(table.initialItemSpriteName);
        }
    }

    public void SaveTableState()
    {
        TrySaveTableState();
    }

    private bool TrySaveTableState()
    {
        if (isRestoring)
            return true;

        if (!SaveService.HasCurrentSave)
            return false;

        if (!string.IsNullOrEmpty(loadedServer) &&
            !SaveService.IsCurrent(loadedServer))
        {
            Debug.LogWarning(
                "[TableManager] 다른 슬롯으로 전환된 뒤 이전 테이블이 " +
                "저장되는 것을 차단했습니다."
            );

            return false;
        }

        TableInfo[] tablesInScene =
            FindObjectsOfType<TableInfo>();

        if (tablesInScene.Length == 0)
        {
            return false;
        }

        TableSaveData currentData =
            SaveService.CurrentData.tableData ??
            new TableSaveData();

        TableSaveData data = new TableSaveData();
        HashSet<string> savedTableIds = new HashSet<string>();

        foreach (TableInfo table in tablesInScene)
        {
            if (table != null &&
                !string.IsNullOrWhiteSpace(table.tableId))
            {
                savedTableIds.Add(table.tableId);
            }
        }

        if (currentData.tables != null)
        {
            foreach (TableSlotSave savedTable in currentData.tables)
            {
                if (savedTable == null ||
                    string.IsNullOrWhiteSpace(savedTable.tableId) ||
                    savedTableIds.Contains(savedTable.tableId))
                {
                    continue;
                }

                data.tables.Add(savedTable);
            }
        }

        savedTableIds.Clear();

        foreach (TableInfo table in tablesInScene)
        {
            if (table == null ||
                string.IsNullOrWhiteSpace(table.tableId))
            {
                continue;
            }

            if (!savedTableIds.Add(table.tableId))
            {
                Debug.LogError(
                    "[TableManager] 중복 tableId 발견: " +
                    table.tableId
                );

                continue;
            }

            SpriteRenderer renderer = null;

            if (table.currentPlacedObject != null)
            {
                renderer = table.currentPlacedObject
                    .GetComponent<SpriteRenderer>();
            }

            bool hasItem =
                renderer != null && renderer.sprite != null;

            data.tables.Add(new TableSlotSave
            {
                tableId = table.tableId,
                hasItem = hasItem,
                itemSpriteName = hasItem
                    ? renderer.sprite.name
                    : ""
            });
        }

        SaveService.CurrentData.tableData = data;
        SaveService.CurrentData.tableMigrationCompleted = true;

        bool saved = SaveService.SaveCurrent();

        return saved;
    }

    public void LoadTableState()
    {
        if (!SaveService.HasCurrentSave)
            return;

        loadedServer = SaveService.CurrentServer;
        restoredTableIds.Clear();

        TableSaveData data =
            SaveService.CurrentData.tableData ??
            new TableSaveData();

        if (data.tables == null)
            data.tables = new List<TableSlotSave>();

        SaveService.CurrentData.tableData = data;

        TableInfo[] tablesInScene =
            FindObjectsOfType<TableInfo>();

        Dictionary<string, TableInfo> tableMap =
            new Dictionary<string, TableInfo>();

        foreach (TableInfo table in tablesInScene)
        {
            if (table == null ||
                string.IsNullOrWhiteSpace(table.tableId))
            {
                continue;
            }

            if (tableMap.ContainsKey(table.tableId))
            {
                Debug.LogError(
                    "[TableManager] 중복 tableId 발견: " +
                    table.tableId
                );

                continue;
            }

            tableMap.Add(table.tableId, table);
        }

        isRestoring = true;

        try
        {
            foreach (TableSlotSave savedTable in data.tables)
            {
                if (savedTable == null ||
                    string.IsNullOrWhiteSpace(savedTable.tableId) ||
                    !restoredTableIds.Add(savedTable.tableId) ||
                    !tableMap.TryGetValue(
                        savedTable.tableId,
                        out TableInfo table
                    ))
                {
                    continue;
                }

                if (table.currentPlacedObject != null)
                {
                    Destroy(table.currentPlacedObject);
                    table.currentPlacedObject = null;
                }

                if (!savedTable.hasItem ||
                    string.IsNullOrWhiteSpace(savedTable.itemSpriteName))
                {
                    continue;
                }

                Sprite sprite = Resources.Load<Sprite>(
                    table.spriteResourceDir +
                    savedTable.itemSpriteName
                );

                if (sprite == null)
                {
                    Debug.LogWarning(
                        "[TableManager] 스프라이트 로드 실패: " +
                        table.spriteResourceDir +
                        savedTable.itemSpriteName
                    );

                    continue;
                }

                table.CreateTableItem(sprite);
            }
        }
        finally
        {
            isRestoring = false;
        }
    }

    private void SaveCurrentSceneStateIfSafe()
    {
        if (string.IsNullOrEmpty(loadedServer) ||
            !SaveService.IsCurrent(loadedServer))
        {
            return;
        }

        SaveTableState();
    }
}

public static class TableInitialItemHelper
{
    public static bool IsInitialTableItemName(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return false;

        TableInfo[] tables =
            GameObject.FindObjectsOfType<TableInfo>();

        foreach (TableInfo table in tables)
        {
            if (!table.spawnInitialItemOnStart ||
                string.IsNullOrEmpty(table.initialItemSpriteName))
            {
                continue;
            }

            if (table.initialItemSpriteName == spriteName)
                return true;
        }

        return false;
    }
}
