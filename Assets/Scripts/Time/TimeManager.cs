using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Globalization;
using UnityEngine.SceneManagement;

public class TimeManager : MonoBehaviour
{
    public static event Action OnNewDayStarted;
    public static TimeManager Instance { get; private set; }

    public int hour = 9;
    public int minute = 0;
    public float realSecondsPerGameMinute = 0.9f;
    private float timer = 0f;

    public TMP_Text dayText;

    public Image clockHandImage;

    public int currentDay = 0;
    private int totalGameMinutes = (26 - 9) * 60;

    private string currentServer;

    public bool isTimeFlow = true; // 시간 흐름 제어 변수

    private DateTime? _sessionStartUtc;
    private string _currentServerForPlay;
    private long _cachedPlaySeconds;

    public GameObject dayEndPanel;  
    public CanvasGroup dayEndGroup;     
    private Coroutine dayEndCo; 
    private bool dayEndWarningShown = false; 

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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void SetServerName(string serverName)
    {
        currentServer = serverName;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        dayText = GameObject.Find("DayText")?.GetComponent<TMP_Text>();
        clockHandImage = GameObject.Find("DayPanel_niddle")?.GetComponent<Image>();
        UpdateDayUI();
        UpdateClockProgressUI();

        var name = scene.name;
        bool shouldPause =
            name == "IntroScene" ||
            name == "SaveSelectScene" ||
            name == "StatementScene";
        isTimeFlow = !shouldPause;

        WireDayEndPanelInScene();
    }
    void Start()
    {
        // 동적으로 씬에서 오브젝트를 찾아 연결
        if (dayText == null)
            dayText = GameObject.Find("DayText")?.GetComponent<TMP_Text>();

        if (clockHandImage == null)
            clockHandImage = GameObject.Find("DayPanel_niddle")?.GetComponent<Image>();

        WireDayEndPanelInScene();

        UpdateDayUI();
        UpdateClockProgressUI();
    }

    void WireDayEndPanelInScene()
    {
        if (dayEndPanel != null && dayEndGroup != null) return;

        var groups = FindObjectsOfType<CanvasGroup>(true);
        foreach (var cg in groups)
        {
            if (cg.gameObject.name == "DayEndWarningPanel")
            {
                if (!cg.gameObject.scene.IsValid()) continue;

                dayEndPanel = cg.gameObject;
                dayEndGroup = cg;

                // 초기 상태 정리
                dayEndGroup.alpha = 0f;
                dayEndPanel.SetActive(false);
                Debug.Log("[TimeManager] DayEndWarningPanel auto-wired.");
                break;
            }
        }
    }

    void Update()
    {
        if (!isTimeFlow) return;

        // 명세서 씬(StatementScene)에서는 시간 진행 X
        if (SceneManager.GetActiveScene().name == "StatementScene")
            return;

        timer += Time.deltaTime;
        if (timer >= realSecondsPerGameMinute)
        {
            timer = 0f;
            minute += 1;
            if (minute >= 60)
            {
                minute = 0;
                hour += 1;

                if (hour >= 26)
                {
                    StartCoroutine(EndOfDayRoutine());
                }
            }

            // '하루 종료 1분 전' 체크
            CheckDayEndWarning();

            UpdateClockProgressUI();
        }
    }

    private void CheckDayEndWarning()
    {
        int minutesPassed = (hour - 9) * 60 + minute;

        int remainingMinutes = totalGameMinutes - minutesPassed;

        // 남은 시간이 1분이고, 아직 경고를 안 띄웠다면
        if (remainingMinutes == 60 && !dayEndWarningShown)
        {
            SFXManager.Instance.PlayDayOffSFX();
            ShowDayEndWarning();
            dayEndWarningShown = true;
        }
    }

    public void ShowDayEndWarning()
    {
        if (dayEndCo != null)
            StopCoroutine(dayEndCo);

        dayEndCo = StartCoroutine(DayEndWarningRoutine());
    }

