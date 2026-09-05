using UnityEngine;

public class PlayerLevelManager : MonoBehaviour
{
    public static PlayerLevelManager Instance;

    public int Level { get; private set; } = 1;
    public int Exp { get; private set; } = 0;
    public int ExpToNextLevel => (100 + (Level - 1) * 50) *2;

    private string serverName;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetServerName(string serverName)
    {
        this.serverName = serverName;
    }

    public void AddExp(int amount)
    {
        Exp += amount;
        while (Exp >= ExpToNextLevel)
        {
            Exp -= ExpToNextLevel;
            Level++;
            UnlockManager.Instance?.ScheduleUnlockForLevel(Level); //레벨업 시 다음 날 적용
        }
        Save(); // 경험치가 바뀔 때마다 저장
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            Debug.LogError(
                "[PlayerLevelManager] serverName이 설정되지 않았습니다."
            );

            return;
        }

        if (!SaveService.EnsureLoaded(serverName))
        {
            Debug.LogError(
                "[PlayerLevelManager] 현재 세이브를 준비할 수 없습니다: " +
                serverName
            );

            return;
        }

        SaveService.CurrentData.levelData =
            new LevelSaveData
            {
                level = Level,
                exp = Exp
            };

        SaveService.CurrentData
            .levelDataMigrationCompleted = true;

        SaveService.SaveCurrent();
    }

    public void Load()
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            Debug.LogError(
                "[PlayerLevelManager] serverName이 설정되지 않았습니다."
            );

            return;
        }

        if (!SaveService.EnsureLoaded(serverName))
        {
            Level = 1;
            Exp = 0;
            return;
        }

        LevelSaveData levelData =
            SaveService.CurrentData.levelData;

        if (levelData == null)
        {
            Level = 1;
            Exp = 0;
            return;
        }

        Level = Mathf.Max(1, levelData.level);
        Exp = Mathf.Max(0, levelData.exp);
    }

    public void SetLevelAndExp(int level, int exp)
    {
        this.Level = Mathf.Max(1, level);
        this.Exp = Mathf.Max(0, exp);
    }
}
