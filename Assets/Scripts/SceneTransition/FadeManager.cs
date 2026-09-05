
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.EventSystems;

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance;

    [Header("UI 이미지 (검은 패널)")]
    public Image fadeImage;

    [Header("페이드 시간 (초)")]
    public float fadeDuration = 1f;

    private bool isFading = false;

    public bool IsFading => isFading;

    Canvas overlayCanvas;
    CanvasGroup fadeGroup;

    [Header("로딩 직후 블랙 유지(초)")]
    [SerializeField] float preRevealHoldSeconds = 0.12f;
    
    //손님 저장 씬
    [SerializeField] private string playerStoreSceneName = "PlayerStoreScene";

    private void Awake()
    {
        EnsureOverlay();

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject); // 중복 방지
        }
    }

    void EnsureOverlay()
    {
        if (fadeImage == null) return;

        overlayCanvas = fadeImage.GetComponentInParent<Canvas>();
        if (overlayCanvas == null)
            overlayCanvas = fadeImage.gameObject.AddComponent<Canvas>();

        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 32767; // 최상단

        if (overlayCanvas.GetComponent<GraphicRaycaster>() == null)
            overlayCanvas.gameObject.AddComponent<GraphicRaycaster>();

        fadeGroup = fadeImage.GetComponent<CanvasGroup>();
        if (fadeGroup == null) fadeGroup = fadeImage.gameObject.AddComponent<CanvasGroup>();

        // 전체 화면 덮도록
        var rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 외부에서 호출하여 씬을 페이드 전환
    /// </summary>
    public void FadeToScene(string sceneName, float delay = 0f)
    {
        // 아이템을 들고 있으면 씬 이동 차단
        if (HeldItemManager.Instance != null &&
            HeldItemManager.Instance.IsHoldingItem())
        {
            Debug.LogWarning("[FadeManager] 아이템을 내려놓아야 이동할 수 있습니다.");
            return;
        }

        if (isFading) return;

        StartCoroutine(FadeAndSwitchScenes(sceneName, delay));
    }

    /// <summary>
    /// 페이드 → 씬 전환 → 페이드 인 코루틴
    /// </summary>
    private IEnumerator FadeAndSwitchScenes(string sceneName, float delay)
    {
        isFading = true;

        yield return StartCoroutine(FadeOut());

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        //손님 데이터 저장

        var activeScene = SceneManager.GetActiveScene();
        if (CustomerSaveManager.Instance != null &&
            activeScene.name == playerStoreSceneName)
        {
            CustomerSaveManager.Instance.SaveFromScene();
        }
        Debug.Log($"[FadeManager] 현재 씬 이름: {activeScene.name}, 저장해야 하는 씬 이름: {playerStoreSceneName}");
        TimeManager.Instance?.SaveDayData();
        SceneManager.LoadScene(sceneName);
        yield return null; 

        // 플레이어 위치/방향 세팅
        if (sceneName == "VillageScene" && TutorialManager.Instance != null)
        {
            TutorialManager.Instance.PrepareVillageIntroUnderFade();
        }

        if (sceneName == "VillageScene" && preRevealHoldSeconds > 0f)
            yield return new WaitForSecondsRealtime(preRevealHoldSeconds);

        yield return StartCoroutine(FadeIn());

        // 화면이 보이기 시작한 뒤 자동 걷기 연출 시작
        if (sceneName == "VillageScene" && TutorialManager.Instance != null)
        {
            TutorialManager.Instance.BeginVillageIntroAfterFade();
        }

        yield return new WaitUntil(() => !IsPointerDown());

        Input.ResetInputAxes();

        var es = EventSystem.current;
        if (es != null)
        {
            es.SetSelectedGameObject(null);

            // StandaloneInputModule (레거시)
            var stand = es.GetComponent<StandaloneInputModule>();
            if (stand != null) { stand.enabled = false; yield return null; stand.enabled = true; }

#if ENABLE_INPUT_SYSTEM
    // InputSystemUIInputModule (새 입력 시스템)
    var inputSys = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    if (inputSys != null) { inputSys.enabled = false; yield return null; inputSys.enabled = true; }
#endif
        }

        yield return new WaitUntil(() => !Input.GetMouseButton(0));

        isFading = false;
    }

    private bool IsPointerDown()
    {
        if (Input.GetMouseButton(0)) return true; // 레거시

#if ENABLE_INPUT_SYSTEM
    var mouse = UnityEngine.InputSystem.Mouse.current;
    if (mouse != null && mouse.leftButton.isPressed) return true; // 새 입력 시스템
#endif
        return false;
    }

    /// <summary>
    /// 화면 어둡게 (페이드 아웃)
    /// </summary>
    public IEnumerator FadeOut()
    {
        EnsureOverlay();
        fadeImage.gameObject.SetActive(true);

        //페이드 시작하면서 클릭 차단
        fadeImage.raycastTarget = true;
        if (fadeGroup != null) { fadeGroup.blocksRaycasts = true; fadeGroup.interactable = true; }

        fadeImage.color = new Color(0, 0, 0, 0);
        float t = 0;
        Color color = fadeImage.color;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(0, 1, t / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = 1f;
        fadeImage.color = color;
    }

    /// <summary>
    /// 화면 밝게 (페이드 인)
    /// </summary>
    public IEnumerator FadeIn()
    {
        // 페이드 인 중에도 클릭 차단 유지
        fadeImage.raycastTarget = true;
        if (fadeGroup != null) { fadeGroup.blocksRaycasts = true; fadeGroup.interactable = true; }

        fadeImage.color = new Color(0, 0, 0, 1);
        float t = 0;
        Color color = fadeImage.color;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(1, 0, t / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = 0f;
        fadeImage.color = color;

        // 클릭 차단 해제
        fadeImage.raycastTarget = false;
        if (fadeGroup != null) { fadeGroup.blocksRaycasts = false; fadeGroup.interactable = false; }
        fadeImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// 아이템 보유 여부와 관계없이 시스템 연출로 씬 전환
    /// </summary>
    public void FadeToSceneForced(string sceneName, float delay = 0f)
    {
        if (isFading)
            return;

        StartCoroutine(FadeAndSwitchScenes(sceneName, delay));
    }
}
