using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class CropTileSave
{
    public int x, y;     
    public string harvestItemName;   
    public int currentStage;    
    public float timer;   
    public bool isWatered;  
    public string lastWaterTime;

    public bool isTree;
    public bool autoRegrow;
}

[System.Serializable]
public class FarmSaveData
{
    public List<CropTileSave> crops = new List<CropTileSave>();
    public List<int> wetXs = new List<int>(); 
    public List<int> wetYs = new List<int>();
    public double lastSavedUtcSeconds;
}

public class FarmManager : MonoBehaviour
{
    public Transform playerTransform;    
    public float treeTooltipRange = 3f;   

    public GameObject storageFullPanel;   
    public CanvasGroup storageFullGroup; 
    private Coroutine storageFullCo;     

    public Tilemap fieldTilemap; 
    public Tilemap overlayTilemap; 
    public TileBase farmTile; 
    public TileBase wetSoilTile; 
    public Tilemap seedOverlayTilemap;  
    public TileBase seedTile;     

    public GameObject cropOverlayPrefab;
    public CropData testCropData; 

    private Dictionary<Vector3Int, CropTile> growingTiles = new Dictionary<Vector3Int, CropTile>();

    private HashSet<Vector3Int> farmPositions = new HashSet<Vector3Int>();

    private HashSet<Vector3Int> wateredTiles = new();

    private HashSet<Vector3Int> autoGrowingTrees = new();

    [Header("나무 레벨 부족 패널")]
    public GameObject levelTooLowPanel;
    public CanvasGroup levelTooLowGroup;
    private Coroutine levelTooLowCo = null;

    [Header("상호작용 세팅")]
    public Transform player;          
    public float interactRadius = 1.6f; 

    private string loadedServer = "";
    private bool isRestoring;

    private TreeTooltip currentHoverTree;

    [Header("성장중 작물 툴팁")]
    public float cropTooltipRange = 1.6f;
    public Vector3 cropTooltipOffset = new Vector3(0f, 0.9f, 0f);

    private Vector3Int? currentNearbyCropPos = null;

    [Header("작물 스치기 흔들림")]
    public Vector2 cropTouchBoxPadding = new Vector2(0.12f, 0.05f); 
    public float cropTouchTopExtra = 0.45f;                
    public float playerTouchProbeOffsetY = 0.55f;           

    private readonly HashSet<Vector3Int> touchingCropTiles = new();

    [Header("플레이어 물주기 모션")]
    public PlayerWateringResolverMotion playerWateringMotion;

    private void PlayPlayerWateringMotion(Vector3Int cellPos)
    {
        Debug.Log($"[FarmWaterMotion] PlayPlayerWateringMotion 진입 / cellPos={cellPos}");

        if (player == null)
        {
            Debug.LogWarning("[FarmWaterMotion] player Transform이 FarmManager에 연결되어 있지 않습니다.");
        }
        else
        {
            Debug.Log($"[FarmWaterMotion] player 연결됨: {player.name}");
        }

        if (playerWateringMotion == null && player != null)
        {
            Debug.Log("[FarmWaterMotion] playerWateringMotion이 null이라 player에서 검색합니다.");

            // 기존 검색
            playerWateringMotion = player.GetComponent<PlayerWateringResolverMotion>();

            if (playerWateringMotion == null)
            {
                Debug.LogWarning("[FarmWaterMotion] player.GetComponent<PlayerWateringResolverMotion>() 실패");

                playerWateringMotion = player.GetComponentInChildren<PlayerWateringResolverMotion>(true);

                if (playerWateringMotion != null)
                {
                    Debug.Log($"[FarmWaterMotion] 자식에서 PlayerWateringResolverMotion 찾음: {playerWateringMotion.name}");
                }
            }
            else
            {
                Debug.Log($"[FarmWaterMotion] player에서 PlayerWateringResolverMotion 찾음: {playerWateringMotion.name}");
            }
        }

        if (playerWateringMotion == null)
        {
            Debug.LogError("[FarmWaterMotion] PlayerWateringResolverMotion을 찾지 못해서 물주기 모션을 재생할 수 없습니다.");
            return;
        }

        if (fieldTilemap == null)
        {
            Debug.LogError("[FarmWaterMotion] fieldTilemap이 null입니다.");
            return;
        }

        Vector3 tileCenter = fieldTilemap.CellToWorld(cellPos) + new Vector3(0.5f, 0.5f, 0f);

        Debug.Log($"[FarmWaterMotion] 물주기 모션 Play 호출 / tileCenter={tileCenter}");
        playerWateringMotion.Play(tileCenter);
    }

    private bool IsTreeLocked(CropData data)
    {
        if (data == null || !data.isTree) return false;

        int needLv = Mathf.Max(1, data.minLevelToInteract); // 기본 7로 세팅
        int effectiveLevel = 1;
        if (UnlockManager.Instance != null)
        {
            effectiveLevel = UnlockManager.Instance.GetMaxAppliedLevel();
        }
        else
        {
            var lvMgr = PlayerLevelManager.Instance;
            if (lvMgr != null)
                effectiveLevel = lvMgr.Level;
        }
        return effectiveLevel < needLv;
        //return playerLv < needLv;
    }

    private const string WATERING_CAN_ITEM = "village_object_wateringcan";

    private bool IsHoldingWateringCan()
    {
        if (HeldItemManager.Instance == null) return false;
        if (!HeldItemManager.Instance.IsHoldingItem()) return false;

        var name = HeldItemManager.Instance.GetHeldItemName();
        if (!string.IsNullOrEmpty(name) && name.Trim() == WATERING_CAN_ITEM)
            return true;

        var sp = HeldItemManager.Instance.GetHeldItemSprite();
        if (sp != null && sp.name == WATERING_CAN_ITEM)
            return true;

        return false;
    }

    [Header("농사 효과음 중복 방지")]
    [SerializeField] private float farmSfxCooldown = 0.15f;

