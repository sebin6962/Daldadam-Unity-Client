using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.Playables;

public class TreeLevelUnlocker : MonoBehaviour
{
    public static TreeLevelUnlocker Instance;

    public static int CurrentLevel = 0;

    public Button[] levelButtons;
    public TMP_Text[] levelDescTexts;
    public int[] starlightNeededForLevel;
    public string[] levelDescriptions;

    public GameObject tooltipPanel;
    public TMP_Text tooltipText;

    private int currentUnlockedLevel = 0;

    public GameObject notEnoughStarlightPanel;
    public CanvasGroup notEnoughStarlightGroup;

    public TMP_Text currentStateText;

    public GameObject unlockEffectPanel;
    public TMP_Text levelText;
    public TMP_Text effectText;
    public string[] unlockEffectDescriptions;

    private Coroutine notEnoughCoroutine = null;

    private TreeUnlockData unlockData;
    private string serverName;

    [Header("나무 해금 패널 UI")]
    public Image unlockPopupPanelImage;          // 나무 해금 팝업의 Image
    public Sprite lockedPanelSprite;             // 레벨 0(잠김)용
    public Sprite[] levelUnlockedPanelSprites;   // 레벨 1..N 해금용 (index = level-1)

    //해금 이펙트
    public GameObject unlockEffectPrefab;
    public float unlockEffectMinDuration = 2.0f;
    [Header("해금 7단계 맵 스프라이트 교체")]
    public SpriteRenderer mapSpriteRenderer;   // 교체 대상 (씬 배경 SpriteRenderer)
    public Sprite[] mapSpritesByLevel;

    [Header("해금 컷신 연출")]
    public CanvasGroup unlockUIPanelGroup;   // 나무 해금 UI 전체를 감싸는 CanvasGroup (클릭 차단용)
    public CanvasGroup cutsceneFadeGroup;    // 화면 전체를 덮는 검정/컷신용 CanvasGroup
    public float fadeInDuration = 1.0f;
    public float fadeOutDuration = 1.0f;

    public Camera targetCamera;
    public Transform cameraStartPoint;
    public Transform cameraEndPoint;
    public float cameraPanDuration = 2.5f;
    public AnimationCurve cameraPanCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool isPlayingUnlockSequence = false;
    private Vector3 originalCamPos;
    private float originalCamOrthoSize;

    [Header("해금 패널 숨김/표시")]
    public GameObject unlockPanelRoot; // 해금 UI 전체 루트(패널) 오브젝트

    [Header("카메라 투어(복귀 포함)")]
    public float cameraReturnDuration = 2.5f;

    [Header("파티클 종료 후 카메라 복귀")]
    [FormerlySerializedAs("gameplayReturnDelay")]
    [Min(0f)] public float particleEndCameraReturnDelay = 0.5f;

    public MonoBehaviour[] cameraControllersToDisable;

    public bool IsPlayingUnlockSequence
    {
        get { return isPlayingUnlockSequence; }
    }

    [Header("계수나무 오로라 연출")]
    public Transform treeEffectAnchor;  
    public GameObject auroraEffectPrefab;   
    public float auroraHoldDuration = 1.2f;  
    public float auroraFadeInDuration = 0.8f;  
    public float auroraStayDuration = 1.6f;     
    public float auroraFadeOutDuration = 0.9f;

    [Header("계수나무 해금 파티클")]
    public GameObject treeUnlockVFXRoot;    
    [Min(0f)] public float treeUnlockVFXPlayDelay = 0f;

    private Coroutine treeUnlockVFXPlayCoroutine;

    [Header("계수나무 스프라이트 페이드")]
    public float treeSpriteFadeDuration = 0.8f;
    public bool changeSpriteAtAuroraStart = true;

    [Header("Tree Shader Time Sync")]
    [SerializeField] private string motionTimeProperty = "_MotionTime";

    private float sharedTreeMotionTime = 0f;
    private MaterialPropertyBlock treeMotionBlock;

    [Header("엔딩 타임라인")]
    [SerializeField] private PlayableDirector endingTreeTimeline;

