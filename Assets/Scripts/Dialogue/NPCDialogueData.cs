using System;
using System.Collections.Generic;

[Serializable]
public class NPCDialogueChoiceOptionData
{
    public string text;
    public string nextNodeId;

    // 이 선택지를 보여줄 조건
    public string requiredQuestId;
    public string requiredQuestTargetNpcId;

    // 이 선택지를 고르면 현재 NPC 관련 Talk 퀘스트를 완료 처리할지
    public bool completeTalkQuestOnSelect;

    // 특정 카테고리의 랜덤 세트를 열기 위한 옵션
    public string targetCategoryId;
    public bool openRandomSetFromCategory;
}

[Serializable]
public class NPCDialogueNodeData
{
    public string nodeId;
    public string type; // "line", "choice", "end"

    public List<string> lines;
    public List<NPCDialogueChoiceOptionData> options;

    public string nextNodeId;
}

[Serializable]
public class NPCDialogueCategoryData
{
    public string categoryId;              // 예: daily_talk
    public string categoryName;            // 예: 일상 대화
    public bool useNonRepeatingRandom = true;
    public List<string> setIds = new List<string>();
}

[Serializable]
public class NPCDialogueSetData
{
    public string setId;                   // 예: yellow_daily_01
    public string title;                   // 예: 안부
    public string categoryId;              // 예: daily_talk
    public string startNodeId;             // 실제 시작 노드

    // true인 경우 계수나무 해금 단계에 따라 대화를 제한
    public bool useTreeLevel;
    public int treeLevel;
}

[Serializable]
public class NPCDialogueData
{
    public string npcId;
    public string npcName;

    // 첫 만남 대화 이후 계수나무 해금 단계에 맞는 대화 세트를
    // 곧바로 선택하려면 true
    public bool useTreeLevelDialogue;

    // NPC와 첫 상호작용일 때만 실행할 전용 시작 노드
    public string firstInteractionStartNodeId;

    // 구버전 fallback
    public string startNodeId;
    public List<string> randomGreetingNodeIds = new List<string>();

    // 신버전
    public string defaultCategoryId;
    public List<NPCDialogueCategoryData> categories = new List<NPCDialogueCategoryData>();
    public List<NPCDialogueSetData> dialogueSets = new List<NPCDialogueSetData>();

    public List<NPCDialogueNodeData> nodes = new List<NPCDialogueNodeData>();
}

[Serializable]
public class NPCDialogueDataList
{
    public List<NPCDialogueData> npcs = new List<NPCDialogueData>();
}

[Serializable]
public class NPCDialogueCategoryProgressData
{
    public string categoryId;
    public List<string> seenSetIds = new List<string>();
}

[Serializable]
public class NPCDialogueNpcProgressData
{
    public string npcId;

    // NPC와 한 번이라도 상호작용했는지
    public bool hasMetNpc;

    public List<NPCDialogueCategoryProgressData> categoryProgressList = new List<NPCDialogueCategoryProgressData>();
}

[Serializable]
public class NPCDialogueProgressDataList
{
    public List<NPCDialogueNpcProgressData> npcProgressList = new List<NPCDialogueNpcProgressData>();
}