using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

public class NPCDialogueUIManager : MonoBehaviour
{
    private const string LocalizationTable = "NPCDialogue";

    public static NPCDialogueUIManager Instance;

    private enum DialogueState
    {
        None,
        Line,
        Choice
    }

    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Transform optionParent;
    [SerializeField] private GameObject optionButtonPrefab;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text nextButtonText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject eKeyGuideImage;

    [Header("Portrait Open Animation")]
    [SerializeField] private float portraitStartDelay = 0.28f;
    [SerializeField] private float portraitStartOffsetY = -180f;
    [SerializeField] private float portraitPopDuration = 0.25f;
    [SerializeField] private float portraitOvershootY = 18f;

    [Header("Typing Effect")]
    [SerializeField] private float typingSpeed = 0.03f;
    [SerializeField] private float firstLineTypingStartDelay = 0.4f;

    private bool waitTypingDelayForNextLine = false;

    [Header("Next Button Move Effect")]
    [SerializeField] private float nextButtonMoveDistance = 8f;
    [SerializeField] private float nextButtonMoveDuration = 0.35f;
    [SerializeField] private bool hideNextButtonWhileTyping = false;

    private Vector2 defaultNextButtonPosition;

    [Header("Portrait Settings")]
    [SerializeField] private List<PortraitDisplaySetting> portraitSettings;

    private Vector3 defaultPortraitScale;
    private Vector2 defaultPortraitPosition;
    private Vector2 currentPortraitTargetPosition;

    private readonly List<GameObject> spawnedOptions = new();
    private readonly Queue<string> pendingLines = new();
    private readonly Dictionary<string, NPCDialogueNodeData> nodeDict = new();

    private NPCInteractable currentNpc;
    private NPCDialogueData currentDialogueData;
    private NPCDialogueNodeData currentNode;

    private DialogueState currentState = DialogueState.None;
    private string nextNodeAfterLines;

    private Coroutine typingCoroutine;
    private Coroutine nextButtonBlinkCoroutine;
    private Coroutine portraitOpenCoroutine;

    private bool isPortraitOpeningAnimation = false;
    private string currentFullLine = "";
    private bool isTyping = false;
    private string currentCategoryId = null;

    private string GetLocalizedText(string key, string fallback)
    {
        string localized = LocalizationSettings.StringDatabase.GetLocalizedString(
            LocalizationTable,
            key
        );

        if (string.IsNullOrEmpty(localized) ||
            localized.Contains("No translation found"))
        {
            localized = fallback ?? string.Empty;
        }

        if (NPCDialogueDatabase.Instance != null)
        {
            localized = NPCDialogueDatabase.Instance
                .ReplacePlayerNameTokenInText(localized);
        }

        return localized;
    }

    private string GetNpcNameKey()
    {
        return currentDialogueData == null
            ? string.Empty
            : $"npc.{currentDialogueData.npcId}.name";
    }

    private string GetNodeTextKey(
        NPCDialogueNodeData node,
        string textType,
        int index)
    {
        if (currentDialogueData == null || node == null)
            return string.Empty;

        return $"npc.{currentDialogueData.npcId}.node.{node.nodeId}.{textType}.{index + 1:00}";
    }

    // 튜토리얼용
    private System.Action tutorialDialogueFinishedCallback;
    private bool isTutorialDialogueMode = false;
    private readonly Queue<TutorialDialogueLine> tutorialLines = new();
    private TutorialDialogueLine currentTutorialLine;

    [System.Serializable]
    public class PortraitDisplaySetting
    {
        public string npcId;
        public Sprite portrait;
        public float scale = 1f;
        public Vector2 positionOffset;
    }

    public bool IsOpen()
    {
        return dialoguePanel != null && dialoguePanel.activeSelf;
    }

