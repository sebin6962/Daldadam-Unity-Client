using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StorageEntry
{
    public string name;
    public int amount;
}

// 기존 storage_{serverName}.json 이전에만 사용하는 레거시 구조
[System.Serializable]
public class StorageData
{
    public List<StorageEntry> items = new List<StorageEntry>();
}

public class StorageInventory : MonoBehaviour
{
    public static StorageInventory Instance;

    private readonly Dictionary<string, int> storage =
        new Dictionary<string, int>();

    public int maxSlots = 12;
    public int maxStackPerItem = 99;

    public int OccupiedSlots => storage.Count;
    public int FreeSlots => Mathf.Max(0, maxSlots - storage.Count);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SaveService.CurrentSaveChanged += OnCurrentSaveChanged;
        LoadStorage();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SaveService.CurrentSaveChanged -= OnCurrentSaveChanged;
        Instance = null;
    }

    private void OnCurrentSaveChanged(SaveData saveData)
    {
        if (saveData == null)
        {
            storage.Clear();
            return;
        }

        LoadStorage();
    }

    // 기존 호출부와의 호환성을 유지한다.
    // 파일 경로를 설정하는 대신 SaveService의 현재 슬롯을 전환한다.
    public void SetServerName(string serverName)
    {
        if (!SaveService.EnsureLoaded(serverName))
        {
            Debug.LogError(
                "[StorageInventory] 저장 슬롯 전환 실패: " +
                serverName
            );

            return;
        }

        LoadStorage();
    }

    public void AddItem(string itemName, int amount)
    {
        if (string.IsNullOrEmpty(itemName))
            return;

        if (storage.ContainsKey(itemName))
        {
            storage[itemName] += amount;

            if (storage[itemName] <= 0)
                storage.Remove(itemName);

            return;
        }

        if (amount > 0)
        {
            storage[itemName] = amount;
            return;
        }

        Debug.LogWarning(
            $"[StorageInventory] 없는 아이템 '{itemName}'에 " +
            $"음수 {amount} 추가 시도"
        );
    }

    public int GetItemCount(string itemName)
    {
        return storage.TryGetValue(itemName, out int count)
            ? count
            : 0;
    }

    public bool SaveStorage()
    {
        if (!SaveService.HasCurrentSave)
        {
            Debug.LogWarning(
                "[StorageInventory] 현재 저장 슬롯이 없어 " +
                "창고 저장을 건너뜁니다."
            );

            return false;
        }

        List<StorageEntry> entries = new List<StorageEntry>();

        foreach (KeyValuePair<string, int> pair in storage)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
                continue;

            entries.Add(new StorageEntry
            {
                name = pair.Key,
                amount = pair.Value
            });
        }

        SaveService.CurrentData.storageItems = entries;
        SaveService.CurrentData.storageMigrationCompleted = true;

        return SaveService.SaveCurrent();
    }

    public void LoadStorage()
    {
        if (!SaveService.HasCurrentSave)
        {
            storage.Clear();
            return;
        }

        LoadFromSaveData(
            SaveService.CurrentData.storageItems
        );
    }

    public bool HasItem(string itemName)
    {
        return storage.ContainsKey(itemName);
    }

    public Dictionary<string, int> GetAllItems()
    {
        return new Dictionary<string, int>(storage);
    }

    public void LoadFromSaveData(List<StorageEntry> entries)
    {
        storage.Clear();

        if (entries == null)
            return;

        foreach (StorageEntry entry in entries)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.name) ||
                entry.amount <= 0)
            {
                continue;
            }

            if (storage.ContainsKey(entry.name))
                storage[entry.name] += entry.amount;
            else
                storage[entry.name] = entry.amount;
        }
    }

    public bool HasRoomFor(string itemName, int amount)
    {
        if (string.IsNullOrEmpty(itemName) || amount <= 0)
            return true;

        if (storage.TryGetValue(itemName, out int current))
        {
            long after = (long)current + amount;
            return after <= maxStackPerItem;
        }

        return FreeSlots >= 1 && amount <= maxStackPerItem;
    }

    public bool TryAddItem(string itemName, int amount)
    {
        if (!HasRoomFor(itemName, amount))
            return false;

        AddItem(itemName, amount);
        return true;
    }
}
