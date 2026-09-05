using System;
using UnityEngine;

public static class SaveService
{
    public static event Action<SaveData> CurrentSaveChanged;

    public static SaveData CurrentData
    {
        get;
        private set;
    }

    public static string CurrentServer
    {
        get;
        private set;
    } = "";

    public static bool HasCurrentSave
    {
        get
        {
            return CurrentData != null &&
                   !string.IsNullOrWhiteSpace(CurrentServer);
        }
    }

    // 에디터에서 Domain Reload를 껐을 때도
    // 이전 플레이의 static 데이터가 남지 않게 초기화
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetState()
    {
        CurrentData = null;
        CurrentServer = "";
        CurrentSaveChanged = null;
    }

    public static bool Load(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            Debug.LogError(
                "[SaveService] 불러올 serverName이 비어 있습니다."
            );

            return false;
        }

        SaveData loadedData =
            SaveRepository.Load(serverName);

        if (loadedData == null)
        {
            Debug.LogError(
                "[SaveService] 통합 세이브 불러오기 실패: " +
                serverName
            );

            return false;
        }

        CurrentServer = serverName;
        CurrentData = loadedData;

        CurrentSaveChanged?.Invoke(CurrentData);

        Debug.Log(
            "[SaveService] 현재 세이브 설정 완료: " +
            serverName
        );

        return true;
    }

    public static bool LoadSelectedSave()
    {
        string serverName = PlayerPrefs.GetString(
            "SelectedSave",
            ""
        );

        return Load(serverName);
    }

    public static bool EnsureLoaded(string serverName)
    {
        if (HasCurrentSave &&
            CurrentServer == serverName)
        {
            return true;
        }

        return Load(serverName);
    }

    public static void SetCurrent(
        string serverName,
        SaveData saveData
    )
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            Debug.LogError(
                "[SaveService] serverName이 비어 있습니다."
            );

            return;
        }

        if (saveData == null)
        {
            Debug.LogError(
                "[SaveService] 설정할 SaveData가 없습니다."
            );

            return;
        }

        saveData.serverName = serverName;

        CurrentServer = serverName;
        CurrentData = saveData;

        CurrentSaveChanged?.Invoke(CurrentData);
    }

    public static bool SaveCurrent()
    {
        if (!HasCurrentSave)
        {
            Debug.LogWarning(
                "[SaveService] 현재 불러온 세이브가 없어 " +
                "저장을 건너뜁니다."
            );

            return false;
        }

        return SaveRepository.Save(
            CurrentServer,
            CurrentData
        );
    }

    public static bool IsCurrent(string serverName)
    {
        return HasCurrentSave &&
               CurrentServer == serverName;
    }

    public static void Clear()
    {
        CurrentData = null;
        CurrentServer = "";

        CurrentSaveChanged?.Invoke(null);
    }
}
