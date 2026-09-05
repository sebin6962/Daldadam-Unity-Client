using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class NPCDialogueDatabase : MonoBehaviour
{
    public static NPCDialogueDatabase Instance;

    [SerializeField] private string jsonFileName = "NPCDialogueData";

    private Dictionary<string, NPCDialogueData> dialogueDict = new();
    private string loadedServerName = "";
    private string loadedPlayerName = "";

    private const string PlayerNameToken = "(플레이어 이름)";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadDialogueData(PlayerPrefs.GetString("SelectedSave", ""));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadDialogueData(string serverName)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(jsonFileName);

        if (jsonFile == null)
        {
            Debug.LogError($"[NPCDialogueDatabase] Resources/{jsonFileName}.json 파일을 찾을 수 없습니다.");
            return;
        }

        NPCDialogueDataList dataList = JsonUtility.FromJson<NPCDialogueDataList>(jsonFile.text);

        if (dataList == null || dataList.npcs == null)
        {
            Debug.LogError("[NPCDialogueDatabase] NPC 대화 JSON 파싱 실패");
            return;
        }

        string playerName = LoadPlayerName(serverName);
        loadedPlayerName = playerName;
        ReplacePlayerNameTokens(dataList, playerName);

        dialogueDict.Clear();

        foreach (var npc in dataList.npcs)
        {
            if (npc == null || string.IsNullOrEmpty(npc.npcId))
                continue;

            dialogueDict[npc.npcId] = npc;
        }

        loadedServerName = serverName ?? "";

        Debug.Log(
            $"[NPCDialogueDatabase] NPC 대화 {dialogueDict.Count}개 로드 완료" +
            (string.IsNullOrEmpty(playerName) ? "" : $" / 플레이어 이름: {playerName}")
        );
    }

    public NPCDialogueData GetDialogueByNpcId(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return null;

        EnsureSelectedSaveLoaded();

        dialogueDict.TryGetValue(npcId, out var data);
        return data;
    }

    public string ReplacePlayerNameTokenInText(string text)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(loadedPlayerName))
            return text;

        return text.Replace(PlayerNameToken, loadedPlayerName);
    }

    /// <summary>
    /// 세이브 슬롯을 바꿔도 DontDestroyOnLoad로 남아 있는 대화 데이터가
    /// 이전 슬롯의 플레이어 이름을 계속 사용하지 않도록 다시 불러온다.
    /// </summary>
    private void EnsureSelectedSaveLoaded()
    {
        string selectedServerName = PlayerPrefs.GetString("SelectedSave", "");

        if (loadedServerName == selectedServerName)
            return;

        LoadDialogueData(selectedServerName);
    }

    private string LoadPlayerName(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            return "";

        string savePath = Path.Combine(
            Application.persistentDataPath,
            $"save_myuser_{serverName}.json"
        );

        if (!File.Exists(savePath))
        {
            Debug.LogWarning(
                $"[NPCDialogueDatabase] 플레이어 이름 저장 파일을 찾을 수 없습니다: {savePath}"
            );
            return "";
        }

        try
        {
            SaveData saveData = JsonUtility.FromJson<SaveData>(
                File.ReadAllText(savePath)
            );

            if (saveData == null || string.IsNullOrWhiteSpace(saveData.playerName))
            {
                Debug.LogWarning(
                    $"[NPCDialogueDatabase] [{serverName}] 세이브에 플레이어 이름이 없습니다."
                );
                return "";
            }

            return saveData.playerName.Trim();
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                $"[NPCDialogueDatabase] 플레이어 이름 불러오기 실패: {e.Message}"
            );
            return "";
        }
    }

    private void ReplacePlayerNameTokens(
        NPCDialogueDataList dataList,
        string playerName
    )
    {
        // 이름이 없는 구버전 세이브라면 토큰을 그대로 두어
        // 저장 데이터 문제를 눈으로 확인할 수 있게 한다.
        if (string.IsNullOrEmpty(playerName))
            return;

        foreach (NPCDialogueData npc in dataList.npcs)
        {
            if (npc == null || npc.nodes == null)
                continue;

            foreach (NPCDialogueNodeData node in npc.nodes)
            {
                if (node == null)
                    continue;

                if (node.lines != null)
                {
                    for (int i = 0; i < node.lines.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(node.lines[i]))
                        {
                            node.lines[i] =
                                node.lines[i].Replace(PlayerNameToken, playerName);
                        }
                    }
                }

                if (node.options == null)
                    continue;

                foreach (NPCDialogueChoiceOptionData option in node.options)
                {
                    if (option != null && !string.IsNullOrEmpty(option.text))
                    {
                        option.text =
                            option.text.Replace(PlayerNameToken, playerName);
                    }
                }
            }
        }
    }
}