    private float lastWaterSfxTime = -999f;
    private float lastSeedSfxTime = -999f;

    private void PlayWaterSfxOnce()
    {
        if (Time.time - lastWaterSfxTime < farmSfxCooldown) return;
        lastWaterSfxTime = Time.time;
        SFXManager.Instance.PlayFarmWaterSFX();
    }

    private void PlaySeedSfxOnce()
    {
        if (Time.time - lastSeedSfxTime < farmSfxCooldown) return;
        lastSeedSfxTime = Time.time;
        SFXManager.Instance.PlayFarmSeedSFX();
    }

    void Start()
    {
        string selectedServer = PlayerPrefs.GetString("SelectedSave", "");
        if (!SaveService.EnsureLoaded(selectedServer))
        {
            Debug.LogError("[Farm] 통합 세이브를 불러오지 못했습니다.");
            return;
        }

        loadedServer = SaveService.CurrentServer;
        RegisterFarmTiles();
        LoadFarmState();

        if (StorageInventory.Instance != null)
        {
            StorageInventory.Instance.LoadStorage();        
        }
        StorageInventoryUIManager.Instance?.SyncMaxSlotsToInventory();
        StorageInventoryUIManager.Instance?.UpdateSlots();

        RegisterAllTreeAnchorsInScene();
        SaveFarmState();
        if (levelTooLowPanel) levelTooLowPanel.SetActive(false);
        if (levelTooLowGroup) levelTooLowGroup.alpha = 0f;
    }

    void OnDisable() { SaveFarmState(); }
    void OnApplicationQuit() { SaveFarmState(); }

    private void Update()
    {
        List<Vector3Int> readyToAdvance = new();

        foreach (var kvp in growingTiles)
        {
            var pos = kvp.Key;
            var tile = kvp.Value;

            bool canGrow =
                tile.currentStage < tile.cropData.stages.Count - 1 &&
                (tile.isWatered || autoGrowingTrees.Contains(pos));

            if (canGrow)
            {
                tile.timer += Time.deltaTime;

                if (tile.timer >= tile.cropData.stages[tile.currentStage].timeToNextStage)
                {
                    readyToAdvance.Add(pos);
                }
            }
        }

        foreach (var pos in readyToAdvance)
        {
            AdvanceCropStage(pos);
        }

        bool blockFarmInteraction =
            NPCInteractable.BlocksOtherWorldInteraction;

        if (!blockFarmInteraction)
            HandleTreeLevelWarningByInput();

        RefreshCropInteractionStates();

        if (!blockFarmInteraction)
            HandleRightClickHarvest();

        HandleTreeTooltipHover();
        HandleGrowingCropTooltip();
        HandleCropTouchShake();
    }

    public void SaveFarmState()
    {
        if (isRestoring ||
            string.IsNullOrEmpty(loadedServer) ||
            !SaveService.IsCurrent(loadedServer) ||
            SaveService.CurrentData == null)
        {
            return;
        }

        var data = new FarmSaveData();

        // 1) 심어진 작물 저장
        foreach (var kv in growingTiles)
        {
            var pos = kv.Key;
            var t = kv.Value;
            data.crops.Add(new CropTileSave
            {
                x = pos.x,
                y = pos.y,
                harvestItemName = t.cropData.cropName,
                currentStage = t.currentStage,
                timer = t.timer,
                isWatered = t.isWatered,
                isTree = t.cropData.isTree,
                autoRegrow = autoGrowingTrees.Contains(pos)   // 추가
            });


        }

        // 2) 젖은 흙 저장
        foreach (var pos in wateredTiles)
        {
            data.wetXs.Add(pos.x);
            data.wetYs.Add(pos.y);
        }

        // 3) 마지막 저장 시각 기록
        data.lastSavedUtcSeconds = (System.DateTime.UtcNow - System.DateTime.UnixEpoch).TotalSeconds;
        SaveService.CurrentData.farmData = data;
        SaveService.CurrentData.farmMigrationCompleted = true;
        SaveService.SaveCurrent();
    }