    private bool endingTimelinePlayed;

    [Header("엔딩 씬 전환")]
    [SerializeField] private string endingSceneName = "EndingScene";

    private bool isEndingSceneTransitioning;

    void Awake()
    {
        Instance = this;

        // 서버 선택값으로 경로 보장
        var serverName = PlayerPrefs.GetString("SelectedSave", string.Empty);
        if (!string.IsNullOrEmpty(serverName))
        {
            SetServerName(serverName);
        }
        else
        {
            Debug.LogWarning("[TreeLevelUnlocker] SelectedSave is empty. SetServerName() later before saving.");
        }
    }

    public void SetServerName(string serverName)
    {
        this.serverName = serverName ?? "";
    }

    public static int GetSavedCurrentLevel()
    {
        // TreeScene에 매니저가 있으면
        // 현재 런타임 해금값을 우선 사용
        if (Instance != null)
        {
            return Mathf.Max(
                0,
                Instance.currentUnlockedLevel
            );
        }

        string selectedServer =
            PlayerPrefs.GetString(
                "SelectedSave",
                ""
            );

        if (string.IsNullOrWhiteSpace(selectedServer))
        {
            Debug.LogWarning(
                "[TreeLevelUnlocker] 선택된 세이브가 없습니다."
            );

            return 0;
        }

        if (!SaveService.EnsureLoaded(selectedServer))
        {
            Debug.LogWarning(
                "[TreeLevelUnlocker] 계수나무 단계를 " +
                $"불러올 수 없습니다: {selectedServer}"
            );

            return 0;
        }

        TreeUnlockData savedData =
            SaveService.CurrentData.treeUnlockData;

        if (savedData == null)
        {
            return 0;
        }

        int savedLevel =
            Mathf.Max(
                0,
                savedData.currentUnlockedLevel
            );

        return savedLevel;
    }

    void Start()
    {
        // 컷신 시작 전에는 이전 재생 흔적이 남지 않도록 완전히 정지한다.
        StopAndClearTreeUnlockVFX();

        // 카메라 원위치 저장
        if (targetCamera != null)
        {
            originalCamPos = targetCamera.transform.position;
            originalCamOrthoSize = targetCamera.orthographicSize;
        }

        // 1) 데이터 로드(경로가 없으면 메모리만)
        LoadUnlockData();

        // 2) 동기화
        currentUnlockedLevel = unlockData != null ? unlockData.currentUnlockedLevel : 0;
        CurrentLevel = currentUnlockedLevel;

        UpdateLevelButtons();
        ApplyPanelSprite();
        ApplyMapSprite();

        // 4) 버튼들에 툴팁 트리거 연결
        if (levelButtons != null)
        {
            for (int i = 0; i < levelButtons.Length; i++)
            {
                int idx = i;
                EventTrigger trigger = levelButtons[i].gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = levelButtons[i].gameObject.AddComponent<EventTrigger>();

                var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entryEnter.callback.AddListener((_) => ShowTooltip(idx));
                trigger.triggers.Add(entryEnter);

                var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                entryExit.callback.AddListener((_) => HideTooltip());
                trigger.triggers.Add(entryExit);
            }
        }
    }

    private void Update()
    {
        sharedTreeMotionTime += Time.deltaTime;

        ApplySharedMotionTime(mapSpriteRenderer);
    }

    private void ApplySharedMotionTime(SpriteRenderer sr)
    {
        if (sr == null) return;

        if (treeMotionBlock == null)
            treeMotionBlock = new MaterialPropertyBlock();

        sr.GetPropertyBlock(treeMotionBlock);
        treeMotionBlock.SetFloat(motionTimeProperty, sharedTreeMotionTime);
        sr.SetPropertyBlock(treeMotionBlock);
    }

