using System.Collections.Generic;
using UnityEngine;

public class NPCDialogueProgressManager : MonoBehaviour
{
    public static NPCDialogueProgressManager Instance;

    private const string LegacySaveKey = "NPC_DIALOGUE_PROGRESS";

    [SerializeField] private NPCDialogueProgressDataList progressData = new NPCDialogueProgressDataList();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SaveService.CurrentSaveChanged += OnCurrentSaveChanged;
            Load();
        }
        else
        {
            Destroy(gameObject);
        }
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
            progressData = new NPCDialogueProgressDataList();
            return;
        }

        Load();
    }

    public NPCDialogueNpcProgressData GetOrCreateNpcProgress(string npcId)
    {
        if (string.IsNullOrEmpty(npcId))
            return null;

        if (progressData == null)
            progressData = new NPCDialogueProgressDataList();

        if (progressData.npcProgressList == null)
            progressData.npcProgressList = new List<NPCDialogueNpcProgressData>();

        NPCDialogueNpcProgressData npcProgress =
            progressData.npcProgressList.Find(p => p.npcId == npcId);

        if (npcProgress == null)
        {
            npcProgress = new NPCDialogueNpcProgressData();
            npcProgress.npcId = npcId;
            progressData.npcProgressList.Add(npcProgress);
        }

        if (npcProgress.categoryProgressList == null)
            npcProgress.categoryProgressList = new List<NPCDialogueCategoryProgressData>();

        return npcProgress;
    }

    public void Save()
    {
        if (progressData == null)
            progressData = new NPCDialogueProgressDataList();

        EnsureProgressLists();

        if (!SaveService.HasCurrentSave)
        {
            Debug.LogWarning(
                "[NPCDialogueProgressManager] 현재 선택된 저장 슬롯이 없어 " +
                "NPC 대화 진행도를 저장하지 않았습니다."
            );

            return;
        }

        SaveService.CurrentData.npcDialogueProgressData = progressData;
        SaveService.CurrentData.npcDialogueProgressMigrationCompleted = true;
        SaveService.SaveCurrent();
    }

    public void Load()
    {
        if (!SaveService.HasCurrentSave)
        {
            progressData = new NPCDialogueProgressDataList();
            return;
        }

        SaveData saveData = SaveService.CurrentData;

        if (!saveData.npcDialogueProgressMigrationCompleted)
        {
            MigrateLegacyProgress(saveData);
        }

        progressData = saveData.npcDialogueProgressData;

        if (progressData == null)
            progressData = new NPCDialogueProgressDataList();

        EnsureProgressLists();

        saveData.npcDialogueProgressData = progressData;
    }

    public void ResetAllProgress()
    {
        progressData = new NPCDialogueProgressDataList();
        EnsureProgressLists();

        // 이전 버전의 전역 기록이 다시 복원되지 않도록 함께 제거한다.
        if (PlayerPrefs.HasKey(LegacySaveKey))
        {
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
        }

        Save();
    }

    private void MigrateLegacyProgress(SaveData saveData)
    {
        NPCDialogueProgressDataList migratedData = null;

        if (PlayerPrefs.HasKey(LegacySaveKey))
        {
            string json = PlayerPrefs.GetString(LegacySaveKey, "");

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    migratedData =
                        JsonUtility.FromJson<NPCDialogueProgressDataList>(json);
                }
                catch (System.Exception exception)
                {
                    Debug.LogError(
                        "[NPCDialogueProgressManager] 기존 NPC 대화 진행도 " +
                        "변환 실패: " + exception.Message
                    );
                }
            }
        }

        saveData.npcDialogueProgressData =
            migratedData ??
            saveData.npcDialogueProgressData ??
            new NPCDialogueProgressDataList();

        saveData.npcDialogueProgressMigrationCompleted = true;

        if (SaveService.SaveCurrent() &&
            PlayerPrefs.HasKey(LegacySaveKey))
        {
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();

            Debug.Log(
                "[NPCDialogueProgressManager] 기존 전역 NPC 대화 진행도를 " +
                "현재 저장 슬롯으로 이전했습니다."
            );
        }
    }

    private void EnsureProgressLists()
    {
        if (progressData == null)
            progressData = new NPCDialogueProgressDataList();

        if (progressData.npcProgressList == null)
        {
            progressData.npcProgressList =
                new List<NPCDialogueNpcProgressData>();
        }
    }
}