    public void LoadFarmState()
    {
        if (SaveService.CurrentData == null) return;

        var data = SaveService.CurrentData.farmData;
        if (data == null)
        {
            data = new FarmSaveData();
            SaveService.CurrentData.farmData = data;
        }

        data.crops ??= new List<CropTileSave>();
        data.wetXs ??= new List<int>();
        data.wetYs ??= new List<int>();
        isRestoring = true;

        double nowUtc = (System.DateTime.UtcNow - System.DateTime.UnixEpoch).TotalSeconds;
        float elapsed = 0f;
        if (data.lastSavedUtcSeconds > 0)
            elapsed = Mathf.Max(0f, (float)(nowUtc - data.lastSavedUtcSeconds));

        foreach (var kv in growingTiles)
            if (kv.Value.cropOverlayObject) Destroy(kv.Value.cropOverlayObject);
        growingTiles.Clear();
        wateredTiles.Clear();
        autoGrowingTrees.Clear();

        // 2) 젖은 흙 복원 (overlay 타일/집합)
        int wetCount = Mathf.Min(data.wetXs.Count, data.wetYs.Count);
        for (int i = 0; i < wetCount; i++)
        {
            var pos = new Vector3Int(data.wetXs[i], data.wetYs[i], 0);
            overlayTilemap.SetTile(pos, wetSoilTile);
            wateredTiles.Add(pos);
        }

        // 3) 작물 복원
        foreach (var c in data.crops)
        {
            var pos = new Vector3Int(c.x, c.y, 0);

            bool isFarm = farmPositions.Contains(pos);
            if (!isFarm && !c.isTree) continue;

            var cropData = CropDataManager.Instance.GetCropDataByItemName(c.harvestItemName);
            if (cropData == null || cropData.stages.Count == 0) continue;

            int stage = Mathf.Clamp(c.currentStage, 0, cropData.stages.Count - 1);
            float timer = Mathf.Max(0f, c.timer);
            bool watered = c.isWatered;

            float remain = elapsed;

            bool autoRegrow = c.autoRegrow && cropData.isTree;

            while (remain > 0f && (watered || autoRegrow) && stage < cropData.stages.Count - 1)
            {
                float need = cropData.stages[stage].timeToNextStage - timer;

                if (need <= 0f)
                {
                    stage = Mathf.Min(stage + 1, cropData.stages.Count - 1);
                    timer = 0f;

                    if (stage >= cropData.stages.Count - 1)
                    {
                        watered = false;
                        autoRegrow = false;
                    }
                    continue;
                }

                if (remain >= need)
                {
                    remain -= need;
                    stage += 1;
                    timer = 0f;

                    if (stage >= cropData.stages.Count - 1)
                    {
                        watered = false;
                        autoRegrow = false;
                    }
                }
                else
                {
                    timer += remain;
                    remain = 0f;
                }
            }
            if (!watered && overlayTilemap.GetTile(pos) == wetSoilTile)
            {
                overlayTilemap.SetTile(pos, null);
                wateredTiles.Remove(pos);
            }

            // 오버레이 스프라이트 오브젝트 재생성
            Vector3 overlayWorldPos = overlayTilemap.CellToWorld(pos) + new Vector3(0.5f, 0.5f, 0f);
            GameObject overlay = Instantiate(cropOverlayPrefab, overlayWorldPos, Quaternion.identity, transform);
            overlay.GetComponent<SpriteRenderer>().sprite = cropData.stages[Mathf.Clamp(stage, 0, cropData.stages.Count - 1)].sprite;

            SetupCropSensor(overlay, cropData);

            if (cropData.isTree)
            {
                SetupTreeComponents(overlay);
                SetupTreeClickArea(overlay);
                SetupTreeTooltip(overlay, cropData);
            }

            var cropInfo = new CropTile(pos, cropData, overlay)
            {
                currentStage = stage,
                timer = timer,
                isWatered = watered
            };

            if (autoRegrow)
            {
                autoGrowingTrees.Add(pos);
            }
            else
            {
                autoGrowingTrees.Remove(pos);
            }

            growingTiles.Add(pos, cropInfo);

            UpdateCropOutlineState(cropInfo);
        }

        foreach (var pos in wateredTiles)
        {
            if (growingTiles.TryGetValue(pos, out var tile))
            {
                if (!tile.isWatered)
                {
                    overlayTilemap.SetTile(pos, null);
                }
            }
        }
        isRestoring = false;
        Debug.Log($"[Farm] Loaded: {data.crops.Count} crops, {data.wetXs.Count} wet tiles");
    }

    private void RegisterAllTreeAnchorsInScene()
    {
        var anchors = FindObjectsOfType<TreeAnchor>();
        foreach (var a in anchors)
        {
            RegisterTreeAtWorldPos(a.transform.position, a.treeData, a.startStage);
        }
    }

    public void RegisterTreeAtWorldPos(Vector3 worldPos, CropData treeData, int startStage)
    {
        Vector3Int cellPos = fieldTilemap.WorldToCell(worldPos);
        if (growingTiles.ContainsKey(cellPos)) return;

        Vector3 overlayWorldPos = overlayTilemap.CellToWorld(cellPos) + new Vector3(0.5f, 0.5f, 0f);
        GameObject overlay = Instantiate(cropOverlayPrefab, overlayWorldPos, Quaternion.identity, transform);

        var sr = overlay.GetComponent<SpriteRenderer>();
        int clampedStage = Mathf.Clamp(startStage, 0, treeData.stages.Count - 1);
        sr.sprite = treeData.stages[clampedStage].sprite;

        SetupCropSensor(overlay, treeData);

        if (treeData.isTree)
        {
            SetupTreeComponents(overlay);
            SetupTreeClickArea(overlay);
            SetupTreeTooltip(overlay, treeData);
        }

        // growingTiles에 등록
        var tile = new CropTile(cellPos, treeData, overlay)
        {
            currentStage = clampedStage,
            timer = 0f,
            isWatered = false
        };
        growingTiles.Add(cellPos, tile);
        UpdateCropOutlineState(tile);
    }

    private void SetupTreeComponents(GameObject overlay)
    {
        if (!overlay.TryGetComponent<YSort>(out var ysort))
            ysort = overlay.AddComponent<YSort>();

        // 2) 중앙 줄기 충돌 박스 (비-트리거)
        if (!overlay.TryGetComponent<BoxCollider2D>(out var box))
            box = overlay.AddComponent<BoxCollider2D>();

        box.isTrigger = false;

        box.offset = new Vector2(0f, 1.33518f);
        box.size = new Vector2(0.9f, 0.89344f);

        var sr = overlay.GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            sr.sortingLayerName = "Obj";   
                                            
        }