    private IEnumerator FadeToCurrentMapSprite()
    {
        if (mapSpriteRenderer == null)
        {
            Debug.LogWarning("[TreeLevelUnlocker] mapSpriteRenderer가 연결되지 않았습니다.");
            yield break;
        }

        if (currentUnlockedLevel <= 0) yield break;
        if (mapSpritesByLevel == null || mapSpritesByLevel.Length == 0) yield break;

        int idx = Mathf.Clamp(currentUnlockedLevel - 1, 0, mapSpritesByLevel.Length - 1);
        Sprite nextSprite = mapSpritesByLevel[idx];
        if (nextSprite == null) yield break;

        Sprite oldSprite = mapSpriteRenderer.sprite;
        if (oldSprite == nextSprite) yield break;

        Color baseColor = mapSpriteRenderer.color;
        float originalAlpha = baseColor.a;

        // 기존 스프라이트 복제용 오브젝트 생성
        GameObject oldSpriteObj = new GameObject("TreeOldSprite_Fade");
        oldSpriteObj.transform.SetParent(mapSpriteRenderer.transform.parent);
        oldSpriteObj.transform.position = mapSpriteRenderer.transform.position;
        oldSpriteObj.transform.rotation = mapSpriteRenderer.transform.rotation;
        oldSpriteObj.transform.localScale = mapSpriteRenderer.transform.localScale;

        SpriteRenderer oldRenderer = oldSpriteObj.AddComponent<SpriteRenderer>();
        oldRenderer.sprite = oldSprite;
        oldRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, originalAlpha);
        oldRenderer.sortingLayerID = mapSpriteRenderer.sortingLayerID;
        oldRenderer.sortingOrder = mapSpriteRenderer.sortingOrder + 1;

        // 중요: 기존 나무와 같은 셰이더/머티리얼 공유
        oldRenderer.sharedMaterial = mapSpriteRenderer.sharedMaterial;
        oldRenderer.flipX = mapSpriteRenderer.flipX;
        oldRenderer.flipY = mapSpriteRenderer.flipY;
        oldRenderer.maskInteraction = mapSpriteRenderer.maskInteraction;
        oldRenderer.drawMode = mapSpriteRenderer.drawMode;

        mapSpriteRenderer.sprite = nextSprite;
        mapSpriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        ApplySharedMotionTime(oldRenderer);
        ApplySharedMotionTime(mapSpriteRenderer);

        float t = 0f;
        while (t < treeSpriteFadeDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / treeSpriteFadeDuration);
            n = Mathf.SmoothStep(0f, 1f, n);

            ApplySharedMotionTime(oldRenderer);
            ApplySharedMotionTime(mapSpriteRenderer);

            Color oldColor = oldRenderer.color;
            oldColor.a = Mathf.Lerp(originalAlpha, 0f, n);
            oldRenderer.color = oldColor;

            Color newColor = mapSpriteRenderer.color;
            newColor.a = Mathf.Lerp(0f, originalAlpha, n);
            mapSpriteRenderer.color = newColor;

