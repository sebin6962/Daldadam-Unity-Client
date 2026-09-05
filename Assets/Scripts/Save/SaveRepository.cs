using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

public static class SaveRepository
{
    [Serializable]
    private class LegacyLevelData
    {
        public int Level;
        public int Exp;
    }

    [Serializable]
    private class LegacyUnlockProgressData
    {
        public List<int> pendingLevels;
        public List<int> appliedLevels;
        public bool initialized;
    }

    public static string GetSavePath(string serverName)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"save_myuser_{serverName}.json"
        );
    }

    private static string GetLegacyStarPath(string serverName)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"playerStarData_{serverName}.json"
        );
    }

    private static string GetLegacyLevelPath(string serverName)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"player_level_data_{serverName}.json"
        );
    }

    private static string GetLegacyWorldTimePath(string serverName)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"dayData_{serverName}.json"
        );
    }

    private static string GetLegacyPlaytimePath(
    string serverName
)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"playtime_{serverName}.json"
        );
    }

    private static string GetLegacyStoragePath(
        string serverName
    )
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"storage_{serverName}.json"
        );
    }

    private static string GetLegacyMakerPath(
        string serverName
    )
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"maker_{serverName}.json"
        );
    }

    private static string GetLegacyTablePath(
        string serverName
    )
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"ps_tableItem_{serverName}.json"
        );
    }

    private static string GetLegacyFarmPath(
        string serverName
    )
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"farm_{serverName}.json"
        );
    }

    private static string GetLegacyTutorialPath(
    string serverName
)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"tutorial_{serverName}.json"
        );
    }

    private static string GetLegacyTreeUnlockPath(
    string serverName
)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"treeUnlock_{serverName}.json"
        );
    }

    private static string GetLegacyUnlockPath(
    string serverName
)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"unlock_{serverName}.json"
        );
    }

    private static string GetLegacyEndingPath(
    string serverName
)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"ending_{serverName}.json"
        );
    }

    public static bool Exists(string serverName)
    {
        return File.Exists(GetSavePath(serverName));
    }

    public static SaveData Load(string serverName)
    {
        string path = GetSavePath(serverName);

        if (!File.Exists(path))
        {
            Debug.LogWarning(
                $"[SaveRepository] 세이브 파일이 없습니다: {path}"
            );

            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveData saveData = JsonUtility.FromJson<SaveData>(json);

            if (saveData == null)
            {
                Debug.LogError(
                    $"[SaveRepository] JSON 변환에 실패했습니다: {path}"
                );

                return null;
            }

            bool dataChanged = MigrateStarData(
    serverName,
    saveData
);

            if (MigrateLevelData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (MigrateWorldTimeData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (MigratePlaytimeData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (MigratePlayerLocationData(saveData))
            {
                dataChanged = true;
            }

            if (MigrateStorageData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (MigrateMakerData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (MigrateTableData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (MigrateFarmData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (MigrateTutorialData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (MigrateTreeUnlockData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (MigrateUnlockProgressData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (MigrateEndingData(serverName, saveData))
            {
                dataChanged = true;
            }

            if (saveData.levelData == null)
            {
                saveData.levelData = new LevelSaveData
                {
                    level = 1,
                    exp = 0
                };

                dataChanged = true;
            }

            if (saveData.starData == null)
            {
                saveData.starData = new StarSaveData();
                dataChanged = true;
            }

            if (saveData.starData == null)
            {
                saveData.starData = new StarSaveData
                {
                    starlight = 0
                };

                dataChanged = true;
            }

            if (saveData.levelData == null)
            {
                saveData.levelData = new LevelSaveData
                {
                    level = 1,
                    exp = 0
                };

                dataChanged = true;
            }

            if (saveData.worldTimeData == null)
            {
                saveData.worldTimeData =
                    new WorldTimeSaveData
                    {
                        day = 1,
                        hour = 9,
                        minute = 0
                    };

                dataChanged = true;
            }

            if (saveData.playtimeData == null)
            {
                saveData.playtimeData =
                    new PlaytimeSaveData
                    {
                        seconds = 0,
                        lastPlayed = ""
                    };

                dataChanged = true;
            }

            if (saveData.tutorialData == null)
            {
                saveData.tutorialData =
                    new TutorialStateData
                    {
                        tutorialDone = false
                    };

                dataChanged = true;
            }

            if (saveData.treeUnlockData == null)
            {
                saveData.treeUnlockData =
                    new TreeUnlockData
                    {
                        currentUnlockedLevel = 0
                    };

                dataChanged = true;
            }

            if (saveData.unlockProgressData == null)
            {
                saveData.unlockProgressData =
                    new UnlockProgressSaveData
                    {
                        pendingLevels = new List<int>(),
                        appliedLevels = new List<int> { 1 },
                        initialized = true
                    };

                dataChanged = true;
            }

            if (saveData.endingData == null)
            {
                saveData.endingData =
                    new EndingData
                    {
                        hasSeenEnding = false
                    };

                dataChanged = true;
            }

            if (saveData.npcDialogueProgressData == null)
            {
                saveData.npcDialogueProgressData =
                    new NPCDialogueProgressDataList();

                dataChanged = true;
            }

            if (saveData.npcDialogueProgressData.npcProgressList == null)
            {
                saveData.npcDialogueProgressData.npcProgressList =
                    new List<NPCDialogueNpcProgressData>();

                dataChanged = true;
            }

            if (saveData.storageItems == null)
            {
                saveData.storageItems = new List<StorageEntry>();
                dataChanged = true;
            }

            if (saveData.makerData == null)
            {
                saveData.makerData = new MakerSaveData();
                dataChanged = true;
            }

            if (saveData.makerData.makers == null)
            {
                saveData.makerData.makers =
                    new List<MakerSlotSave>();

                dataChanged = true;
            }

            if (saveData.tableData == null)
            {
                saveData.tableData = new TableSaveData();
                dataChanged = true;
            }

            if (saveData.tableData.tables == null)
            {
                saveData.tableData.tables =
                    new List<TableSlotSave>();

                dataChanged = true;
            }

            if (NormalizeFarmData(saveData))
            {
                dataChanged = true;
            }

            if (saveData.playerLocationData == null)
            {
                saveData.playerLocationData =
                    new PlayerLocationSaveData();

                dataChanged = true;
            }

            // 모든 마이그레이션과 null 보정이 끝난 뒤 한 번만 저장
            if (dataChanged)
            {
                Save(serverName, saveData);
            }

            return saveData;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[SaveRepository] 세이브 불러오기 실패\n" +
                $"경로: {path}\n" +
                $"오류: {exception.Message}"
            );

            return null;
        }
    }

    private static bool MigrateStarData(
        string serverName,
        SaveData saveData
    )
    {
        // 이미 이전이 끝난 세이브라면 다시 읽지 않는다.
        if (saveData.starDataMigrationCompleted)
            return false;

        string legacyPath = GetLegacyStarPath(serverName);

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson = File.ReadAllText(legacyPath);

                StarSaveData legacyData =
                    JsonUtility.FromJson<StarSaveData>(legacyJson);

                if (legacyData == null)
                {
                    Debug.LogError(
                        $"[SaveRepository] 기존 별빛 데이터 변환 실패: " +
                        legacyPath
                    );

                    return false;
                }

                saveData.starData = legacyData;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[SaveRepository] 기존 별빛 데이터 이전 실패\n" +
                    $"경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                // 실패했다면 완료 처리하지 않는다.
                return false;
            }
        }
        else
        {
            if (saveData.starData == null)
            {
                saveData.starData = new StarSaveData();
            }

            // 별도 파일이 없는 구형 세이브를 위한 보조 처리
            saveData.starData.starlight = saveData.starlight;
        }

        saveData.starDataMigrationCompleted = true;

        Debug.Log(
            $"[SaveRepository] 별빛 데이터 통합 완료: {serverName}"
        );

        return true;
    }

    public static bool Save(
        string serverName,
        SaveData saveData
    )
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            Debug.LogError(
                "[SaveRepository] serverName이 비어 있습니다."
            );

            return false;
        }

        if (saveData == null)
        {
            Debug.LogError(
                "[SaveRepository] 저장할 SaveData가 없습니다."
            );

            return false;
        }

        try
        {
            saveData.serverName = serverName;

            string path = GetSavePath(serverName);
            string json = JsonUtility.ToJson(saveData, true);

            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[SaveRepository] 세이브 저장 실패\n" +
                $"마을: {serverName}\n" +
                $"오류: {exception.Message}"
            );

            return false;
        }
    }

    private static bool MigrateLevelData(
    string serverName,
    SaveData saveData
)
    {
        if (saveData.levelDataMigrationCompleted)
            return false;

        string legacyPath = GetLegacyLevelPath(serverName);

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson = File.ReadAllText(legacyPath);

                LegacyLevelData legacyData =
                    JsonUtility.FromJson<LegacyLevelData>(legacyJson);

                if (legacyData == null)
                {
                    Debug.LogError(
                        $"[SaveRepository] 기존 레벨 데이터 변환 실패: " +
                        legacyPath
                    );

                    return false;
                }

                saveData.levelData = new LevelSaveData
                {
                    level = Mathf.Max(1, legacyData.Level),
                    exp = Mathf.Max(0, legacyData.Exp)
                };
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[SaveRepository] 기존 레벨 데이터 이전 실패\n" +
                    $"경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                return false;
            }
        }
        else
        {
            // 별도 레벨 파일이 없는 구형 세이브 대응
            saveData.levelData = new LevelSaveData
            {
                level = Mathf.Max(1, saveData.level),
                exp = Mathf.Max(0, saveData.exp)
            };
        }

        saveData.levelDataMigrationCompleted = true;

        Debug.Log(
            $"[SaveRepository] 레벨 데이터 통합 완료: {serverName}"
        );

        return true;
    }

    private static bool MigrateWorldTimeData(
    string serverName,
    SaveData saveData
)
    {
        if (saveData.worldTimeMigrationCompleted)
            return false;

        string legacyPath =
            GetLegacyWorldTimePath(serverName);

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson =
                    File.ReadAllText(legacyPath);

                WorldTimeSaveData legacyData =
                    JsonUtility.FromJson<WorldTimeSaveData>(
                        legacyJson
                    );

                if (legacyData == null)
                {
                    Debug.LogError(
                        "[SaveRepository] 기존 날짜·시간 데이터 " +
                        $"변환 실패: {legacyPath}"
                    );

                    return false;
                }

                saveData.worldTimeData =
                    new WorldTimeSaveData
                    {
                        day = Mathf.Max(1, legacyData.day),
                        hour = Mathf.Clamp(
                            legacyData.hour,
                            0,
                            26
                        ),
                        minute = Mathf.Clamp(
                            legacyData.minute,
                            0,
                            59
                        )
                    };
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveRepository] 기존 날짜·시간 데이터 " +
                    $"이전 실패\n경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                // 실패했으므로 완료로 표시하지 않는다.
                return false;
            }
        }
        else
        {
            // dayData 파일이 없는 오래된 세이브 대응
            saveData.worldTimeData =
                new WorldTimeSaveData
                {
                    day = Mathf.Max(1, saveData.day),
                    hour = 9,
                    minute = 0
                };
        }

        saveData.worldTimeMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 날짜·시간 데이터 통합 완료: " +
            serverName
        );

        return true;
    }

    private static bool MigratePlaytimeData(
    string serverName,
    SaveData saveData
)
    {
        if (saveData.playtimeMigrationCompleted)
            return false;

        string legacyPath =
            GetLegacyPlaytimePath(serverName);

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson =
                    File.ReadAllText(legacyPath);

                PlaytimeSaveData legacyData =
                    JsonUtility.FromJson<PlaytimeSaveData>(
                        legacyJson
                    );

                if (legacyData == null)
                {
                    Debug.LogError(
                        "[SaveRepository] 기존 플레이 시간 " +
                        $"변환 실패: {legacyPath}"
                    );

                    return false;
                }

                saveData.playtimeData =
                    new PlaytimeSaveData
                    {
                        seconds = Math.Max(
                            0,
                            legacyData.seconds
                        ),
                        lastPlayed =
                            legacyData.lastPlayed ?? ""
                    };
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveRepository] 기존 플레이 시간 " +
                    $"이전 실패\n경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                return false;
            }
        }
        else
        {
            saveData.playtimeData =
                new PlaytimeSaveData
                {
                    seconds = 0,
                    lastPlayed = ""
                };
        }

        saveData.playtimeMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 플레이 시간 데이터 통합 완료: " +
            serverName
        );

        return true;
    }

    private static bool MigrateStorageData(
        string serverName,
        SaveData saveData
    )
    {
        if (saveData.storageMigrationCompleted)
            return false;

        string legacyPath = GetLegacyStoragePath(serverName);
        List<StorageEntry> migratedItems = new List<StorageEntry>();

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson = File.ReadAllText(legacyPath);
                StorageData legacyData =
                    JsonUtility.FromJson<StorageData>(legacyJson);

                if (legacyData == null)
                {
                    Debug.LogError(
                        "[SaveRepository] 기존 창고 데이터 변환 실패: " +
                        legacyPath
                    );

                    return false;
                }

                if (legacyData.items != null)
                {
                    foreach (StorageEntry entry in legacyData.items)
                    {
                        AddOrMergeStorageEntry(migratedItems, entry);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveRepository] 기존 창고 데이터 읽기 실패\n" +
                    $"경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                return false;
            }
        }
        else
        {
            // 별도 창고 파일이 없던 슬롯은 기존 창고 기본값을 사용한다.
            migratedItems.Add(new StorageEntry
            {
                name = "Mepssalgaru",
                amount = 10
            });
        }

        saveData.storageItems = migratedItems;
        saveData.storageMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 창고 데이터 통합 완료: " +
            serverName
        );

        return true;
    }

    private static bool MigratePlayerLocationData(
        SaveData saveData
    )
    {
        if (saveData.playerLocationMigrationCompleted)
            return false;

        bool hasLegacyPosition =
            !Mathf.Approximately(saveData.playerPosX, 0f) ||
            !Mathf.Approximately(saveData.playerPosY, 0f);

        Vector2 legacyFacing = new Vector2(
            saveData.moveDirX,
            saveData.moveDirY
        );

        if (legacyFacing.sqrMagnitude < 0.001f)
            legacyFacing = Vector2.down;
        else
            legacyFacing.Normalize();

        saveData.playerLocationData =
            new PlayerLocationSaveData
            {
                // 기존 필드에는 씬 정보가 없었으므로 마을 위치로만 이전한다.
                sceneName = hasLegacyPosition
                    ? "VillageScene"
                    : "",
                positionX = saveData.playerPosX,
                positionY = saveData.playerPosY,
                facingX = legacyFacing.x,
                facingY = legacyFacing.y,
                initialized = hasLegacyPosition
            };

        saveData.playerLocationMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 플레이어 위치 데이터 통합 완료: " +
            saveData.serverName
        );

        return true;
    }

    private static void AddOrMergeStorageEntry(
        List<StorageEntry> entries,
        StorageEntry entry
    )
    {
        if (entry == null ||
            string.IsNullOrWhiteSpace(entry.name) ||
            entry.amount <= 0)
        {
            return;
        }

        StorageEntry existing = entries.Find(
            item => item.name == entry.name
        );

        if (existing == null)
        {
            entries.Add(new StorageEntry
            {
                name = entry.name,
                amount = entry.amount
            });

            return;
        }

        existing.amount += entry.amount;
    }

    private static bool MigrateMakerData(
        string serverName,
        SaveData saveData
    )
    {
        if (saveData.makerMigrationCompleted)
            return false;

        string legacyPath = GetLegacyMakerPath(serverName);
        MakerSaveData migratedData = new MakerSaveData();

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson = File.ReadAllText(legacyPath);
                MakerSaveData legacyData =
                    JsonUtility.FromJson<MakerSaveData>(legacyJson);

                if (legacyData == null)
                {
                    Debug.LogError(
                        "[SaveRepository] 기존 제작대 데이터 변환 실패: " +
                        legacyPath
                    );

                    return false;
                }

                if (legacyData.makers != null)
                {
                    foreach (MakerSlotSave maker in legacyData.makers)
                    {
                        AddOrReplaceMakerState(
                            migratedData.makers,
                            maker
                        );
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveRepository] 기존 제작대 데이터 읽기 실패\n" +
                    $"경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                return false;
            }
        }

        saveData.makerData = migratedData;
        saveData.makerMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 제작대 데이터 통합 완료: " +
            serverName
        );

        return true;
    }

    private static void AddOrReplaceMakerState(
        List<MakerSlotSave> makers,
        MakerSlotSave source
    )
    {
        if (source == null ||
            string.IsNullOrWhiteSpace(source.makerId))
        {
            return;
        }

        List<string> inputItems = new List<string>();

        if (source.inputItemNames != null)
        {
            foreach (string itemName in source.inputItemNames)
            {
                if (!string.IsNullOrWhiteSpace(itemName))
                    inputItems.Add(itemName);
            }
        }

        bool canProduce =
            source.isProducing &&
            !string.IsNullOrWhiteSpace(source.resultItemName);

        MakerSlotSave copiedState = new MakerSlotSave
        {
            makerId = source.makerId,
            inputItemNames = inputItems,
            isProducing = canProduce,
            resultItemName = source.resultItemName,
            craftEndUtcSeconds = canProduce
                ? Math.Max(0d, source.craftEndUtcSeconds)
                : 0d
        };

        int existingIndex = makers.FindIndex(
            maker => maker != null &&
                     maker.makerId == copiedState.makerId
        );

        if (existingIndex >= 0)
            makers[existingIndex] = copiedState;
        else
            makers.Add(copiedState);
    }

    private static bool MigrateTableData(
        string serverName,
        SaveData saveData
    )
    {
        if (saveData.tableMigrationCompleted)
            return false;

        string legacyPath = GetLegacyTablePath(serverName);
        TableSaveData migratedData = new TableSaveData();

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson = File.ReadAllText(legacyPath);
                TableSaveData legacyData =
                    JsonUtility.FromJson<TableSaveData>(legacyJson);

                if (legacyData == null)
                {
                    Debug.LogError(
                        "[SaveRepository] 기존 테이블 데이터 변환 실패: " +
                        legacyPath
                    );

                    return false;
                }

                if (legacyData.tables != null)
                {
                    foreach (TableSlotSave table in legacyData.tables)
                    {
                        AddOrReplaceTableState(
                            migratedData.tables,
                            table
                        );
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveRepository] 기존 테이블 데이터 읽기 실패\n" +
                    $"경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                return false;
            }
        }

        saveData.tableData = migratedData;
        saveData.tableMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 테이블 데이터 통합 완료: " +
            serverName
        );

        return true;
    }

    private static void AddOrReplaceTableState(
        List<TableSlotSave> tables,
        TableSlotSave source
    )
    {
        if (source == null ||
            string.IsNullOrWhiteSpace(source.tableId))
        {
            return;
        }

        bool hasItem =
            source.hasItem ||
            !string.IsNullOrWhiteSpace(source.itemSpriteName);

        TableSlotSave copiedState = new TableSlotSave
        {
            tableId = source.tableId,
            hasItem = hasItem,
            itemSpriteName = hasItem
                ? source.itemSpriteName
                : ""
        };

        int existingIndex = tables.FindIndex(
            table => table != null &&
                     table.tableId == copiedState.tableId
        );

        if (existingIndex >= 0)
            tables[existingIndex] = copiedState;
        else
            tables.Add(copiedState);
    }

    private static bool MigrateFarmData(
        string serverName,
        SaveData saveData
    )
    {
        if (saveData.farmMigrationCompleted)
            return false;

        string legacyPath = GetLegacyFarmPath(serverName);
        FarmSaveData migratedData = new FarmSaveData();

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson = File.ReadAllText(legacyPath);
                FarmSaveData legacyData =
                    JsonUtility.FromJson<FarmSaveData>(legacyJson);

                if (legacyData == null)
                {
                    Debug.LogError(
                        "[SaveRepository] 기존 농장 데이터 변환 실패: " +
                        legacyPath
                    );

                    return false;
                }

                if (legacyData.crops != null)
                {
                    foreach (CropTileSave crop in legacyData.crops)
                    {
                        AddOrReplaceFarmCrop(migratedData.crops, crop);
                    }
                }

                CopyFarmWetTiles(legacyData, migratedData);
                migratedData.lastSavedUtcSeconds =
                    Math.Max(0d, legacyData.lastSavedUtcSeconds);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveRepository] 기존 농장 데이터 읽기 실패\n" +
                    $"경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                return false;
            }
        }

        saveData.farmData = migratedData;
        saveData.farmMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 농장 데이터 통합 완료: " +
            serverName
        );

        return true;
    }

    private static bool NormalizeFarmData(SaveData saveData)
    {
        bool changed = false;

        if (saveData.farmData == null)
        {
            saveData.farmData = new FarmSaveData();
            changed = true;
        }

        FarmSaveData source = saveData.farmData;
        FarmSaveData normalized = new FarmSaveData
        {
            lastSavedUtcSeconds =
                Math.Max(0d, source.lastSavedUtcSeconds)
        };

        if (source.crops != null)
        {
            foreach (CropTileSave crop in source.crops)
            {
                AddOrReplaceFarmCrop(normalized.crops, crop);
            }
        }
        else
        {
            changed = true;
        }

        if (source.wetXs == null || source.wetYs == null)
            changed = true;

        CopyFarmWetTiles(source, normalized);

        if (JsonUtility.ToJson(source) != JsonUtility.ToJson(normalized))
        {
            changed = true;
        }

        saveData.farmData = normalized;

        return changed;
    }

    private static void AddOrReplaceFarmCrop(
        List<CropTileSave> crops,
        CropTileSave source
    )
    {
        if (source == null ||
            string.IsNullOrWhiteSpace(source.harvestItemName))
        {
            return;
        }

        CropTileSave copiedState = new CropTileSave
        {
            x = source.x,
            y = source.y,
            harvestItemName = source.harvestItemName,
            currentStage = Math.Max(0, source.currentStage),
            timer = Mathf.Max(0f, source.timer),
            isWatered = source.isWatered,
            lastWaterTime = source.lastWaterTime,
            isTree = source.isTree,
            autoRegrow = source.autoRegrow && source.isTree
        };

        int existingIndex = crops.FindIndex(
            crop => crop != null &&
                    crop.x == copiedState.x &&
                    crop.y == copiedState.y
        );

        if (existingIndex >= 0)
            crops[existingIndex] = copiedState;
        else
            crops.Add(copiedState);
    }

    private static void CopyFarmWetTiles(
        FarmSaveData source,
        FarmSaveData destination
    )
    {
        if (source.wetXs == null || source.wetYs == null)
            return;

        int count = Math.Min(source.wetXs.Count, source.wetYs.Count);
        HashSet<string> positions = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            string key = source.wetXs[i] + ":" + source.wetYs[i];
            if (!positions.Add(key))
                continue;

            destination.wetXs.Add(source.wetXs[i]);
            destination.wetYs.Add(source.wetYs[i]);
        }
    }

    private static bool MigrateTutorialData(
    string serverName,
    SaveData saveData
)
    {
        if (saveData.tutorialMigrationCompleted)
            return false;

        string legacyPath =
            GetLegacyTutorialPath(serverName);

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson =
                    File.ReadAllText(legacyPath);

                TutorialStateData legacyData =
                    JsonUtility.FromJson<TutorialStateData>(
                        legacyJson
                    );

                if (legacyData == null)
                {
                    Debug.LogError(
                        "[SaveRepository] 기존 튜토리얼 데이터 " +
                        $"변환 실패: {legacyPath}"
                    );

                    return false;
                }

                saveData.tutorialData =
                    new TutorialStateData
                    {
                        tutorialDone =
                            legacyData.tutorialDone
                    };
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveRepository] 기존 튜토리얼 데이터 " +
                    $"이전 실패\n경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                return false;
            }
        }
        else
        {
            saveData.tutorialData =
                new TutorialStateData
                {
                    tutorialDone = false
                };
        }

        saveData.tutorialMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 튜토리얼 데이터 통합 완료: " +
            serverName
        );

        return true;
    }

    private static bool MigrateTreeUnlockData(
    string serverName,
    SaveData saveData
)
    {
        if (saveData.treeUnlockMigrationCompleted)
            return false;

        string legacyPath =
            GetLegacyTreeUnlockPath(serverName);

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson =
                    File.ReadAllText(legacyPath);

                TreeUnlockData legacyData =
                    JsonUtility.FromJson<TreeUnlockData>(
                        legacyJson
                    );

                if (legacyData == null)
                {
                    Debug.LogError(
                        "[SaveRepository] 기존 계수나무 데이터 " +
                        $"변환 실패: {legacyPath}"
                    );

                    return false;
                }

                saveData.treeUnlockData =
                    new TreeUnlockData
                    {
                        currentUnlockedLevel =
                            Mathf.Max(
                                0,
                                legacyData.currentUnlockedLevel
                            )
                    };
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveRepository] 기존 계수나무 데이터 " +
                    $"이전 실패\n경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                return false;
            }
        }
        else
        {
            // 별도 파일이 없는 초기 통합 세이브 대응
            saveData.treeUnlockData =
                new TreeUnlockData
                {
                    currentUnlockedLevel =
                        Mathf.Max(
                            0,
                            saveData.currentUnlockedTreeLevel
                        )
                };
        }

        saveData.treeUnlockMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 계수나무 데이터 통합 완료: " +
            serverName
        );

        return true;
    }

    private static bool MigrateUnlockProgressData(
    string serverName,
    SaveData saveData
)
    {
        if (saveData.unlockProgressMigrationCompleted)
            return false;

        string legacyPath =
            GetLegacyUnlockPath(serverName);

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson =
                    File.ReadAllText(legacyPath);

                LegacyUnlockProgressData legacyData =
                    JsonUtility.FromJson
                        <LegacyUnlockProgressData>(
                            legacyJson
                        );

                if (legacyData == null)
                {
                    Debug.LogError(
                        "[SaveRepository] 기존 레벨별 해금 " +
                        $"데이터 변환 실패: {legacyPath}"
                    );

                    return false;
                }

                saveData.unlockProgressData =
                    new UnlockProgressSaveData
                    {
                        pendingLevels =
                            legacyData.pendingLevels ??
                            new List<int>(),

                        appliedLevels =
                            legacyData.appliedLevels ??
                            new List<int>(),

                        initialized =
                            legacyData.initialized
                    };
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveRepository] 기존 레벨별 해금 " +
                    $"데이터 이전 실패\n경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                return false;
            }
        }
        else
        {
            saveData.unlockProgressData =
                new UnlockProgressSaveData
                {
                    pendingLevels = new List<int>(),
                    appliedLevels = new List<int> { 1 },
                    initialized = true
                };
        }

        saveData.unlockProgressMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 레벨별 해금 데이터 통합 완료: " +
            serverName
        );

        return true;
    }

    private static bool MigrateEndingData(
    string serverName,
    SaveData saveData
)
    {
        if (saveData.endingMigrationCompleted)
        {
            return false;
        }

        string legacyPath =
            GetLegacyEndingPath(serverName);

        if (File.Exists(legacyPath))
        {
            try
            {
                string legacyJson =
                    File.ReadAllText(legacyPath);

                EndingData legacyData =
                    JsonUtility.FromJson<EndingData>(
                        legacyJson
                    );

                if (legacyData == null)
                {
                    Debug.LogError(
                        "[SaveRepository] 기존 엔딩 데이터 " +
                        $"변환 실패: {legacyPath}"
                    );

                    return false;
                }

                saveData.endingData =
                    new EndingData
                    {
                        hasSeenEnding =
                            legacyData.hasSeenEnding
                    };
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveRepository] 기존 엔딩 데이터 " +
                    $"이전 실패\n경로: {legacyPath}\n" +
                    $"오류: {exception.Message}"
                );

                return false;
            }
        }
        else if (saveData.endingData == null)
        {
            saveData.endingData =
                new EndingData
                {
                    hasSeenEnding = false
                };
        }

        saveData.endingMigrationCompleted = true;

        Debug.Log(
            "[SaveRepository] 엔딩 데이터 통합 완료: " +
            serverName
        );

        return true;
    }

    public static void Delete(string serverName)
    {
        string path = GetSavePath(serverName);

        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[SaveRepository] 세이브 삭제 실패\n" +
                $"경로: {path}\n" +
                $"오류: {exception.Message}"
            );
        }
    }
}