        int layer = LayerMask.NameToLayer("Interactable"); 
        if (layer != -1) overlay.layer = layer;
    }

    private const string TreeClickAreaName = "TreeClickArea";

    // 줄기 충돌 콜라이더는 유지하고, 나무 스프라이트 클릭 전용 콜라이더를 별도로 만든다.
    private GameObject SetupTreeClickArea(GameObject overlay)
    {
        if (overlay == null) return null;

        Transform clickAreaTransform = overlay.transform.Find(TreeClickAreaName);
        GameObject clickArea;

        if (clickAreaTransform == null)
        {
            clickArea = new GameObject(TreeClickAreaName);
            clickArea.transform.SetParent(overlay.transform, false);
        }
        else
        {
            clickArea = clickAreaTransform.gameObject;
        }

        clickArea.layer = overlay.layer;

        var clickCollider = clickArea.GetComponent<BoxCollider2D>();
        if (clickCollider == null)
            clickCollider = clickArea.AddComponent<BoxCollider2D>();

        clickCollider.isTrigger = true;

        var treeRenderer = overlay.GetComponent<SpriteRenderer>();
        if (treeRenderer != null && treeRenderer.sprite != null)
        {
            Bounds spriteBounds = treeRenderer.sprite.bounds;
            clickCollider.size = new Vector2(
                Mathf.Max(spriteBounds.size.x, 0.1f),
                Mathf.Max(spriteBounds.size.y, 0.1f)
            );
            clickCollider.offset = spriteBounds.center;
        }

        if (clickArea.GetComponent<WorldHandCursor>() == null)
            clickArea.AddComponent<WorldHandCursor>();

        return clickArea;
    }

    private GameObject GetInteractionObject(CropTile tile)
    {
        if (tile == null || tile.cropOverlayObject == null)
            return null;

        if (tile.cropData != null && tile.cropData.isTree)
            return SetupTreeClickArea(tile.cropOverlayObject);

        return tile.cropOverlayObject;
    }


    // 1. 타일맵에서 밭 범위 자동 등록
    void RegisterFarmTiles()
    {
        farmPositions.Clear();

        BoundsInt bounds = fieldTilemap.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                TileBase tile = fieldTilemap.GetTile(pos);

                if (tile == farmTile)
                {
                    farmPositions.Add(pos);
                }
            }
        }

        Debug.Log($"밭 위치 {farmPositions.Count}개 등록 완료");
    }

    // 2. 이 위치가 밭인가?
    public bool IsFarmTile(Vector3 worldPos)
    {
        Vector3Int cellPos = fieldTilemap.WorldToCell(worldPos);
        return farmPositions.Contains(cellPos);
    }

    // 3. 추후 밭 범위 확장 (예: 레벨업)
    public void AddFarmTile(Vector3Int cellPos)
    {
        farmPositions.Add(cellPos);
        fieldTilemap.SetTile(cellPos, farmTile);
    }

    //밭에 물 뿌렸을 때 변화
    public void WaterSoil(Vector3 worldPos)
    {;
        Vector3Int cellPos = fieldTilemap.WorldToCell(worldPos);

        Debug.Log($"[FarmWater] 클릭한 셀 위치: {cellPos}");
        Debug.Log($"[FarmWater] growingTiles 포함 여부: {growingTiles.ContainsKey(cellPos)}");
        Debug.Log($"[FarmWater] wateredTiles 포함 여부: {wateredTiles.Contains(cellPos)}");

        // 1) 이미 작물/나무가 있다면 → 밭 여부와 무관하게 물주기 + 젖은 흙 표시
        if (growingTiles.TryGetValue(cellPos, out var tileInfo))
        {
            if (IsTreeLocked(tileInfo.cropData))
            {
                Debug.Log("[Tree Locked] 레벨 미만이라 나무에 물을 줄 수 없습니다.");
                return; // 젖은 흙도 깔지 않음
            }

            // 이미 물 준 상태면 효과음/처리 중복 방지
            if (tileInfo.isWatered && wateredTiles.Contains(cellPos))
                return;

            overlayTilemap.SetTile(cellPos, wetSoilTile);  // 젖은 흙 연출
            wateredTiles.Add(cellPos);
            tileInfo.isWatered = true;     

            Debug.Log("[FarmWater] 작물/나무 타일에 물주기 성공 → 모션 호출 직전");
            PlayWaterSfxOnce();
            PlayPlayerWateringMotion(cellPos);

            //village2 튜토리얼 진행 트리거 7
            if (TutorialManager.Instance && TutorialManager.Instance.IsCurrentStep(VillageSecondStep.Water))
            {
                TutorialManager.Instance.GoToNextVillageSecondStep();
            }

            SaveFarmState();

            return;


        }

        // 2) 심어진 게 없고 '밭'이면 기존처럼 젖은 흙만 표시 (씨앗 심을 준비)
        if (IsFarmTile(worldPos))
        {
            if (wateredTiles.Contains(cellPos) && overlayTilemap.GetTile(cellPos) == wetSoilTile)
                return;

            overlayTilemap.SetTile(cellPos, wetSoilTile);
            wateredTiles.Add(cellPos);

            Debug.Log("[FarmWater] 빈 밭 타일에 물주기 성공 → 모션 호출 직전");
            PlayWaterSfxOnce();
            PlayPlayerWateringMotion(cellPos);
            SaveFarmState();
        }

        bool IsFarmTile(Vector3 worldPos)
        {
            Vector3Int cellPos = fieldTilemap.WorldToCell(worldPos);
            // 변경: "밭"이거나 "이미 작물/나무가 심어진 칸"이면 true
            return farmPositions.Contains(cellPos) || growingTiles.ContainsKey(cellPos);
        }
    }

    public void PlantSeed(Vector3 worldPos, CropData cropData)
    {
        Vector3Int cellPos = fieldTilemap.WorldToCell(worldPos);

        if (!IsFarmTile(worldPos) || growingTiles.ContainsKey(cellPos))
            return;

        Vector3 overlayWorldPos = overlayTilemap.CellToWorld(cellPos) + new Vector3(0.5f, 0.5f, 0f);
        GameObject overlay = Instantiate(cropOverlayPrefab, overlayWorldPos, Quaternion.identity, transform);
        overlay.GetComponent<SpriteRenderer>().sprite = cropData.stages[0].sprite;

        SetupCropSensor(overlay, cropData);

        var cropInfo = new CropTile(cellPos, cropData, overlay);

        if (wateredTiles.Contains(cellPos))
        {
            cropInfo.isWatered = true;
        }

        growingTiles.Add(cellPos, cropInfo);

        PlaySeedSfxOnce();

        if (TutorialManager.Instance && TutorialManager.Instance.IsCurrentStep(VillageSecondStep.PlantSeed))
        {
            TutorialManager.Instance.GoToNextVillageSecondStep();
        }
        SaveFarmState();
    }

    //작물 성장
    private void AdvanceCropStage(Vector3Int pos)
    {
        var tile = growingTiles[pos];
        tile.currentStage++;
        tile.timer = 0f;

        bool isFinalStage = tile.currentStage >= tile.cropData.stages.Count - 1;
        bool isAutoTree = autoGrowingTrees.Contains(pos);

        if (tile.cropOverlayObject != null)
        {
            tile.cropOverlayObject.GetComponent<SpriteRenderer>().sprite =
                tile.cropData.stages[tile.currentStage].sprite;
        }

        // 최종 단계에 도달했을 때만 성장 상태 종료
        if (isFinalStage)
        {
            tile.isWatered = false;
            autoGrowingTrees.Remove(pos);

            overlayTilemap.SetTile(pos, null);
            wateredTiles.Remove(pos);

        }
        else
        {
            // 나무 자동 재성장은 물 없이 자라는 상태이므로 젖은 흙 표시 제거 유지
            if (isAutoTree)
            {
                overlayTilemap.SetTile(pos, null);
                wateredTiles.Remove(pos);
            }
        }

        Debug.Log($"작물 {tile.cropData.cropName}이 {tile.currentStage}단계로 성장함");

        UpdateCropOutlineState(tile);

        if (TutorialManager.Instance &&
            TutorialManager.Instance.IsCurrentStep(VillageSecondStep.CropGrowing) &&
            tile.currentStage == tile.cropData.stages.Count - 1)
        {
            TutorialManager.Instance.GoToNextVillageSecondStep();
        }
        SaveFarmState();
    }

    // 수확 처리 함수
    private void HandleRightClickHarvest()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        // 물뿌리개를 들고 있으면 수확 금지
        if (IsHoldingWateringCan())
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 mouseWorldPos =
            cam.ScreenToWorldPoint(Input.mousePosition);

        Vector2 clickPoint =
            new Vector2(mouseWorldPos.x, mouseWorldPos.y);

        // 마우스 위치에 존재하는 실제 Collider2D들을 검사
        Collider2D[] clickedColliders =
            Physics2D.OverlapPointAll(clickPoint);

        foreach (Collider2D clickedCollider in clickedColliders)
        {
            if (clickedCollider == null)
                continue;

            foreach (var pair in growingTiles)
            {
                Vector3Int cellPos = pair.Key;
                CropTile tile = pair.Value;

                if (tile == null ||
                    tile.cropData == null ||
                    tile.cropOverlayObject == null)
                    continue;

                Transform cropTransform =
                    tile.cropOverlayObject.transform;

                // 클릭한 콜라이더가 작물 루트 또는 작물의 자식인지 확인
                bool clickedThisCrop =
                    clickedCollider.transform == cropTransform ||
                    clickedCollider.transform.IsChildOf(cropTransform);

                if (!clickedThisCrop)
                    continue;

                bool isFullyGrown =
                    tile.currentStage >=
                    tile.cropData.stages.Count - 1;

                if (!isFullyGrown)
                    return;

                // 아웃라인과 똑같이 Sensor 콜라이더 범위 안에서만 수확 가능
                var sensor =
                    tile.cropOverlayObject.GetComponentInChildren<SpriteSensor>(true);

                if (sensor == null || !sensor.IsPlayerInside)
                    return;

                if (tile.cropData.isTree && IsTreeLocked(tile.cropData))
                {
                    ShowLevelTooLowByInput();
                    return;
                }

                // 작물 성장 튜토리얼 중에는 수확 금지
                if (TutorialManager.Instance != null &&
                    TutorialManager.Instance.IsCurrentStep(
                        VillageSecondStep.CropGrowing))
                {
                    return;
                }

                StartCoroutine(HarvestCropAfterInteractionResolution(
                    cellPos,
                    tile.cropData.cropName
                ));

                // 한 번 클릭으로 여러 작물이 수확되는 것 방지
                return;
            }
        }
    }

    // 같은 입력 프레임에 물뿌리개 들기와 작물 수확이 함께 감지되면
    // 모든 상호작용 처리가 끝난 다음 물뿌리개 상태를 다시 확인합니다.
    // 따라서 MonoBehaviour의 Update 실행 순서와 관계없이 물뿌리개가 우선됩니다.
    private IEnumerator HarvestCropAfterInteractionResolution(
        Vector3Int pos,
        string cropName)
    {
        yield return null;

        HarvestCrop(pos, cropName);
    }

    private void HarvestCrop(Vector3Int pos, string cropName)
    {
        if (NPCInteractable.BlocksOtherWorldInteraction)
            return;

        if (IsHoldingWateringCan())
            return;

        if (!growingTiles.TryGetValue(pos, out var tile)) return;
        var data = tile.cropData;

        if (IsTreeLocked(data))
        {
            Debug.Log("[Tree Locked] 레벨 미만이라 나무를 수확할 수 없습니다.");
            return;
        }

        // 1) 수확 예정 수량 계산
        int amount = data.isTree ? 5 : 1;

        // 2) 수확물 키
        string itemKey = data.harvestItemName;

        // 3) 창고 공간 확인 (없으면 경고 패널만 띄우고 return)
        if (!StorageInventory.Instance.HasRoomFor(itemKey, amount))
        {
            // 수확 가능한 작물을 클릭했다는 반응은 창고가 가득 차도 보여준다.
            if (tile.cropOverlayObject != null)
            {
                StartCoroutine(PlayTreeHarvestShake(tile.cropOverlayObject.transform));
            }

            ShowStorageFull();
            return;
        }

        SFXManager.Instance.HarvestingSFX();

        StorageInventory.Instance.TryAddItem(itemKey, amount);
        StorageInventory.Instance.SaveStorage();

        Sprite cropSprite = Resources.Load<Sprite>("Sprites/Ingredients/" + itemKey); // 수확물 스프라이트 로드

        Vector3 worldPos = fieldTilemap.CellToWorld(pos) + new Vector3(0.5f, 0.5f, 0);

        StorageIconFlyEffect.Instance.Play(cropSprite, worldPos);
        SFXManager.Instance.HarvestItemSFX();

        // 0.5초 뒤 알림 등록
        StorageAlertManager.Instance.NotifyNewHarvestedItem(cropName);

        if (data.isTree)
        {
            if (tile.cropOverlayObject != null)
            {
                StartCoroutine(PlayTreeHarvestShake(tile.cropOverlayObject.transform));
            }

            tile.currentStage = Mathf.Clamp(data.harvestResetStage, 0, data.stages.Count - 1);
            tile.timer = 0f;
            tile.isWatered = false;

            // 수확 후에는 물 없이 자동 재성장 시작
            autoGrowingTrees.Add(pos);

            if (tile.cropOverlayObject != null)
            {
                var sr = tile.cropOverlayObject.GetComponent<SpriteRenderer>();
                sr.sprite = data.stages[tile.currentStage].sprite;
            }

            overlayTilemap.SetTile(pos, null);
            wateredTiles.Remove(pos);

            UpdateCropOutlineState(tile);
        }
        else
        {
            if (tile.cropOverlayObject != null) Destroy(tile.cropOverlayObject);
            overlayTilemap.SetTile(pos, null);
            wateredTiles.Remove(pos);
            growingTiles.Remove(pos);
        }

        Debug.Log($"작물 {cropName} 수확됨 → 창고로 이동");

        //village2 튜토리얼 진행 트리거 10
        if (TutorialManager.Instance && TutorialManager.Instance.IsCurrentStep(VillageSecondStep.HarvestCrop))
        {
            TutorialManager.Instance.GoToNextVillageSecondStep();
        }
        SaveFarmState();
    }

    private readonly HashSet<Transform> shakingTrees = new HashSet<Transform>();

    private IEnumerator PlayTreeHarvestShake(Transform target)
    {
        if (target == null) yield break;

        //  중복 실행 방지
        if (shakingTrees.Contains(target))
            yield break;

        shakingTrees.Add(target);

        Vector3 originalPos = target.localPosition;
        Quaternion originalRot = target.localRotation;

        float duration = 0.22f;   
        float maxAngle = 7f;   
        float maxOffset = 0.03f; 
        float frequency = 24f;  

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            float damping = 1f - t;

            float wave = Mathf.Sin(time * frequency);

            float angle = wave * maxAngle * damping;
            float offsetX = wave * maxOffset * damping;

            target.localRotation = Quaternion.Euler(0f, 0f, angle);
            target.localPosition = originalPos + new Vector3(offsetX, 0f, 0f);

            yield return null;
        }

        target.localPosition = originalPos;
        target.localRotation = originalRot;

        shakingTrees.Remove(target);
    }

    private void HandleTreeLevelWarningByInput()
    {
        // 1) 마우스 왼클릭: 커서 아래 나무 잠금이면 경고
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int cellPos = fieldTilemap.WorldToCell(worldPos);

            if (growingTiles.TryGetValue(cellPos, out var tile))
            {
                var data = tile.cropData;
                if (data != null && data.isTree && IsTreeLocked(data)) 
                {
                    if (player == null) return;

                    Vector3 tileCenter = overlayTilemap.CellToWorld(cellPos) + new Vector3(0.5f, 0.5f, 0f);

                    if (Vector2.Distance(player.position, tileCenter) <= interactRadius)
                    {
                        ShowLevelTooLowByInput();
                        return;
                    }
                }
            }
        }

        // 2) E키: 플레이어 주변 반경 내에 '나무 잠금'이 하나라도 있으면 경고
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (player == null) return;

            foreach (var kv in growingTiles)
            {
                var cell = kv.Key;
                var cropTile = kv.Value;
                var data = cropTile.cropData;

                if (data == null || !data.isTree) continue;

                Vector3 tileCenter = overlayTilemap.CellToWorld(cell) + new Vector3(0.5f, 0.5f, 0f);

                if (Vector2.Distance(player.position, tileCenter) <= interactRadius)
                {
                    if (IsTreeLocked(data))
                    {
                        ShowLevelTooLowByInput();
                        return; 
                    }
                }
            }
        }
    }

    public void ShowLevelTooLowByInput()
    {
        if (levelTooLowPanel == null || levelTooLowGroup == null) return;
        if (levelTooLowCo != null) StopCoroutine(levelTooLowCo);
        levelTooLowCo = StartCoroutine(LevelTooLowRoutine());
    }

    private IEnumerator LevelTooLowRoutine()
    {
        levelTooLowPanel.SetActive(true);

        float duration = 0.5f;
        float t = 0f;

        // Fade In
        while (t < duration)
        {
            t += Time.deltaTime;
            levelTooLowGroup.alpha = Mathf.Lerp(0f, 1f, t / duration);
            yield return null;
        }
        levelTooLowGroup.alpha = 1f;

        yield return new WaitForSeconds(1f);

        // Fade Out
        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            levelTooLowGroup.alpha = Mathf.Lerp(1f, 0f, t / duration);
            yield return null;
        }
        levelTooLowGroup.alpha = 0f;

        levelTooLowPanel.SetActive(false);
        levelTooLowCo = null;
    }


    public bool HasPlantedAt(Vector3 worldPos)
    {
        var cell = fieldTilemap.WorldToCell(worldPos);
        return growingTiles.ContainsKey(cell);
    }

    public void ShowStorageFull()
    {
        if (storageFullCo != null) StopCoroutine(storageFullCo);
        storageFullCo = StartCoroutine(StorageFullRoutine());
    }

    private IEnumerator StorageFullRoutine()
    {
        storageFullPanel.SetActive(true);

        float duration = 0.5f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            storageFullGroup.alpha = Mathf.Lerp(0f, 1f, t / duration);
            yield return null;
        }
        storageFullGroup.alpha = 1f;

        yield return new WaitForSeconds(1f);

        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            storageFullGroup.alpha = Mathf.Lerp(1f, 0f, t / duration);
            yield return null;
        }
        storageFullGroup.alpha = 0f;

        storageFullPanel.SetActive(false);
        storageFullCo = null;
    }

    private void SetupTreeTooltip(GameObject overlay, CropData data)
    {
        if (overlay == null || data == null) return;

        var tooltip = overlay.GetComponent<TreeTooltip>();
        if (tooltip == null)
            tooltip = overlay.AddComponent<TreeTooltip>();

        // 한글 이름은 기존 ItemTooltipDB 재사용
        string label;
        if (!ItemTooltipDB.TooltipTexts.TryGetValue(data.harvestItemName, out label))
            label = data.harvestItemName;

        tooltip.treeName = label + "나무";

        tooltip.worldOffset = new Vector3(0f, 1.8f, 0f);
    }

    private void HandleTreeTooltipHover()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (InventoryTooltipManager.Instance == null) return;

        //  마우스 스크린 좌표 → 월드 좌표
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = -cam.transform.position.z;
        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);

        Vector2 point = new Vector2(mouseWorld.x, mouseWorld.y);

        int mask = LayerMask.GetMask("Interactable", "Default");
        Collider2D col = Physics2D.OverlapPoint(point, mask);

        if (col != null)
        {
            TreeTooltip tt = col.GetComponentInParent<TreeTooltip>();
            if (tt != null)
            {
                if (playerTransform != null)
                {
                    float sqrDist = (playerTransform.position - tt.transform.position).sqrMagnitude;
                    if (sqrDist > treeTooltipRange * treeTooltipRange)
                    {
                        if (currentHoverTree != null)
                        {
                            currentHoverTree = null;
                            InventoryTooltipManager.Instance.HideWorld();
                        }
                        return;
                    }
                }

                if (currentHoverTree != tt)
                {
                    currentHoverTree = tt;
                    InventoryTooltipManager.Instance.ShowWorld(
                        tt.treeName,
                        tt.transform.position + tt.worldOffset
                    );
                }
                return;
            }
        }

        if (currentHoverTree != null)
        {
            currentHoverTree = null;
            InventoryTooltipManager.Instance.HideWorld();
        }
    }

    //아웃라인
    private void SetupCropSensor(GameObject overlay, CropData data)
    {
        if (overlay == null) return;

        Transform sensorTr = overlay.transform.Find("Sensor");
        Transform outlineTr = overlay.transform.Find("OutLineSprite");

        if (sensorTr == null || outlineTr == null) return;

        var outlineSR = outlineTr.GetComponent<SpriteRenderer>();

        if (outlineSR != null)
        {
            outlineSR.sprite = data != null ? data.outlineSprite : null;

            if (data != null && data.isTree)
            {
                outlineTr.localPosition = data.outlineOffset;
            }

            outlineSR.enabled = false;
        }

        var sensor = sensorTr.GetComponent<SpriteSensor>();

        if (sensor == null)
            sensor = sensorTr.gameObject.AddComponent<SpriteSensor>();

        sensor.spriteRenderer = outlineSR;
        sensor.playerLayer = LayerMask.GetMask("Player");
        sensor.enabled = false;
        sensor.SetOutline(false);
    }

    //최종 단계일 때만 활성화
    private void UpdateCropOutlineState(CropTile tile)
    {
        if (tile == null ||
            tile.cropOverlayObject == null ||
            tile.cropData == null)
            return;

        bool isFinalStage =
            tile.currentStage >= tile.cropData.stages.Count - 1;

        var sensor =
            tile.cropOverlayObject.GetComponentInChildren<SpriteSensor>(true);

        if (sensor != null)
        {
            sensor.enabled = isFinalStage;

            if (!isFinalStage)
                sensor.SetOutline(false);
        }

        // 손 모양 커서 처리
        GameObject interactionObject = GetInteractionObject(tile);
        if (interactionObject == null)
            return;

        var handCursor =
            interactionObject.GetComponent<WorldHandCursor>();

        if (handCursor == null)
        {
            handCursor =
                interactionObject.AddComponent<WorldHandCursor>();
        }

        var collider = interactionObject.GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            collider = interactionObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;

        // 현재 작물 스프라이트 크기에 맞춰 콜라이더 설정
        SpriteRenderer cropRenderer =
            tile.cropOverlayObject.GetComponent<SpriteRenderer>();

        if (cropRenderer != null && cropRenderer.sprite != null)
        {
            Bounds spriteBounds = cropRenderer.sprite.bounds;

            collider.size = new Vector2(
                Mathf.Max(spriteBounds.size.x, 0.1f),
                Mathf.Max(spriteBounds.size.y, 0.1f)
            );

            collider.offset = new Vector2(
                spriteBounds.center.x,
                spriteBounds.center.y
            );
        }

        // 최종 성장 단계이면서 플레이어가 아웃라인 Sensor 범위 안에 있을 때만
        // 클릭 콜라이더와 손 모양 커서를 활성화한다.
        bool canInteract =
            isFinalStage &&
            sensor != null &&
            sensor.IsPlayerInside;

        collider.enabled = canInteract;
        handCursor.enabled = canInteract;

        Debug.Log(
            $"[Crop Cursor] {tile.cropData.cropName} " +
            $"final={isFinalStage}, " +
            $"sensorInside={sensor != null && sensor.IsPlayerInside}, " +
            $"cursor={handCursor.enabled}, " +
            $"collider={collider.enabled}"
        );
    }

    // 플레이어가 Sensor 범위에 들어오거나 나갈 때
    // 손 커서와 클릭 수확 범위도 아웃라인 범위와 계속 동기화
    private void RefreshCropInteractionStates()
    {
        foreach (var pair in growingTiles)
        {
            CropTile tile = pair.Value;

            if (tile == null ||
                tile.cropData == null ||
                tile.cropOverlayObject == null)
                continue;

            bool isFinalStage =
                tile.currentStage >= tile.cropData.stages.Count - 1;

            var sensor =
                tile.cropOverlayObject.GetComponentInChildren<SpriteSensor>(true);

            bool canInteract =
                isFinalStage &&
                sensor != null &&
                sensor.IsPlayerInside;

            GameObject interactionObject = GetInteractionObject(tile);
            if (interactionObject == null)
                continue;

            var collider =
                interactionObject.GetComponent<BoxCollider2D>();

            if (collider != null)
                collider.enabled = canInteract;

            var handCursor =
                interactionObject.GetComponent<WorldHandCursor>();

            if (handCursor != null)
                handCursor.enabled = canInteract;
        }
    }

    private void HandleGrowingCropTooltip()
    {
        if (InventoryTooltipManager.Instance == null) return;

        Transform target = playerTransform != null ? playerTransform : player;
        if (target == null) return;

        // 나무 툴팁이 우선
        if (currentHoverTree != null) return;

        CropTile nearestTile = null;
        Vector3Int nearestPos = default;

        float maxSqr = cropTooltipRange * cropTooltipRange;
        float bestSqr = maxSqr;

        foreach (var kv in growingTiles)
        {
            var pos = kv.Key;
            var tile = kv.Value;

            if (tile == null || tile.cropData == null) continue;
            if (tile.cropData.isTree) continue;
            if (!tile.isWatered) continue;

            int finalStage = tile.cropData.stages.Count - 1;
            if (tile.currentStage >= finalStage) continue;

            Vector3 cropWorldPos =
                tile.cropOverlayObject != null
                ? tile.cropOverlayObject.transform.position
                : overlayTilemap.CellToWorld(pos) + new Vector3(0.5f, 0.5f, 0f);

            float sqr = (target.position - cropWorldPos).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                nearestTile = tile;
                nearestPos = pos;
            }
        }

        if (nearestTile == null)
        {
            if (currentNearbyCropPos.HasValue)
            {
                currentNearbyCropPos = null;
                InventoryTooltipManager.Instance.HideWorld();
            }
            return;
        }

        currentNearbyCropPos = nearestPos;

        string tooltip = BuildGrowingCropTooltipText(nearestTile);

        Vector3 tooltipWorldPos =
            nearestTile.cropOverlayObject != null
            ? nearestTile.cropOverlayObject.transform.position + cropTooltipOffset
            : overlayTilemap.CellToWorld(nearestPos) + new Vector3(0.5f, 0.5f, 0f) + cropTooltipOffset;

        InventoryTooltipManager.Instance.ShowWorld(tooltip, tooltipWorldPos);
    }

    private string BuildGrowingCropTooltipText(CropTile tile)
    {
        string cropName = GetCropDisplayName(tile.cropData);
        float remain = GetRemainingTimeToFinalStage(tile);

        return $"{cropName}\n{FormatTime(remain)}";
    }

    private string GetCropDisplayName(CropData data)
    {
        if (data == null) return "작물";

        if (!string.IsNullOrEmpty(data.harvestItemName) &&
            ItemTooltipDB.TooltipTexts.TryGetValue(data.harvestItemName, out var name))
        {
            return name;
        }

        if (!string.IsNullOrEmpty(data.cropName))
            return data.cropName;

        return "작물";
    }

    private float GetRemainingTimeToFinalStage(CropTile tile)
    {
        if (tile == null || tile.cropData == null || tile.cropData.stages == null)
            return 0f;

        int finalStage = tile.cropData.stages.Count - 1;
        if (tile.currentStage >= finalStage)
            return 0f;

        // 현재 단계에서 다음 단계까지 남은 시간
        float remain = Mathf.Max(
            0f,
            tile.cropData.stages[tile.currentStage].timeToNextStage - tile.timer
        );

        // 이후 단계들의 시간 누적
        for (int i = tile.currentStage + 1; i < finalStage; i++)
        {
            remain += tile.cropData.stages[i].timeToNextStage;
        }

        return remain;
    }

    private string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);

        int hour = total / 3600;
        int min = (total % 3600) / 60;
        int sec = total % 60;

        if (hour > 0) return $"{hour}시간 {min}분 {sec}초";
        if (min > 0) return $"{min}분 {sec}초";
        return $"{sec}초";
    }

    private void HandleCropTouchShake()
    {
        Transform target = playerTransform != null ? playerTransform : player;
        if (target == null) return;

        HashSet<Vector3Int> touchedThisFrame = new();

        Vector3 playerFeet = target.position;
        Vector3 playerBody = target.position + new Vector3(0f, playerTouchProbeOffsetY, 0f);

        foreach (var kv in growingTiles)
        {
            var pos = kv.Key;
            var tile = kv.Value;

            if (tile == null || tile.cropData == null) continue;
            if (tile.cropData.isTree) continue;
            if (tile.cropOverlayObject == null) continue;

            var sr = tile.cropOverlayObject.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) continue;

            Bounds touchBounds = sr.bounds;

            // 기본 범위를 살짝 넓힘
            touchBounds.Expand(new Vector3(cropTouchBoxPadding.x * 2f, cropTouchBoxPadding.y * 2f, 0f));

            // 위쪽(칸 밖으로 튀어나온 잎/줄기) 판정을 더 크게
            Vector3 min = touchBounds.min;
            Vector3 max = touchBounds.max + new Vector3(0f, cropTouchTopExtra, 0f);
            touchBounds.SetMinMax(min, max);

            // 플레이어 발 + 상체 두 점으로 체크
            bool isTouching =
                touchBounds.Contains(playerFeet) ||
                touchBounds.Contains(playerBody);

            if (!isTouching) continue;

            touchedThisFrame.Add(pos);

            // 들어온 순간에만 1회 흔들림
            if (!touchingCropTiles.Contains(pos))
            {
                touchingCropTiles.Add(pos);
                StartCoroutine(PlayTreeHarvestShake(tile.cropOverlayObject.transform));
            }
        }

        // 범위에서 벗어난 작물은 다시 흔들릴 수 있게 해제
        touchingCropTiles.RemoveWhere(pos => !touchedThisFrame.Contains(pos));
    }
}