            yield return null;
        }

        mapSpriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, originalAlpha);

        if (oldSpriteObj != null)
            Destroy(oldSpriteObj);
    }

    private void SetCameraControllersEnabled(bool enable)
    {
        if (cameraControllersToDisable == null) return;
        foreach (var c in cameraControllersToDisable)
            if (c != null) c.enabled = enable;
    }

    public void ApplyPanelSprite()
    {
        if (unlockPopupPanelImage == null) return;

        if (currentUnlockedLevel <= 0)
        {
            if (lockedPanelSprite != null)
                unlockPopupPanelImage.sprite = lockedPanelSprite;
        }
        else
        {
            int idx = Mathf.Clamp(currentUnlockedLevel - 1, 0, levelUnlockedPanelSprites.Length - 1);
            if (levelUnlockedPanelSprites != null && levelUnlockedPanelSprites.Length > 0)
                unlockPopupPanelImage.sprite = levelUnlockedPanelSprites[idx];
        }

        // 필요 시 원본 크기 반영
        // unlockPopupPanelImage.SetNativeSize();
    }

    public void ApplyMapSprite()
    {
        if (mapSpriteRenderer == null)
        {
            Debug.LogWarning("[TreeLevelUnlocker] mapSpriteRenderer가 연결되지 않았습니다.");
            return;
        }

        // 기본 잠금 상태: index = -1
        if (currentUnlockedLevel <= 0)
        {
            return;
        }

        int idx = Mathf.Clamp(currentUnlockedLevel - 1, 0, mapSpritesByLevel.Length - 1);
        if (mapSpritesByLevel != null && mapSpritesByLevel.Length > 0 && mapSpritesByLevel[idx] != null)
        {
            mapSpriteRenderer.sprite = mapSpritesByLevel[idx];
        }
    }

    public void ShowTooltip(int levelIdx)
    {
        bool unlocked = levelIdx < currentUnlockedLevel;
        if (unlocked)
        {
            tooltipPanel.SetActive(false);
            return;
        }

        tooltipPanel.SetActive(true);
        tooltipText.text = $"{starlightNeededForLevel[levelIdx]}";

        RectTransform buttonRect = levelButtons[levelIdx].GetComponent<RectTransform>();
        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, buttonRect.position);

        RectTransform tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            tooltipRect.parent as RectTransform,
            screenPos,
            null,
            out localPoint
        );
        tooltipRect.anchoredPosition = localPoint + new Vector2(0, 80f);
    }

    public void HideTooltip()
    {
        tooltipPanel.SetActive(false);
    }

    public void TryUnlockLevel(int levelIdx)
    {
        if (levelIdx > currentUnlockedLevel) return;

        // StarDataManager가 서버 기반으로 로드되어 있어야 함
        if (StarDataManager.Instance == null)
        {
            Debug.LogError("[TreeLevelUnlocker] StarDataManager.Instance is null.");
            return;
        }

        int needStarlight = starlightNeededForLevel[levelIdx];
        int currentStarlight = StarDataManager.Instance.playerData.starlight;

        if (currentStarlight < needStarlight)
        {
            ShowNotEnoughStarlight();
            return;
        }

        StarDataManager.Instance.SpendStarlight(needStarlight);

        if (SFXManager.Instance) SFXManager.Instance.PlayTreeOpenSFX();

        currentUnlockedLevel = Mathf.Max(currentUnlockedLevel, levelIdx + 1);
        if (unlockData == null) unlockData = new TreeUnlockData();
        unlockData.currentUnlockedLevel = currentUnlockedLevel;
        CurrentLevel = currentUnlockedLevel;
        SaveUnlockData();

        UpdateLevelButtons();
        ApplyPanelSprite();

        StartCoroutine(UnlockCameraOnlySequence(levelIdx));

    }

    public void SaveUnlockData()
    {
        if (unlockData == null)
        {
            unlockData =
                new TreeUnlockData();
        }

        if (string.IsNullOrWhiteSpace(serverName))
        {
            Debug.LogError(
                "[TreeLevelUnlocker] serverName이 비어 있습니다. " +
                "SetServerName()을 먼저 호출해야 합니다."
            );

            return;
        }

        if (!SaveService.EnsureLoaded(serverName))
        {
            Debug.LogError(
                "[TreeLevelUnlocker] 통합 세이브를 " +
                $"준비할 수 없습니다: {serverName}"
            );

            return;
        }

        unlockData.currentUnlockedLevel =
            Mathf.Max(
                0,
                unlockData.currentUnlockedLevel
            );

        SaveService.CurrentData.treeUnlockData =
            unlockData;

        SaveService.CurrentData
            .treeUnlockMigrationCompleted = true;

        if (!SaveService.SaveCurrent())
        {
            Debug.LogError(
                "[TreeLevelUnlocker] 계수나무 단계 " +
                "저장에 실패했습니다."
            );
        }
    }

    public void LoadUnlockData()
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            serverName =
                PlayerPrefs.GetString(
                    "SelectedSave",
                    ""
                );
        }

        if (string.IsNullOrWhiteSpace(serverName))
        {
            Debug.LogWarning(
                "[TreeLevelUnlocker] 선택된 세이브가 없어 " +
                "기본 계수나무 데이터를 사용합니다."
            );

            unlockData =
                new TreeUnlockData();

            return;
        }

        if (!SaveService.EnsureLoaded(serverName))
        {
            Debug.LogWarning(
                "[TreeLevelUnlocker] 통합 세이브를 " +
                $"불러올 수 없습니다: {serverName}"
            );

            unlockData =
                new TreeUnlockData();

            return;
        }

        unlockData =
            SaveService.CurrentData.treeUnlockData;

        if (unlockData == null)
        {
            unlockData =
                new TreeUnlockData
                {
                    currentUnlockedLevel = 0
                };

            SaveService.CurrentData.treeUnlockData =
                unlockData;
        }

        unlockData.currentUnlockedLevel =
            Mathf.Max(
                0,
                unlockData.currentUnlockedLevel
            );
    }

    void UpdateLevelButtons()
    {
        if (levelButtons == null || levelDescTexts == null || levelDescriptions == null) return;

        for (int i = 0; i < levelButtons.Length; i++)
        {
            bool unlocked = i < currentUnlockedLevel;
            bool canUnlock = i == currentUnlockedLevel;
            levelButtons[i].interactable = canUnlock;

            if (unlocked)
                levelDescTexts[i].text = levelDescriptions[i];
            else
                levelDescTexts[i].text = "???";
        }

        if (currentStateText != null)
        {
            if (currentUnlockedLevel > 0)
                currentStateText.text = $"현재 상태: {levelDescriptions[currentUnlockedLevel - 1]}";
            else
                currentStateText.text = "현재 상태: 시들어 있는 계수나무";
        }
    }

    public void ShowNotEnoughStarlight()
    {
        if (notEnoughCoroutine != null)
            StopCoroutine(notEnoughCoroutine);

        notEnoughCoroutine = StartCoroutine(NotEnoughRoutine());
    }

    private IEnumerator NotEnoughRoutine()
    {
        notEnoughStarlightPanel.SetActive(true);

        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            notEnoughStarlightGroup.alpha = Mathf.Lerp(0, 1, elapsed / duration);
            yield return null;
        }
        notEnoughStarlightGroup.alpha = 1f;

        yield return new WaitForSeconds(1f);

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            notEnoughStarlightGroup.alpha = Mathf.Lerp(1, 0, elapsed / duration);
            yield return null;
        }
        notEnoughStarlightGroup.alpha = 0f;

        notEnoughStarlightPanel.SetActive(false);
        notEnoughCoroutine = null;
    }

    void ShowUnlockEffectPanel(int level)
    {
        int idx = level - 1;
        if (idx < 0 || unlockEffectDescriptions == null || idx >= unlockEffectDescriptions.Length) return;

        unlockEffectPanel.SetActive(true);
        levelText.text = $"{level}단계 해금";
        effectText.text = unlockEffectDescriptions[idx];
    }

    public void ClosePanel()
    {
        if (SFXManager.Instance) SFXManager.Instance.PlayBtnClickSFX();
        unlockEffectPanel.SetActive(false);
    }

    public void SetCurrentUnlockedLevel(int level)
    {
        currentUnlockedLevel = Mathf.Max(0, level);
        if (unlockData == null) unlockData = new TreeUnlockData();
        unlockData.currentUnlockedLevel = currentUnlockedLevel;
        CurrentLevel = currentUnlockedLevel;
        SaveUnlockData();
        UpdateLevelButtons();
        ApplyPanelSprite();
        //ApplyMapSprite();
    }

    //해금 이펙트
    private void PlayUnlockEffect(int levelIdx)
    {
        if (unlockEffectPrefab == null) return;
        if (levelButtons == null || levelIdx < 0 || levelIdx >= levelButtons.Length) return;

        var btn = levelButtons[levelIdx];
        if (btn == null) return;

        RectTransform btnRect = btn.GetComponent<RectTransform>();
        if (btnRect == null) return;

        //버튼 자식
        GameObject fx = Instantiate(unlockEffectPrefab, btnRect);
        var fxRect = fx.GetComponent<RectTransform>();
        if (fxRect != null)
        {
            fxRect.localScale = Vector3.one;
        }

        //클릭 방해
        var cg = fx.GetComponent<CanvasGroup>();
        if (cg == null) cg = fx.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        Destroy(fx, 5f);
    }

    private IEnumerator UnlockCameraOnlySequence(int levelIdx)
    {
        if (isPlayingUnlockSequence) yield break;
        isPlayingUnlockSequence = true;

        StopAndClearTreeUnlockVFX();

        if (targetCamera == null || cameraStartPoint == null || cameraEndPoint == null)
        {
            Debug.LogWarning("[TreeLevelUnlocker] Camera refs missing.");
            isPlayingUnlockSequence = false;
            yield break;
        }

        Vector3 clickCamPos = targetCamera.transform.position;
        float clickCamSize = targetCamera.orthographicSize;

        SetUnlockUIInteractable(false);

        yield return PlayUnlockEffectRoutine(levelIdx);

        SetCameraControllersEnabled(false);

        yield return FadeSimple(0f, 1f, fadeInDuration);

        SetUnlockUIVisible(false);

        Vector3 startPos = cameraStartPoint.position;
        startPos.z = clickCamPos.z;
        targetCamera.transform.position = startPos;
        targetCamera.orthographicSize = clickCamSize;

        yield return FadeSimple(1f, 0f, fadeOutDuration);

        Vector3 endPos = cameraEndPoint.position;
        endPos.z = clickCamPos.z;
        yield return CameraPanTo(endPos, cameraPanDuration);

        yield return PlayTreeAuroraSequence();

        if (particleEndCameraReturnDelay > 0f)
            yield return new WaitForSeconds(particleEndCameraReturnDelay);

        StopAndClearTreeUnlockVFX();

        bool isFinalLevel = levelIdx == levelButtons.Length - 1;

        if (isFinalLevel)
        {
            SetCameraControllersEnabled(true);

            PlayEndingTreeTimeline();

            isPlayingUnlockSequence = false;
            yield break;
        }

        // 아래부터는 마지막 단계가 아닐 때만 실행된다.

        yield return CameraPanTo(clickCamPos, cameraReturnDuration);

        yield return FadeSimple(0f, 1f, fadeInDuration);

        targetCamera.transform.position = clickCamPos;
        targetCamera.orthographicSize = clickCamSize;
        SetUnlockUIVisible(true);

        yield return FadeSimple(1f, 0f, fadeOutDuration);

        SetCameraControllersEnabled(true);

        SetUnlockUIInteractable(true);

        isPlayingUnlockSequence = false;
    }

    private IEnumerator PlayTreeAuroraSequence()
    {
        if (treeEffectAnchor == null)
        {
            yield return new WaitForSeconds(auroraHoldDuration);
            yield break;
        }

        // 카메라가 도착한 뒤 잠깐 멈춤
        if (auroraHoldDuration > 0f)
            yield return new WaitForSeconds(auroraHoldDuration);

        if (auroraEffectPrefab == null)
        {
            PlayTreeUnlockVFX();
            yield return new WaitForSeconds(auroraFadeInDuration + auroraStayDuration + auroraFadeOutDuration);
            yield return FadeToCurrentMapSprite();
            StopAndClearTreeUnlockVFX();
            yield break;
        }

        if (auroraHoldDuration > 0f)
            yield return new WaitForSeconds(auroraHoldDuration);

        PlayTreeUnlockVFX();

        GameObject fx = null;
        TreeAuroraEffect aura = null;

        if (auroraEffectPrefab != null)
        {
            fx = Instantiate(auroraEffectPrefab, treeEffectAnchor.position, Quaternion.identity);
            aura = fx.GetComponent<TreeAuroraEffect>();
        }

        if (changeSpriteAtAuroraStart)
        {
            // 오로라와 스프라이트 페이드를 동시에 진행
            Coroutine fadeRoutine = StartCoroutine(FadeToCurrentMapSprite());

            if (aura != null)
            {
                yield return aura.PlayRoutine(auroraFadeInDuration, auroraStayDuration, auroraFadeOutDuration);
            }
            else
            {
                yield return new WaitForSeconds(auroraFadeInDuration + auroraStayDuration + auroraFadeOutDuration);
            }

            yield return fadeRoutine;
        }
        else
        {
            // 먼저 스프라이트 바꾸고 그 다음 오로라
            yield return FadeToCurrentMapSprite();

            if (aura != null)
            {
                yield return aura.PlayRoutine(auroraFadeInDuration, auroraStayDuration, auroraFadeOutDuration);
            }
            else
            {
                yield return new WaitForSeconds(auroraFadeInDuration + auroraStayDuration + auroraFadeOutDuration);
            }
        }

        if (fx != null)
            Destroy(fx);

        StopAndClearTreeUnlockVFX();
    }

    private void PlayTreeUnlockVFX()
    {
        StopAndClearTreeUnlockVFX();

        if (treeUnlockVFXRoot == null)
            return;

        if (treeUnlockVFXPlayDelay > 0f)
        {
            treeUnlockVFXPlayCoroutine = StartCoroutine(PlayTreeUnlockVFXAfterDelay());
            return;
        }

        PlayTreeUnlockVFXNow();
    }

    private IEnumerator PlayTreeUnlockVFXAfterDelay()
    {
        yield return new WaitForSeconds(treeUnlockVFXPlayDelay);
        treeUnlockVFXPlayCoroutine = null;
        PlayTreeUnlockVFXNow();
    }

    private void PlayTreeUnlockVFXNow()
    {
        if (treeUnlockVFXRoot == null)
            return;

        treeUnlockVFXRoot.SetActive(true);

        ParticleSystem[] particleSystems =
            treeUnlockVFXRoot.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particle in particleSystems)
        {
            if (particle == null) continue;

            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Clear(false);
            particle.Play(false);
        }
    }

    private void StopAndClearTreeUnlockVFX()
    {
        if (treeUnlockVFXPlayCoroutine != null)
        {
            StopCoroutine(treeUnlockVFXPlayCoroutine);
            treeUnlockVFXPlayCoroutine = null;
        }

        if (treeUnlockVFXRoot == null)
            return;

        ParticleSystem[] particleSystems =
            treeUnlockVFXRoot.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particle in particleSystems)
        {
            if (particle == null) continue;

            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Clear(false);
        }
        if (treeUnlockVFXRoot != gameObject)
            treeUnlockVFXRoot.SetActive(false);
        else
            Debug.LogWarning("[TreeLevelUnlocker] Tree Unlock VFX Root에는 별도 오브젝트를 연결해야 합니다.");
    }

    private void OnDisable()
    {
        StopAndClearTreeUnlockVFX();
    }

    private IEnumerator PlayUnlockEffectRoutine(int levelIdx)
    {
        if (unlockEffectPrefab == null ||
            levelButtons == null ||
            levelIdx < 0 || levelIdx >= levelButtons.Length)
            yield break;

        var btn = levelButtons[levelIdx];
        if (btn == null) yield break;

        RectTransform btnRect = btn.GetComponent<RectTransform>();
        if (btnRect == null) yield break;

        GameObject fx = Instantiate(unlockEffectPrefab, btnRect);
        var fxRect = fx.GetComponent<RectTransform>();
        if (fxRect != null) fxRect.localScale = Vector3.one;

        var cg = fx.GetComponent<CanvasGroup>();
        if (cg == null) cg = fx.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        // 이펙트 길이 자동 추정 (Animator > ParticleSystem 순)
        float waitTime = 1.5f;
        var anim = fx.GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            yield return null;
            var st = anim.GetCurrentAnimatorStateInfo(0);
            waitTime = Mathf.Max(waitTime, st.length);
        }
        else
        {
            var ps = fx.GetComponentInChildren<ParticleSystem>();
            if (ps != null) waitTime = ps.main.duration + ps.main.startLifetime.constantMax;
        }

        yield return new WaitForSeconds(waitTime);

        Destroy(fx);
    }

    private void TeleportCameraToStart()
    {
        if (targetCamera == null || cameraStartPoint == null) return;

        Vector3 p = cameraStartPoint.position;
        p.z = targetCamera.transform.position.z; // z는 기존 유지
        targetCamera.transform.position = p;
    }

    private void RestoreCamera()
    {
        if (targetCamera == null) return;

        targetCamera.transform.position = originalCamPos;
        targetCamera.orthographicSize = originalCamOrthoSize;
    }

    private IEnumerator CameraTourRoutine()
    {
        if (targetCamera == null || cameraStartPoint == null || cameraEndPoint == null)
            yield break;

        // (1) start -> end
        Vector3 start = targetCamera.transform.position;
        Vector3 end = cameraEndPoint.position;
        end.z = start.z;

        float t = 0f;
        while (t < cameraPanDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / cameraPanDuration);
            float c = cameraPanCurve != null ? cameraPanCurve.Evaluate(n) : n;
            targetCamera.transform.position = Vector3.Lerp(start, end, c);
            yield return null;
        }
        targetCamera.transform.position = end;

        // (2) end -> original
        Vector3 original = originalCamPos;
        original.z = end.z;

        t = 0f;
        while (t < cameraReturnDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / cameraReturnDuration);
            float c = cameraPanCurve != null ? cameraPanCurve.Evaluate(n) : n;
            targetCamera.transform.position = Vector3.Lerp(end, original, c);
            yield return null;
        }
        targetCamera.transform.position = original;
    }

    private void SetUnlockUIVisible(bool visible)
    {
        if (unlockPanelRoot != null)
        {
            unlockPanelRoot.SetActive(visible);
            return;
        }

        // unlockPanelRoot 안 넣었을 때 fallback
        if (unlockUIPanelGroup != null)
            unlockUIPanelGroup.gameObject.SetActive(visible);
    }

    private IEnumerator FadeSimple(float from, float to, float duration)
    {
        if (cutsceneFadeGroup == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        cutsceneFadeGroup.gameObject.SetActive(true);
        cutsceneFadeGroup.blocksRaycasts = true;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            cutsceneFadeGroup.alpha = Mathf.Lerp(from, to, n);
            yield return null;
        }

        cutsceneFadeGroup.alpha = to;

        if (Mathf.Approximately(to, 0f))
        {
            cutsceneFadeGroup.blocksRaycasts = false;
            cutsceneFadeGroup.gameObject.SetActive(false);
        }
    }

    private IEnumerator CameraPanTo(Vector3 worldTarget, float duration)
    {
        if (targetCamera == null) yield break;

        Vector3 start = targetCamera.transform.position;
        Vector3 end = worldTarget;
        end.z = start.z;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            float c = cameraPanCurve != null ? cameraPanCurve.Evaluate(n) : n;

            targetCamera.transform.position = Vector3.Lerp(start, end, c);
            yield return null;
        }

        targetCamera.transform.position = end;
    }

    private void SetUnlockUIInteractable(bool enable)
    {
        if (unlockUIPanelGroup != null)
        {
            unlockUIPanelGroup.interactable = enable;
            unlockUIPanelGroup.blocksRaycasts = enable;
        }

        if (!enable)
        {
            foreach (var b in levelButtons)
                if (b != null) b.interactable = false;
        }
        else
        {
            UpdateLevelButtons();
        }
    }

    private void PlayEndingTreeTimeline()
    {
        if (endingTimelinePlayed)
            return;

        if (endingTreeTimeline == null)
        {
            Debug.LogWarning("[TreeLevelUnlocker] 엔딩 Timeline이 연결되지 않았습니다.");
            return;
        }

        endingTimelinePlayed = true;

        endingTreeTimeline.time = 0;
        endingTreeTimeline.Evaluate();
        endingTreeTimeline.Play();
    }

    /// <summary>
    /// 엔딩 Timeline 마지막 Signal에서 호출
    /// </summary>
    public void GoToEndingScene()
    {
        if (isEndingSceneTransitioning)
            return;

        if (FadeManager.Instance == null)
        {
            Debug.LogError(
                "[TreeLevelUnlocker] FadeManager.Instance가 없어 EndingScene으로 이동할 수 없습니다."
            );
            return;
        }

        isEndingSceneTransitioning = true;

        FadeManager.Instance.FadeToSceneForced(endingSceneName);
    }

}