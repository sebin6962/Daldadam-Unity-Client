using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using UnityEngine.SceneManagement;



[Serializable] class UnlockLevelEntry { public int level; public List<string> makers; public List<string> recipes; public List<string> shopItems; }
[Serializable] class UnlockConfig { public List<UnlockLevelEntry> levels; }

[Serializable] class UnlockSaveData
{
    public HashSet<string> unlockedMakers = new();
    public HashSet<string> unlockedRecipes = new();
    public HashSet<string> unlockedShopItems = new();
    public HashSet<int> pendingLevels = new();
    public HashSet<int> appliedLevels = new();
    public bool initialized;
}
public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance;
    private UnlockConfig config;
    private UnlockSaveData save = new();

    private HashSet<int> _lastAppliedLevels = new();
    private bool _revealShownToday = false;     

    const string PP_RevealLevels = "Unlock_RevealLevels_Today";
    const string PP_RevealShown = "Unlock_RevealShown_Today";


    public bool IsMakerUnlocked(string makerId)
    {
        if (save?.unlockedMakers == null) return false;
        return save.unlockedMakers.Contains(Norm(makerId));
    }
    public bool IsRecipeUnlocked(string recipeKey)
    {
        if (save?.unlockedRecipes == null) return false;
        return save.unlockedRecipes.Contains(Norm(recipeKey));
    }
    public bool IsShopItemUnlocked(string itemId, bool isMillShop)
    {
        string key = (isMillShop ? "mill:" : "shop:") + Norm(itemId);
        return save?.unlockedShopItems?.Contains(key) ?? false;
    }

    public void SwitchToServer(string serverName)
    {
        SetServerName(serverName);  
        LoadUnlockData();       
    }

    static string Norm(string s) => (s ?? "").Trim().ToLowerInvariant();

    private string _serverName = ""; 
    private string PPK(string key) => string.IsNullOrEmpty(_serverName) ? key : $"{_serverName}:{key}";
    public void SetServerName(string serverName)
    {
        _serverName = serverName ?? "";

        // 이전 슬롯의 런타임 데이터 제거
        save = new UnlockSaveData();

        _lastAppliedLevels.Clear();
        _revealShownToday = false;
    }

    public void LoadUnlockData()
    {
        if (string.IsNullOrWhiteSpace(_serverName))
        {
            SetServerName(
                PlayerPrefs.GetString(
                    "SelectedSave",
                    ""
                )
            );
        }

        if (string.IsNullOrWhiteSpace(_serverName))
        {
            Debug.LogWarning(
                "[UnlockManager] 선택된 세이브가 없습니다."
            );

            return;
        }

        if (!SaveService.EnsureLoaded(_serverName))
        {
            Debug.LogError(
                "[UnlockManager] 현재 세이브를 " +
                $"준비할 수 없습니다: {_serverName}"
            );

            return;
        }

        UnlockProgressSaveData progressData =
            SaveService.CurrentData
                .unlockProgressData;

        save = new UnlockSaveData
        {
            pendingLevels =
                new HashSet<int>(
                    progressData?.pendingLevels ??
                    new List<int>()
                ),

            appliedLevels =
                new HashSet<int>(
                    progressData?.appliedLevels ??
                    new List<int>()
                ),

            initialized =
                progressData != null &&
                progressData.initialized
        };

        // 저장 데이터가 비어 있거나 초기화되지 않은 경우 보정
        if (!save.initialized ||
            save.appliedLevels.Count == 0)
        {
            int playerLevel = 1;

            if (PlayerLevelManager.Instance != null)
            {
                playerLevel = Mathf.Max(
                    1,
                    PlayerLevelManager.Instance.Level
                );
            }
            else if (SaveService.CurrentData.levelData != null)
            {
                playerLevel = Mathf.Max(
                    1,
                    SaveService.CurrentData
                        .levelData.level
                );
            }

            SeedAppliedLevelsUpTo(playerLevel);
            save.initialized = true;
        }

        // appliedLevels를 원본으로 실제 해금 목록 재계산
        RebuildUnlockedFromApplied();

        RefreshMakerActivationInScene();
    }

    private void SeedAppliedLevelsUpTo(int level)
    {
        if (save.appliedLevels == null) save.appliedLevels = new HashSet<int>();
        for (int lv = 1; lv <= level; lv++)
            if (config.levels.Any(e => e.level == lv))
                save.appliedLevels.Add(lv);
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadConfig();

        string selectedServer =
            PlayerPrefs.GetString(
                "SelectedSave",
                ""
            );

        if (!string.IsNullOrWhiteSpace(selectedServer))
        {
            SwitchToServer(selectedServer);
            return;
        }

        // 아직 슬롯을 고르지 않은 타이틀 화면용 메모리 기본값
        save = new UnlockSaveData();
        save.appliedLevels.Add(1);
        save.initialized = true;

        // 저장할 슬롯이 없으므로 메모리 목록만 계산
        RebuildUnlockedFromApplied();
    }

    void RebuildUnlockedFromApplied()
    {
        if (save.appliedLevels == null || save.appliedLevels.Count == 0)
        {
            if (save.appliedLevels == null)
                save.appliedLevels = new HashSet<int>();

            int playerLevel = Mathf.Max(
                1,
                PlayerLevelManager.Instance ? PlayerLevelManager.Instance.Level : 1
            );

            Debug.LogWarning($"[Unlock] appliedLevels가 비어 있어서 현재 플레이어 레벨 {playerLevel}까지 시드합니다.");

            // UnlockConfig에 정의된 레벨까지만 안전하게 시드
            SeedAppliedLevelsUpTo(playerLevel);

            save.initialized = true;
        }


        // 항상 클리어 후 재계산
        save.unlockedMakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        save.unlockedRecipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        save.unlockedShopItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var lv in save.appliedLevels)
        {
            var entry = config.levels.FirstOrDefault(e => e.level == lv);
            if (entry == null) continue;

            if (entry.makers != null)
                foreach (var m in entry.makers)
                    if (!string.IsNullOrWhiteSpace(m)) save.unlockedMakers.Add(Norm(m));

            if (entry.recipes != null)
                foreach (var r in entry.recipes)
                    if (!string.IsNullOrWhiteSpace(r)) save.unlockedRecipes.Add(Norm(r));

            if (entry.shopItems != null)
            {
                foreach (var raw in entry.shopItems)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var k = Norm(raw);

                    if (k.StartsWith("shop:") || k.StartsWith("mill:"))
                        save.unlockedShopItems.Add(k);
                    else
                    {
                        save.unlockedShopItems.Add("shop:" + k);
                        save.unlockedShopItems.Add("mill:" + k);
                    }
                }
            }
        }

        SaveState();
    }

    void Start()
    {
        RefreshMakerActivationInScene();
    }

    void LoadConfig()
    {
        TextAsset json = Resources.Load<TextAsset>("Data/UnlockConfig");
        if (json == null) { Debug.LogError("[Unlock] UnlockConfig.json을 찾을 수 없음"); config = new UnlockConfig { levels = new() }; return; }
        config = JsonUtility.FromJson<UnlockConfig>(json.text) ?? new UnlockConfig { levels = new() };
    }


    void SaveState()
    {
        if (string.IsNullOrWhiteSpace(_serverName))
        {
            return;
        }

        if (!SaveService.EnsureLoaded(_serverName))
        {
            Debug.LogError(
                "[UnlockManager] 현재 세이브를 " +
                $"준비할 수 없습니다: {_serverName}"
            );

            return;
        }

        List<int> pendingLevels =
            save.pendingLevels != null
                ? save.pendingLevels
                    .OrderBy(level => level)
                    .ToList()
                : new List<int>();

        List<int> appliedLevels =
            save.appliedLevels != null
                ? save.appliedLevels
                    .OrderBy(level => level)
                    .ToList()
                : new List<int>();

        SaveService.CurrentData.unlockProgressData =
            new UnlockProgressSaveData
            {
                pendingLevels = pendingLevels,
                appliedLevels = appliedLevels,
                initialized = save.initialized
            };

        SaveService.CurrentData
            .unlockProgressMigrationCompleted = true;

        SaveService.SaveCurrent();
    }

    // 레벨업 "즉시 해금"이 아니라 "다음 날 적용" 예약만
    public void ScheduleUnlockForLevel(int level)
    {
        if (!config.levels.Any(e => e.level == level))
        {
            Debug.LogWarning($"[Unlock] UnlockConfig에 level {level} 항목이 없어 예약 실패");
            return;
        }
        if (save.pendingLevels.Add(level))
        {
            Debug.Log($"[Unlock] 레벨 {level} 해금 예약 완료 (다음 날 적용)");
            SaveState();
        }
    }

    // TimeManager가 하루 넘길 때 호출
    public void ApplyScheduledUnlocksForNewDay()
    {
        _lastAppliedLevels.Clear();
        if (save.pendingLevels != null && save.pendingLevels.Count > 0)
        {
            _lastAppliedLevels.UnionWith(save.pendingLevels);
            PersistRevealLevels(save.pendingLevels);   // 추가: 오늘 표시용 영속 버퍼
        }
        _revealShownToday = false;

        if (save.pendingLevels.Count == 0) { ClearRevealLevelsIfShown(); return; }

        foreach (int lv in save.pendingLevels.ToList())
        {
            save.appliedLevels.Add(lv);
            save.pendingLevels.Remove(lv);
        }
        RebuildUnlockedFromApplied();   
        RefreshMakerActivationInScene();
    }

    public bool HasLevelUpRevealToShow()
    {
        if (HasPendingLevelUps()) return true;
        if (_lastAppliedLevels.Count > 0 && !_revealShownToday) return true;
        var persisted = LoadRevealLevels();
        return persisted.Count > 0 && PlayerPrefs.GetInt(PPK(PP_RevealShown), 0) == 0;
    }

    public int GetLevelUpRevealLevel()
    {
        if (HasPendingLevelUps()) return GetHighestPendingLevel();
        if (_lastAppliedLevels.Count > 0) return _lastAppliedLevels.Max();
        var persisted = LoadRevealLevels();
        return persisted.Count > 0 ? Mathf.Max(persisted.ToArray()) : 0;
    }
    public int GetMaxAppliedLevel()
    {
        if (save == null || save.appliedLevels == null || save.appliedLevels.Count == 0)
            return 1; // 최소 1레벨

        return save.appliedLevels.Max();
    }

    public void RefreshMakerActivationInScene()
    {
        foreach (var maker in FindObjectsOfType<MakerInfo>(true))
        { // makerId로 판정
            bool unlocked = IsMakerUnlocked(maker.makerId);
            maker.ApplyLockState(!unlocked); // 잠금이면 true
        }
    }

    public bool HasPendingLevelUps()
    {
        return save != null && save.pendingLevels != null && save.pendingLevels.Count > 0;
    }

    public int GetHighestPendingLevel()
    {
        if (!HasPendingLevelUps()) return 0;
        return save.pendingLevels.Max();
    }

    public List<string> GetLevelUpRevealFinishKeys()
    {
        IEnumerable<int> levels;
        if (HasPendingLevelUps()) levels = save.pendingLevels;
        else if (_lastAppliedLevels.Count > 0) levels = _lastAppliedLevels;
        else levels = LoadRevealLevels();

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lv in levels)
        {
            var entry = config.levels.FirstOrDefault(e => e.level == lv);
            if (entry?.recipes == null) continue;
            foreach (var key in entry.recipes)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                var k = key.Trim();
                if (k.EndsWith("_finish", StringComparison.OrdinalIgnoreCase))
                    result.Add(k);
            }
        }
        return new List<string>(result);
    }

    public void MarkLevelUpRevealShown()
    {
        _revealShownToday = true;
        PlayerPrefs.SetInt(PPK(PP_RevealShown), 1);  // 영속 플래그
        PlayerPrefs.Save();
    }

    void OnEnable()
    {
        TimeManager.OnNewDayStarted += ApplyScheduledUnlocksForNewDay;
        SceneManager.sceneLoaded += OnSceneLoaded;     
    }
    void OnDisable()
    {
        TimeManager.OnNewDayStarted -= ApplyScheduledUnlocksForNewDay;
        SceneManager.sceneLoaded -= OnSceneLoaded;       
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        StartCoroutine(RefreshNextFrame());
    }

    IEnumerator RefreshNextFrame()
    {
        yield return null;                         
        RefreshMakerActivationInScene();                
    }

    private void PersistRevealLevels(IEnumerable<int> levels)
    {
        PlayerPrefs.SetString(PPK(PP_RevealLevels), string.Join(",", levels));
        PlayerPrefs.SetInt(PPK(PP_RevealShown), 0);
        PlayerPrefs.Save();
    }

    private List<int> LoadRevealLevels()
    {
        var s = PlayerPrefs.GetString(PPK(PP_RevealLevels), "");
        var res = new List<int>();
        if (string.IsNullOrEmpty(s)) return res;
        foreach (var tok in s.Split(','))
        {
            if (int.TryParse(tok, out var lv)) res.Add(lv);
        }
        return res;
    }

    private void ClearRevealLevelsIfShown()
    {
        if (PlayerPrefs.GetInt(PPK(PP_RevealShown), 0) == 1)
        {
            PlayerPrefs.DeleteKey(PPK(PP_RevealLevels));
            PlayerPrefs.Save();
        }
    }

    public string DebugState()
    {
        var pend = (save?.pendingLevels != null) ? string.Join(",", save.pendingLevels) : "(null)";
        var last = (_lastAppliedLevels != null) ? string.Join(",", _lastAppliedLevels) : "(null)";
        var pers = string.Join(",", LoadRevealLevels());
        var shown = PlayerPrefs.GetInt(PPK(PP_RevealShown), 0);
        return $"pending=[{pend}] lastApplied=[{last}] persisted=[{pers}] shownToday={_revealShownToday} PP_Shown={shown}";
    }
}