    public bool IsDialogueOpen
    {
        get
        {
            return dialoguePanel != null && dialoguePanel.activeSelf;
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        SetEKeyGuideVisible(false);

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnClickNextButton);

            RectTransform nextButtonRect =
                nextButton.GetComponent<RectTransform>();

            if (nextButtonRect != null)
                defaultNextButtonPosition =
                    nextButtonRect.anchoredPosition;
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClickCloseButton);
        }

        if (portraitImage != null)
        {
            defaultPortraitScale =
                portraitImage.transform.localScale;

            defaultPortraitPosition =
                portraitImage.rectTransform.anchoredPosition;

            currentPortraitTargetPosition =
                defaultPortraitPosition;
        }
    }

    private void Update()
    {
        if (dialoguePanel == null ||
            !dialoguePanel.activeSelf)
        {
            return;
        }

        // 초상화 등장 연출 중에는 입력 방지
        if (isPortraitOpeningAnimation)
            return;

        // 선택지가 표시된 상태
        if (currentState == DialogueState.Choice)
        {
            HandleChoiceNumberInput();
            return;
        }

        // 일반 대사가 아니면 입력하지 않음
        if (currentState != DialogueState.Line)
            return;

        if (Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            OnClickNextButton();
        }
    }

    private void HandleChoiceNumberInput()
    {
        // 숫자키 1~9까지만 사용
        int optionCount =
            Mathf.Min(spawnedOptions.Count, 9);

        for (int i = 0; i < optionCount; i++)
        {
            KeyCode numberKey =
                (KeyCode)((int)KeyCode.Alpha1 + i);

            KeyCode keypadKey =
                (KeyCode)((int)KeyCode.Keypad1 + i);

            if (!Input.GetKeyDown(numberKey) &&
                !Input.GetKeyDown(keypadKey))
            {
                continue;
            }

            GameObject optionObject =
                spawnedOptions[i];

            if (optionObject == null)
                return;

            Button optionButton =
                optionObject.GetComponent<Button>();

            if (optionButton != null &&
                optionButton.interactable)
            {
                // 마우스로 클릭했을 때와 같은 로직 실행
                optionButton.onClick.Invoke();
            }

            return;
        }
    }

    private void ApplyPortrait(Sprite portrait)
    {
        if (portraitImage == null)
            return;

        portraitImage.sprite = portrait;

        portraitImage.transform.localScale =
            defaultPortraitScale;

        portraitImage.rectTransform.anchoredPosition =
            defaultPortraitPosition;

        if (portrait != null && portraitSettings != null)
        {
            foreach (PortraitDisplaySetting setting
                     in portraitSettings)
            {
                if (setting == null)
                    continue;

                if (setting.portrait != portrait)
                    continue;

                portraitImage.transform.localScale =
                    defaultPortraitScale * setting.scale;

                portraitImage.rectTransform.anchoredPosition =
                    defaultPortraitPosition +
                    setting.positionOffset;

                break;
            }
        }

        currentPortraitTargetPosition =
            portraitImage.rectTransform.anchoredPosition;

        portraitImage.gameObject.SetActive(
            portrait != null
        );
    }

    private void OpenPanelWithPortraitAnimation(
        System.Action onFinished
    )
    {
        StopPortraitOpenAnimation();

        StopNextButtonBlink();
        SetNextButton(false, "다음");

        if (dialogueText != null)
        {
            dialogueText.text = "";
            dialogueText.maxVisibleCharacters = 0;
        }

        bool hasPortrait =
            portraitImage != null &&
            portraitImage.sprite != null;

        if (hasPortrait)
        {
            currentPortraitTargetPosition =
                portraitImage.rectTransform.anchoredPosition;

            portraitImage.rectTransform.anchoredPosition =
                currentPortraitTargetPosition +
                new Vector2(
                    0f,
                    portraitStartOffsetY
                );

            // 패널이 켜지는 순간 최종 위치의 초상화가
            // 잠깐 보이는 현상 방지
            portraitImage.gameObject.SetActive(false);
        }

        /*
         * dialoguePanel에 UIPanelPopAnimation을 붙여두면
         * SetActive(true) 시 OnEnable()에서 자동 재생됩니다.
         */
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        portraitOpenCoroutine = StartCoroutine(
            PortraitOpenCoroutine(
                hasPortrait,
                onFinished
            )
        );
    }

    private IEnumerator PortraitOpenCoroutine(
        bool hasPortrait,
        System.Action onFinished
    )
    {
        isPortraitOpeningAnimation = true;

        // 패널 팝업이 먼저 나온 뒤 초상화 등장
        float delayElapsed = 0f;

        while (delayElapsed < portraitStartDelay)
        {
            delayElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (hasPortrait && portraitImage != null)
        {
            Vector2 hiddenPosition =
                currentPortraitTargetPosition +
                new Vector2(
                    0f,
                    portraitStartOffsetY
                );

            Vector2 overshootPosition =
                currentPortraitTargetPosition +
                new Vector2(
                    0f,
                    portraitOvershootY
                );

            portraitImage.rectTransform.anchoredPosition =
                hiddenPosition;

            portraitImage.gameObject.SetActive(true);

            float duration =
                Mathf.Max(
                    0.01f,
                    portraitPopDuration
                );

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress =
                    Mathf.Clamp01(
                        elapsed / duration
                    );

                /*
                 * 처음 72%:
                 * 아래에서 목표보다 살짝 위까지 올라옴
                 */
                if (progress < 0.72f)
                {
                    float subProgress =
                        progress / 0.72f;

                    portraitImage
                        .rectTransform
                        .anchoredPosition =
                        Vector2.Lerp(
                            hiddenPosition,
                            overshootPosition,
                            EaseOutCubic(
                                subProgress
                            )
                        );
                }
                /*
                 * 나머지 28%:
                 * 오버슈트 위치에서 최종 위치로 내려옴
                 */
                else
                {
                    float subProgress =
                        (progress - 0.72f) /
                        0.28f;

                    portraitImage
                        .rectTransform
                        .anchoredPosition =
                        Vector2.Lerp(
                            overshootPosition,
                            currentPortraitTargetPosition,
                            EaseOutCubic(
                                subProgress
                            )
                        );
                }

                yield return null;
            }

            portraitImage.rectTransform.anchoredPosition =
                currentPortraitTargetPosition;
        }

        isPortraitOpeningAnimation = false;
        portraitOpenCoroutine = null;

        onFinished?.Invoke();
    }

    private void StopPortraitOpenAnimation()
    {
        if (portraitOpenCoroutine != null)
        {
            StopCoroutine(portraitOpenCoroutine);
            portraitOpenCoroutine = null;
        }

        isPortraitOpeningAnimation = false;

        if (portraitImage != null)
        {
            portraitImage.rectTransform.anchoredPosition =
                currentPortraitTargetPosition;
        }
    }

    private Sprite GetPortraitByNpcId(string npcId)
    {
        if (portraitSettings == null ||
            string.IsNullOrEmpty(npcId))
        {
            return null;
        }

        foreach (PortraitDisplaySetting setting
                 in portraitSettings)
        {
            if (setting != null &&
                setting.npcId == npcId)
            {
                return setting.portrait;
            }
        }

        return null;
    }

    public void OpenDialogue(NPCInteractable npc)
    {
        OpenDialogue(npc, null);
    }

    public void OpenDialogue(
        NPCInteractable npc,
        string categoryId
    )
    {
        if (npc == null)
            return;

        if (NPCDialogueDatabase.Instance == null)
            return;

        currentNpc = npc;
        currentCategoryId = categoryId;

        currentDialogueData =
            NPCDialogueDatabase.Instance
                .GetDialogueByNpcId(
                    npc.NpcId
                );

        if (currentDialogueData == null)
        {
            Debug.LogWarning(
                $"[NPCDialogueUIManager] " +
                $"npcId={npc.NpcId}의 " +
                $"대화 데이터가 없습니다."
            );

            CloseDialogue();
            return;
        }

        if (npcNameText != null)
        {
            string fallbackName =
                string.IsNullOrEmpty(currentDialogueData.npcName)
                    ? npc.NpcName
                    : currentDialogueData.npcName;

            npcNameText.text = GetLocalizedText(
                GetNpcNameKey(),
                fallbackName
            );
        }

        ApplyPortrait(
            GetPortraitByNpcId(
                currentDialogueData.npcId
            )
        );

        BuildNodeDictionary(
            currentDialogueData
        );

        waitTypingDelayForNextLine = true;

        ClearOptions();
        pendingLines.Clear();
        nextNodeAfterLines = null;

        string entryNodeId =
            GetEntryNodeId(
                currentDialogueData,
                currentCategoryId
            );

        if (string.IsNullOrEmpty(entryNodeId))
        {
            Debug.LogWarning(
                $"[NPCDialogueUIManager] " +
                $"npcId={npc.NpcId}의 " +
                $"시작 노드를 찾을 수 없습니다."
            );

            CloseDialogue();
            return;
        }

        if (BGMPlayer.Instance != null)
        {
            BGMPlayer.Instance
                .StartDialogueBGM();
        }

        OpenPanelWithPortraitAnimation(() =>
        {
            MoveToNode(entryNodeId);
        });
    }

    public void OpenTutorialDialogue(
        List<TutorialDialogueLine> lines,
        System.Action onFinished = null
    )
    {
        if (lines == null ||
            lines.Count == 0)
        {
            onFinished?.Invoke();
            return;
        }

        isTutorialDialogueMode = true;

        tutorialDialogueFinishedCallback =
            onFinished;

        tutorialLines.Clear();

        foreach (TutorialDialogueLine line in lines)
            tutorialLines.Enqueue(line);

        ClearOptions();
        pendingLines.Clear();
        nextNodeAfterLines = null;

        currentState = DialogueState.Line;
        waitTypingDelayForNextLine = true;

        ShowNextTutorialLine(true);
    }

    private void ShowNextTutorialLine(
        bool playOpenAnimation = false
    )
    {
        if (tutorialLines.Count == 0)
        {
            CloseTutorialDialogue();
            return;
        }

        currentTutorialLine =
            tutorialLines.Dequeue();

        if (npcNameText != null)
        {
            npcNameText.text =
                currentTutorialLine.speakerName;
        }

        ApplyPortrait(
            currentTutorialLine.portrait
        );

        if (playOpenAnimation)
        {
            OpenPanelWithPortraitAnimation(() =>
            {
                StartTyping(
                    currentTutorialLine.dialogue
                );
            });
        }
        else
        {
            StartTyping(
                currentTutorialLine.dialogue
            );
        }
    }

    private void BuildNodeDictionary(
        NPCDialogueData data
    )
    {
        nodeDict.Clear();

        if (data == null ||
            data.nodes == null)
        {
            return;
        }

        for (int i = 0;
             i < data.nodes.Count;
             i++)
        {
            NPCDialogueNodeData node =
                data.nodes[i];

            if (node == null ||
                string.IsNullOrEmpty(
                    node.nodeId
                ))
            {
                continue;
            }

            nodeDict[node.nodeId] = node;
        }
    }

    private string GetEntryNodeId(
        NPCDialogueData data,
        string categoryId
    )
    {
        if (data == null)
            return null;

        NPCDialogueNpcProgressData npcProgress =
            null;

        if (NPCDialogueProgressManager.Instance != null)
        {
            npcProgress =
                NPCDialogueProgressManager
                    .Instance
                    .GetOrCreateNpcProgress(
                        data.npcId
                    );
        }

        Debug.Log(
    "[NPCDialogue] 시작 노드 선택 / " +
    $"NPC={data.npcId}, " +
    $"category={categoryId ?? "(기본)"}, " +
    $"hasMetNpc={npcProgress?.hasMetNpc}, " +
    $"treeLevel=" +
    TreeLevelUnlocker.GetSavedCurrentLevel()
);

        string entryNodeId =
            NPCDialogueSelector.GetStartNodeId(
                data,
                npcProgress,
                categoryId
            );

        Debug.Log(
    "[NPCDialogue] 선택 결과 / " +
    $"nodeId={entryNodeId}"
);

        if (NPCDialogueProgressManager.Instance != null)
        {
            NPCDialogueProgressManager
                .Instance
                .Save();
        }

        return entryNodeId;
    }

    private void MoveToNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            CloseDialogue();
            return;
        }

        if (!nodeDict.TryGetValue(
                nodeId,
                out currentNode
            ) ||
            currentNode == null)
        {
            Debug.LogWarning(
                $"[NPCDialogueUIManager] " +
                $"nodeId={nodeId}를 " +
                $"찾을 수 없습니다."
            );

            CloseDialogue();
            return;
        }

        string nodeType =
            currentNode.type?
                .Trim()
                .ToLower();

        switch (nodeType)
        {
            case "line":
                StartLineNode(currentNode);
                break;

            case "choice":
                StartChoiceNode(currentNode);
                break;

            case "end":
                CloseDialogue();
                break;

            default:
                Debug.LogWarning(
                    $"[NPCDialogueUIManager] " +
                    $"알 수 없는 node type: " +
                    $"{currentNode.type}"
                );

                CloseDialogue();
                break;
        }
    }

    private void StartLineNode(
        NPCDialogueNodeData node
    )
    {
        ClearOptions();
        pendingLines.Clear();

        if (node.lines != null)
        {
            for (int i = 0;
                 i < node.lines.Count;
                 i++)
            {
                string line = GetLocalizedText(
                    GetNodeTextKey(node, "line", i),
                    node.lines[i]
                );

                if (!string.IsNullOrWhiteSpace(line))
                    pendingLines.Enqueue(line);
            }
        }

        nextNodeAfterLines =
            node.nextNodeId;

        currentState =
            DialogueState.Line;

        if (pendingLines.Count > 0)
            ShowNextLine();
        else
            MoveToNode(nextNodeAfterLines);
    }

    private void StartChoiceNode(
        NPCDialogueNodeData node
    )
    {
        ClearOptions();
        pendingLines.Clear();
        nextNodeAfterLines = null;

        StopTypingImmediately();
        StopNextButtonBlink();

        currentState =
            DialogueState.Choice;

        // 선택지가 표시될 때만 E키 안내 숨김
        SetEKeyGuideVisible(false);
        SetNextButton(false, "다음");

        if (node.options == null ||
            node.options.Count == 0)
        {
            CloseDialogue();
            return;
        }

        int createdOptionCount = 0;

        for (int i = 0;
             i < node.options.Count;
             i++)
        {
            NPCDialogueChoiceOptionData option =
                node.options[i];

            if (option == null)
                continue;

            if (!ShouldShowOption(option))
                continue;

            createdOptionCount++;

            CreateOption(
                GetLocalizedText(
                    GetNodeTextKey(node, "option", i),
                    option.text
                ),
                () =>
                {
                    HandleOptionSelected(option);
                }
            );
        }

        if (createdOptionCount == 0)
            CloseDialogue();
    }

    private bool ShouldShowOption(
        NPCDialogueChoiceOptionData option
    )
    {
        if (option == null)
            return false;

        bool hasQuestIdCondition =
            !string.IsNullOrEmpty(
                option.requiredQuestId
            );

        bool hasQuestTargetNpcCondition =
            !string.IsNullOrEmpty(
                option.requiredQuestTargetNpcId
            );

        if (!hasQuestIdCondition &&
            !hasQuestTargetNpcCondition)
        {
            return true;
        }

        if (QuestAcceptManager.Instance == null)
            return false;

        if (hasQuestIdCondition)
        {
            return QuestAcceptManager
                .Instance
                .IsAccepted(
                    option.requiredQuestId
                );
        }

        if (hasQuestTargetNpcCondition)
        {
            QuestData talkQuest =
                QuestAcceptManager
                    .Instance
                    .GetAcceptedTalkQuestForNpc(
                        option.requiredQuestTargetNpcId
                    );

            return talkQuest != null;
        }

        return true;
    }

    private void HandleOptionSelected(
        NPCDialogueChoiceOptionData option
    )
    {
        if (option == null)
            return;

        if (option.completeTalkQuestOnSelect &&
            QuestAcceptManager.Instance != null &&
            currentNpc != null)
        {
            QuestData talkQuest =
                QuestAcceptManager
                    .Instance
                    .GetAcceptedTalkQuestForNpc(
                        currentNpc.NpcId
                    );

            if (talkQuest != null)
            {
                QuestAcceptManager
                    .Instance
                    .CompleteAcceptedTalkQuest(
                        talkQuest.id
                    );
            }
        }

        if (option.openRandomSetFromCategory &&
            !string.IsNullOrEmpty(
                option.targetCategoryId
            ))
        {
            currentCategoryId =
                option.targetCategoryId;

            string categoryEntryNodeId =
                GetEntryNodeId(
                    currentDialogueData,
                    currentCategoryId
                );

            if (string.IsNullOrEmpty(
                    categoryEntryNodeId
                ))
            {
                Debug.LogWarning(
                    $"[NPCDialogueUIManager] " +
                    $"categoryId=" +
                    $"{currentCategoryId}의 " +
                    $"시작 노드를 찾을 수 없습니다."
                );

                CloseDialogue();
                return;
            }

            MoveToNode(categoryEntryNodeId);
            return;
        }

        MoveToNode(option.nextNodeId);
    }

    private void OnClickNextButton()
    {
        if (currentState != DialogueState.Line)
            return;

        if (isPortraitOpeningAnimation)
            return;

        if (isTyping)
        {
            CompleteCurrentTyping();
            return;
        }

        if (isTutorialDialogueMode)
        {
            ShowNextTutorialLine();
            return;
        }

        ShowNextLine();
    }

    public void OnClickCloseButton()
    {
        if (dialoguePanel == null ||
            !dialoguePanel.activeSelf)
        {
            return;
        }

        // 튜토리얼 대화일 경우
        if (isTutorialDialogueMode)
        {
            CloseTutorialDialogue();
            return;
        }

        // 일반 NPC 대화일 경우
        CloseDialogue();
    }

    private void CloseTutorialDialogue()
    {
        StopPortraitOpenAnimation();

        StopTypingImmediately();
        StopNextButtonBlink();
        SetEKeyGuideVisible(false);

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        tutorialLines.Clear();
        isTutorialDialogueMode = false;

        System.Action callback =
            tutorialDialogueFinishedCallback;

        tutorialDialogueFinishedCallback = null;

        waitTypingDelayForNextLine = false;
        currentState = DialogueState.None;

        callback?.Invoke();
    }

    private void ShowNextLine()
    {
        if (pendingLines.Count > 0)
        {
            string line =
                pendingLines.Dequeue();

            StartTyping(line);
            return;
        }

        MoveToNode(nextNodeAfterLines);
    }

    private void StartTyping(string line)
    {
        StopTypingImmediately();
        StopNextButtonBlink();
        SetEKeyGuideVisible(false);

        currentFullLine = line;

        if (dialogueText == null)
            return;

        dialogueText.text =
            currentFullLine;

        dialogueText.maxVisibleCharacters = 0;

        if (hideNextButtonWhileTyping)
        {
            SetNextButton(false, "다음");
        }
        else
        {
            SetNextButton(true, "다음");

            if (nextButton != null)
                nextButton.interactable = false;
        }

        SetEKeyGuideVisible(true);

        float startDelay =
            waitTypingDelayForNextLine
                ? firstLineTypingStartDelay
                : 0f;

        waitTypingDelayForNextLine = false;

        typingCoroutine = StartCoroutine(
            TypeLineCoroutine(startDelay)
        );
    }

    private IEnumerator TypeLineCoroutine(
        float startDelay
    )
    {
        isTyping = true;

        if (startDelay > 0f)
        {
            float delayElapsed = 0f;

            while (delayElapsed < startDelay)
            {
                delayElapsed +=
                    Time.unscaledDeltaTime;

                yield return null;
            }
        }

        dialogueText.ForceMeshUpdate();

        int totalVisibleCount =
            dialogueText
                .textInfo
                .characterCount;

        for (int i = 0;
             i <= totalVisibleCount;
             i++)
        {
            dialogueText.maxVisibleCharacters = i;

            float characterDelay = 0f;

            while (characterDelay < typingSpeed)
            {
                characterDelay +=
                    Time.unscaledDeltaTime;

                yield return null;
            }
        }

        dialogueText.maxVisibleCharacters =
            totalVisibleCount;

        isTyping = false;
        typingCoroutine = null;

        ActivateAndBlinkNextButton();
    }

    private void CompleteCurrentTyping()
    {
        if (dialogueText == null)
            return;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        dialogueText.text =
            currentFullLine;

        dialogueText.ForceMeshUpdate();

        dialogueText.maxVisibleCharacters =
            dialogueText
                .textInfo
                .characterCount;

        isTyping = false;

        ActivateAndBlinkNextButton();
    }

    private void ActivateAndBlinkNextButton()
    {
        SetNextButton(true, "다음");

        if (nextButton != null)
            nextButton.interactable = true;

        SetEKeyGuideVisible(true);
        StartNextButtonBlink();
    }

    private void StartNextButtonBlink()
    {
        StopNextButtonBlink();

        if (nextButton == null)
            return;

        nextButtonBlinkCoroutine =
            StartCoroutine(
                BlinkNextButtonCoroutine()
            );
    }

    private void StopNextButtonBlink()
    {
        if (nextButtonBlinkCoroutine != null)
        {
            StopCoroutine(
                nextButtonBlinkCoroutine
            );

            nextButtonBlinkCoroutine = null;
        }

        if (nextButton == null)
            return;

        nextButton.gameObject.SetActive(true);

        RectTransform rectTransform =
            nextButton.GetComponent<RectTransform>();

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition =
                defaultNextButtonPosition;
        }

        Image image =
            nextButton.GetComponent<Image>();

        if (image != null)
        {
            Color color = image.color;
            color.a = 1f;
            image.color = color;
        }
    }

    private IEnumerator BlinkNextButtonCoroutine()
    {
        if (nextButton == null)
            yield break;

        RectTransform rectTransform =
            nextButton.GetComponent<RectTransform>();

        if (rectTransform == null)
            yield break;

        Vector2 upPosition =
            defaultNextButtonPosition;

        Vector2 downPosition =
            defaultNextButtonPosition +
            new Vector2(
                0f,
                -nextButtonMoveDistance
            );

        while (true)
        {
            float elapsed = 0f;

            while (elapsed <
                   nextButtonMoveDuration)
            {
                elapsed +=
                    Time.unscaledDeltaTime;

                float progress =
                    Mathf.Clamp01(
                        elapsed /
                        nextButtonMoveDuration
                    );

                float eased =
                    EaseInOutSine(progress);

                rectTransform.anchoredPosition =
                    Vector2.Lerp(
                        upPosition,
                        downPosition,
                        eased
                    );

                yield return null;
            }

            rectTransform.anchoredPosition =
                downPosition;

            elapsed = 0f;

            while (elapsed <
                   nextButtonMoveDuration)
            {
                elapsed +=
                    Time.unscaledDeltaTime;

                float progress =
                    Mathf.Clamp01(
                        elapsed /
                        nextButtonMoveDuration
                    );

                float eased =
                    EaseInOutSine(progress);

                rectTransform.anchoredPosition =
                    Vector2.Lerp(
                        downPosition,
                        upPosition,
                        eased
                    );

                yield return null;
            }

            rectTransform.anchoredPosition =
                upPosition;
        }
    }

    private float EaseOutCubic(float value)
    {
        return 1f -
               Mathf.Pow(
                   1f - value,
                   3f
               );
    }

    private float EaseInOutSine(float value)
    {
        return -(
            Mathf.Cos(
                Mathf.PI * value
            ) - 1f
        ) / 2f;
    }

    private void StopTypingImmediately()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;

        if (dialogueText != null)
        {
            dialogueText.maxVisibleCharacters =
                int.MaxValue;
        }
    }

    private void CreateOption(
    string text,
    UnityEngine.Events.UnityAction callback
)
    {
        if (optionButtonPrefab == null ||
            optionParent == null)
        {
            return;
        }

        GameObject optionObject =
            Instantiate(
                optionButtonPrefab,
                optionParent,
                false
            );

        spawnedOptions.Add(optionObject);

        // 화면에 표시되는 순서대로 1, 2, 3...
        int optionNumber = spawnedOptions.Count;

        RectTransform rectTransform =
            optionObject.GetComponent<RectTransform>();

        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        Button button =
            optionObject.GetComponent<Button>();

        // 선택지 문장
        Transform choiceTextTransform =
            optionObject.transform.Find("ChoiceText");

        if (choiceTextTransform != null)
        {
            TMP_Text choiceText =
                choiceTextTransform.GetComponent<TMP_Text>();

            if (choiceText != null)
                choiceText.text = text;
        }
        else
        {
            Debug.LogWarning(
                "[NPCDialogueUIManager] " +
                "선택지 프리팹에서 ChoiceText를 찾지 못했습니다.",
                optionObject
            );
        }

        // 오른쪽 숫자 안내 텍스트
        Transform numberTextTransform =
            optionObject.transform.Find(
                "NumberGuide/NumberText"
            );

        if (numberTextTransform != null)
        {
            TMP_Text numberText =
                numberTextTransform.GetComponent<TMP_Text>();

            if (numberText != null)
                numberText.text = optionNumber.ToString();
        }
        else
        {
            Debug.LogWarning(
                "[NPCDialogueUIManager] " +
                "선택지 프리팹에서 " +
                "NumberGuide/NumberText를 찾지 못했습니다.",
                optionObject
            );
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            optionParent as RectTransform
        );
    }

    private void ClearOptions()
    {
        for (int i = spawnedOptions.Count - 1;
             i >= 0;
             i--)
        {
            if (spawnedOptions[i] != null)
                Destroy(spawnedOptions[i]);
        }

        spawnedOptions.Clear();
    }

    private void SetNextButton(
        bool visible,
        string text
    )
    {
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(
                visible
            );
        }

        if (nextButtonText != null)
            nextButtonText.text = text == "다음"
                ? GetLocalizedText("npc.ui.next", text)
                : text;
    }

    private void SetEKeyGuideVisible(bool visible)
    {
        if (eKeyGuideImage != null)
            eKeyGuideImage.SetActive(visible);
    }

    public void CloseDialogue()
    {
        if (BGMPlayer.Instance != null)
        {
            BGMPlayer.Instance
                .StopDialogueBGM();
        }

        StopPortraitOpenAnimation();

        ClearOptions();
        pendingLines.Clear();
        nodeDict.Clear();

        nextNodeAfterLines = null;

        StopTypingImmediately();
        StopNextButtonBlink();
        SetEKeyGuideVisible(false);

        currentFullLine = "";
        currentCategoryId = null;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (currentNpc != null)
            currentNpc.EndDialogue();

        currentNpc = null;
        currentDialogueData = null;
        currentNode = null;
        currentState = DialogueState.None;

        waitTypingDelayForNextLine = false;
    }
}