    private IEnumerator DayEndWarningRoutine()
    {
        if (dayEndPanel == null || dayEndGroup == null)
            yield break;

        dayEndPanel.SetActive(true);

        float duration = 0.5f;
        float t = 0f;

        // 페이드 인
        while (t < duration)
        {
            t += Time.deltaTime;
            dayEndGroup.alpha = Mathf.Lerp(0f, 1f, t / duration);
            yield return null;
        }
        dayEndGroup.alpha = 1f;

        yield return new WaitForSeconds(2f);

        // 페이드 아웃
        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            dayEndGroup.alpha = Mathf.Lerp(1f, 0f, t / duration);
            yield return null;
        }
        dayEndGroup.alpha = 0f;

        dayEndPanel.SetActive(false);
        dayEndCo = null;
    }

    public void LoadDay()
    {
        if (string.IsNullOrWhiteSpace(currentServer))
        {
            Debug.LogWarning(
                "[TimeManager] 서버명이 설정되지 않아 " +
                "날짜·시간을 불러올 수 없습니다."
            );

            return;
        }

        if (!SaveService.EnsureLoaded(currentServer))
        {
            currentDay = 1;
            hour = 9;
            minute = 0;

            UpdateDayUI();
            UpdateClockProgressUI();

            return;
        }

        SaveData saveData =
            SaveService.CurrentData;

        if (saveData == null ||
            saveData.worldTimeData == null)
        {
            currentDay = 1;
            hour = 9;
            minute = 0;

            Debug.LogWarning(
                "[TimeManager] 날짜·시간 데이터가 없어 " +
                "기본값을 사용합니다."
            );
        }
        else
        {
            WorldTimeSaveData timeData =
                saveData.worldTimeData;

            currentDay = Mathf.Max(1, timeData.day);
            hour = Mathf.Clamp(timeData.hour, 0, 26);
            minute = Mathf.Clamp(timeData.minute, 0, 59);
        }

        // 이전 세이브에서 남아 있던 분 계산값 제거
        timer = 0f;

        UpdateDayUI();
        UpdateClockProgressUI();

        dayEndWarningShown = false;
    }

    void UpdateClockProgressUI()
    {

        if (clockHandImage == null) return;

        int minutesPassed = (hour - 9) * 60 + minute;
        float progress = Mathf.Clamp01((float)minutesPassed / totalGameMinutes);
        float zAngle = Mathf.Lerp(-90f, -360f, progress);

        clockHandImage.rectTransform.localEulerAngles = new Vector3(0, 0, zAngle);
    }

    void UpdateDayUI()
    {
        if (dayText == null) return;
        dayText.text = $"{currentDay}일차";
    }

    IEnumerator EndOfDayRoutine()
    {
        isTimeFlow = false;

        if (NPCDialogueUIManager.Instance != null && NPCDialogueUIManager.Instance.IsOpen())
            NPCDialogueUIManager.Instance.CloseDialogue();

        if (DialogueFocusManager.Instance != null)
            DialogueFocusManager.Instance.EndFocusImmediate();

        yield return null;

        currentDay++;  
        hour = 9;    
        minute = 0;

        //날 넘어갈때 손님도 초기화
        if (CustomerSaveManager.Instance != null)
        {
            CustomerSaveManager.Instance.ClearForNewDay();
        }

        // 다음 날로 넘어갈 때 플래그 리셋
        dayEndWarningShown = false;

        SaveDayData();

        OnNewDayStarted?.Invoke();

        yield return new WaitForSeconds(1f);

        if (FadeManager.Instance != null)
            FadeManager.Instance.FadeToScene("StatementScene");
        else
            SceneManager.LoadScene("StatementScene");
    }

    public void SaveDayData()
    {
        if (string.IsNullOrWhiteSpace(currentServer))
        {
            Debug.LogWarning(
                "[TimeManager] 서버명이 설정되지 않아 " +
                "날짜·시간 저장을 건너뜁니다."
            );

            return;
        }

        if (!SaveService.EnsureLoaded(currentServer))
        {
            Debug.LogError(
                "[TimeManager] 현재 세이브를 준비할 수 없어 " +
                "날짜·시간을 저장하지 못했습니다: " +
                currentServer
            );

            return;
        }

        SaveService.CurrentData.worldTimeData =
            new WorldTimeSaveData
            {
                day = Mathf.Max(1, currentDay),
                hour = Mathf.Clamp(hour, 0, 26),
                minute = Mathf.Clamp(minute, 0, 59)
            };

        SaveService.CurrentData
            .worldTimeMigrationCompleted = true;

        SaveService.SaveCurrent();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded_PlaySession;
    }

    void OnDisable()
    {
        if (this == Instance)
            SaveDayData();
        SceneManager.sceneLoaded -= OnSceneLoaded_PlaySession;
    }

    // 외부에서 시간 흐름 On/Off
    public void SetTimeFlow(bool flow)
    {
        isTimeFlow = flow;
    }

    public void BeginSessionForSelectedSave()
    {
        var server = PlayerPrefs.GetString("SelectedSave", "");
        if (!string.IsNullOrEmpty(server)) BeginSession(server);
    }

    public void BeginSession(string serverName)
    {
        EndAndPersistSession();

        _currentServerForPlay = serverName;
        _cachedPlaySeconds = 0;

        if (SaveService.EnsureLoaded(serverName))
        {
            PlaytimeSaveData playtimeData =
                SaveService.CurrentData.playtimeData;

            if (playtimeData != null)
            {
                _cachedPlaySeconds = Math.Max(
                    0,
                    playtimeData.seconds
                );
            }
        }

        _sessionStartUtc = DateTime.UtcNow;
    }

    public void EndAndPersistSession()
    {
        if (_sessionStartUtc == null ||
            string.IsNullOrWhiteSpace(
                _currentServerForPlay
            ))
        {
            return;
        }

        long elapsed = (long)Math.Max(
            0,
            (
                DateTime.UtcNow -
                _sessionStartUtc.Value
            ).TotalSeconds
        );

        _cachedPlaySeconds += elapsed;

        if (SaveService.EnsureLoaded(
            _currentServerForPlay
        ))
        {
            SaveService.CurrentData.playtimeData =
                new PlaytimeSaveData
                {
                    seconds = _cachedPlaySeconds,
                    lastPlayed = DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm",
                        CultureInfo.InvariantCulture
                    )
                };

            SaveService.CurrentData
                .playtimeMigrationCompleted = true;

            SaveService.SaveCurrent();
        }
        else
        {
            Debug.LogError(
                "[TimeManager] 현재 세이브를 준비할 수 없어 " +
                "플레이 시간을 저장하지 못했습니다: " +
                _currentServerForPlay
            );
        }

        _sessionStartUtc = null;
    }

    private void OnSceneLoaded_PlaySession(Scene scene, LoadSceneMode mode)
    {
        var name = scene.name;

        // Intro/SaveSelect/Statement 씬에선 시간 멈춤 + 세션 종료
        bool nonPlayScene = name == "IntroScene" || name == "SaveSelectScene" || name == "StatementScene";
        isTimeFlow = !nonPlayScene; 

        if (nonPlayScene)
        {
            EndAndPersistSession();           // 플레이 중이었다면 종료+저장
        }
        else
        {
            // 플레이 씬에 진입 → 현재 SelectedSave 기준으로 세션 시작
            BeginSessionForSelectedSave();
        }
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) EndAndPersistSession(); 
    }

    void OnApplicationQuit()
    {
        EndAndPersistSession();           
        SaveDayData();
    }

    public string GetCurrentTimeTooltipText()
    {
        int displayHour = hour;

        if (displayHour >= 24)
            displayHour -= 24;

        string period = displayHour < 12 ? "오전" : "오후";

        int hour12 = displayHour % 12;
        if (hour12 == 0)
            hour12 = 12;

        return $"{period} {hour12}시";
    }
}
