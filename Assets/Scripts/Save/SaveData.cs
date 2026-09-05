using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveInfo
{
    public string serverName;
    public string created;
    public string lastPlayed;
}

[Serializable]
public class Profile
{
    public string username;
    public List<SaveInfo> saves = new List<SaveInfo>();
}

[Serializable]
public class StarSaveData
{
    public int starlight;

    public DayReport todayReport;
    public DayReport yesterdayReport;
}

[Serializable]
public struct DayReport
{
    public int normalCount;
    public int normalStars;

    public int questCount;
    public int questStars;

    public List<string> soldDagwaKeys;

    public int TotalStars => normalStars + questStars;
}

[Serializable]
public class LevelSaveData
{
    public int level = 1;
    public int exp = 0;
}

[Serializable]
public class WorldTimeSaveData
{
    public int day = 1;
    public int hour = 9;
    public int minute = 0;
}

[Serializable]
public class PlaytimeSaveData
{
    public long seconds = 0;
    public string lastPlayed = "";
}

[Serializable]
public class UnlockProgressSaveData
{
    public List<int> pendingLevels =
        new List<int>();

    public List<int> appliedLevels =
        new List<int>();

    public bool initialized = false;
}

[Serializable]
public class EndingData
{
    public bool hasSeenEnding = false;
}

[Serializable]
public class PlayerLocationSaveData
{
    public string sceneName = "";
    public float positionX;
    public float positionY;
    public float facingX;
    public float facingY = -1f;
    public bool initialized;
}

[Serializable]
public class SaveData
{
    // 세이브 파일을 구분하는 마을 이름
    public string serverName;

    // 해당 세이브에서 사용하는 캐릭터 이름
    public string playerName;

    //플레이 시간
    public PlaytimeSaveData playtimeData;
    public bool playtimeMigrationCompleted;

    // ① 별빛(재화) // 이전 필드: 앞으로 사용하지 않음(리펙토링)
    public int starlight;

    // ② 날짜
    public int day;

    public WorldTimeSaveData worldTimeData;
    public bool worldTimeMigrationCompleted;

    // ③ 경험치/레벨
    public int exp;
    public int level;

    // ④ 나무 해금 현황
    public int currentUnlockedTreeLevel;

    // ⑤ 아이템 재고
    public List<StorageEntry> storageItems;
    public bool storageMigrationCompleted;

    // 제작대 진행 상태
    public MakerSaveData makerData;
    public bool makerMigrationCompleted;

    // 테이블 위 아이템 상태
    public TableSaveData tableData;
    public bool tableMigrationCompleted;

    // 농장 작물, 젖은 흙, 나무 성장 상태
    public FarmSaveData farmData;
    public bool farmMigrationCompleted;

    // 플레이어가 마지막으로 있던 씬, 위치, 방향
    public PlayerLocationSaveData playerLocationData;
    public bool playerLocationMigrationCompleted;

    // 이전 위치 필드: 마이그레이션 호환용
    public float playerPosX;
    public float playerPosY;
    public float moveDirX;
    public float moveDirY;

    public List<string> acceptedQuestIds;

    public string dailyQuestRealDate;
    public List<string> dailyQuestIds;

    public StarSaveData starData;

    //튜토리얼 상태 저장
    public TutorialStateData tutorialData;
    public bool tutorialMigrationCompleted;

    //계수나무 해금 저장
    public TreeUnlockData treeUnlockData;
    public bool treeUnlockMigrationCompleted;

    //레벨 별 다과 해금
    public UnlockProgressSaveData unlockProgressData;
    public bool unlockProgressMigrationCompleted;

    // 기존 별빛 JSON을 통합했는지 기록
    public bool starDataMigrationCompleted;

    //레벨 저장
    public LevelSaveData levelData;
    public bool levelDataMigrationCompleted;

    // 엔딩 완료 상태
    public EndingData endingData;
    public bool endingMigrationCompleted;

    // NPC 대화 진행도
    public NPCDialogueProgressDataList npcDialogueProgressData;
    public bool npcDialogueProgressMigrationCompleted;

    // 새 게임 생성 시 기본값
    public SaveData()
    {
        serverName = "";
        playerName = "";

        starlight = 0;
        day = 1;
        exp = 0;
        level = 1;
        currentUnlockedTreeLevel = 0;

        starData = new StarSaveData
        {
            starlight = 0
        };

        storageItems = new List<StorageEntry>();
        acceptedQuestIds = new List<string>();

        makerData = new MakerSaveData();
        tableData = new TableSaveData();
        farmData = new FarmSaveData();
        playerLocationData = new PlayerLocationSaveData();

        levelData = new LevelSaveData
        {
            level = 1,
            exp = 0
        };

        worldTimeData = new WorldTimeSaveData
        {
            day = 1,
            hour = 9,
            minute = 0
        };

        playtimeData = new PlaytimeSaveData
        {
            seconds = 0,
            lastPlayed = ""
        };

        dailyQuestRealDate = "";
        dailyQuestIds = new List<string>();

        tutorialData = new TutorialStateData
        {
            tutorialDone = false
        };

        treeUnlockData = new TreeUnlockData
        {
            currentUnlockedLevel = 0
        };

        unlockProgressData =
    new UnlockProgressSaveData
    {
        pendingLevels = new List<int>(),
        appliedLevels = new List<int> { 1 },
        initialized = true
    };

        endingData = new EndingData
        {
            hasSeenEnding = false
        };

        npcDialogueProgressData =
            new NPCDialogueProgressDataList();

        npcDialogueProgressMigrationCompleted = false;

        storageItems.Add(new StorageEntry
        {
            name = "Mepssalgaru",
            amount = 10
        });

        // 위치/방향 기본값
        playerPosX = 0f;
        playerPosY = 0f;
        moveDirX = 0f;
        moveDirY = 1f;
    }
}
