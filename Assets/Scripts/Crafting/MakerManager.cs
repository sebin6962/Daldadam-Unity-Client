using System;
using System.Collections.Generic;
using UnityEngine;

public class MakerManager : MonoBehaviour
{
    private string loadedServer = "";
    private bool isRestoring;

    private void Start()
    {
        if (!SaveService.HasCurrentSave &&
            !SaveService.LoadSelectedSave())
        {
            Debug.LogWarning(
                "[MakerManager] 불러올 저장 슬롯이 없습니다."
            );

            return;
        }

        loadedServer = SaveService.CurrentServer;
        LoadMakerState();
    }

    private void OnDisable()
    {
        SaveCurrentSceneStateIfSafe();
    }

    private void OnApplicationQuit()
    {
        SaveCurrentSceneStateIfSafe();
    }

    public bool SaveMakerState()
    {
        if (isRestoring)
            return true;

        if (!SaveService.HasCurrentSave)
            return false;

        if (!string.IsNullOrEmpty(loadedServer) &&
            !SaveService.IsCurrent(loadedServer))
        {
            Debug.LogWarning(
                "[MakerManager] 다른 슬롯으로 전환된 뒤 이전 제작대가 " +
                "저장되는 것을 차단했습니다."
            );

            return false;
        }

        MakerInfo[] makersInScene =
            FindObjectsOfType<MakerInfo>();

        // 제작대가 없는 씬에서 기존 데이터를 빈 목록으로 덮어쓰지 않는다.
        if (makersInScene.Length == 0)
        {
            Debug.Log(
                "[MakerManager] 씬에 MakerInfo가 없어 저장을 생략합니다."
            );

            return false;
        }

        MakerSaveData data = new MakerSaveData();

        foreach (MakerInfo maker in makersInScene)
        {
            if (maker == null ||
                string.IsNullOrWhiteSpace(maker.makerId))
            {
                continue;
            }

            bool hasInput =
                maker.inputItemNames != null &&
                maker.inputItemNames.Count > 0;

            bool hasResult = maker.currentResultObject != null;

            if (!hasInput && !hasResult && !maker.isProducing)
                continue;

            data.makers.Add(new MakerSlotSave
            {
                makerId = maker.makerId,
                inputItemNames = hasInput
                    ? new List<string>(maker.inputItemNames)
                    : new List<string>(),
                isProducing = maker.isProducing,
                resultItemName = maker.resultItemName,
                craftEndUtcSeconds = maker.craftEndUtcSeconds
            });
        }

        SaveService.CurrentData.makerData = data;
        SaveService.CurrentData.makerMigrationCompleted = true;

        bool saved = SaveService.SaveCurrent();

        if (saved)
        {
            Debug.Log(
                $"[MakerManager] 제작대 {data.makers.Count}개 저장 완료"
            );
        }

        return saved;
    }

    public void LoadMakerState()
    {
        if (!SaveService.HasCurrentSave)
            return;

        loadedServer = SaveService.CurrentServer;

        MakerSaveData data =
            SaveService.CurrentData.makerData ??
            new MakerSaveData();

        if (data.makers == null)
            data.makers = new List<MakerSlotSave>();

        SaveService.CurrentData.makerData = data;

        double nowUtc =
            (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        MakerInfo[] makersInScene =
            FindObjectsOfType<MakerInfo>();

        Dictionary<string, MakerInfo> makerMap =
            new Dictionary<string, MakerInfo>();

        foreach (MakerInfo maker in makersInScene)
        {
            if (maker == null ||
                string.IsNullOrWhiteSpace(maker.makerId))
            {
                continue;
            }

            if (makerMap.ContainsKey(maker.makerId))
            {
                Debug.LogError(
                    "[MakerManager] 중복 makerId 발견: " +
                    maker.makerId
                );

                continue;
            }

            makerMap.Add(maker.makerId, maker);
        }

        isRestoring = true;

        try
        {
            foreach (MakerSlotSave savedMaker in data.makers)
            {
                if (savedMaker == null ||
                    string.IsNullOrWhiteSpace(savedMaker.makerId) ||
                    !makerMap.TryGetValue(
                        savedMaker.makerId,
                        out MakerInfo maker
                    ))
                {
                    continue;
                }

                RestoreInputItems(maker, savedMaker.inputItemNames);

                maker.isProducing = savedMaker.isProducing;
                maker.resultItemName = savedMaker.resultItemName;
                maker.craftEndUtcSeconds =
                    savedMaker.craftEndUtcSeconds;

                if (savedMaker.isProducing &&
                    !string.IsNullOrWhiteSpace(savedMaker.resultItemName))
                {
                    RestoreProducingMaker(
                        maker,
                        savedMaker,
                        nowUtc
                    );
                }
                else if (!savedMaker.isProducing &&
                         !string.IsNullOrWhiteSpace(
                             savedMaker.resultItemName
                         ))
                {
                    RestoreCompletedResult(maker, savedMaker);
                }
            }
        }
        finally
        {
            isRestoring = false;
        }

        Debug.Log(
            $"[MakerManager] 제작대 {data.makers.Count}개 불러오기 완료"
        );
    }

    private void SaveCurrentSceneStateIfSafe()
    {
        if (string.IsNullOrEmpty(loadedServer) ||
            !SaveService.IsCurrent(loadedServer))
        {
            return;
        }

        SaveMakerState();
    }

    private static void RestoreInputItems(
        MakerInfo maker,
        List<string> itemNames
    )
    {
        maker.inputItemNames = itemNames != null
            ? new List<string>(itemNames)
            : new List<string>();

        maker.inputItemSprites.Clear();

        foreach (string itemName in maker.inputItemNames)
        {
            Sprite sprite = Resources.Load<Sprite>(
                $"Sprites/Ingredients/{itemName}"
            );

            maker.inputItemSprites.Add(sprite);

            if (sprite == null)
            {
                Debug.LogWarning(
                    "[MakerManager] 재료 스프라이트 로드 실패: " +
                    itemName
                );
            }
        }

        maker.EnsureSlotUIInstance();

        if (maker.slotUIManager == null)
            return;

        if (maker.inputItemSprites.Count > 0)
        {
            maker.slotUIManager.transform.position =
                maker.transform.position +
                new Vector3(0f, 1f, 0f);

            maker.slotUIManager.gameObject.SetActive(true);
            maker.slotUIManager.UpdateSlots(maker.inputItemSprites);
        }
        else
        {
            maker.slotUIManager.ClearSlots();
            maker.slotUIManager.gameObject.SetActive(false);
        }
    }

    private static void RestoreProducingMaker(
        MakerInfo maker,
        MakerSlotSave savedMaker,
        double nowUtc
    )
    {
        Sprite resultSprite = Resources.Load<Sprite>(
            $"Sprites/Ingredients/{savedMaker.resultItemName}"
        );

        if (resultSprite == null)
        {
            Debug.LogWarning(
                "[MakerManager] 제작 결과 스프라이트 로드 실패: " +
                savedMaker.resultItemName
            );

            maker.isProducing = false;
            return;
        }

        double remainingSeconds =
            savedMaker.craftEndUtcSeconds - nowUtc;

        float remainingDuration = remainingSeconds <= 0d
            ? 0.01f
            : Mathf.Max(0.01f, (float)remainingSeconds);

        maker.StartCraft(
            resultSprite,
            remainingDuration,
            force: true
        );
    }

    private static void RestoreCompletedResult(
        MakerInfo maker,
        MakerSlotSave savedMaker
    )
    {
        if (maker.currentResultObject != null)
            return;

        Sprite resultSprite = Resources.Load<Sprite>(
            $"Sprites/Ingredients/{savedMaker.resultItemName}"
        );

        if (resultSprite == null || maker.resultItemPrefab == null)
        {
            Debug.LogWarning(
                "[MakerManager] 저장된 제작 결과 복원 실패: " +
                savedMaker.resultItemName
            );

            return;
        }

        Vector3 resultPosition =
            maker.transform.position + new Vector3(0f, 1.2f, 0f);

        GameObject resultObject = Instantiate(
            maker.resultItemPrefab,
            resultPosition,
            Quaternion.identity
        );

        SpriteRenderer spriteRenderer =
            resultObject.GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            spriteRenderer.sprite = resultSprite;

        maker.currentResultObject = resultObject;
        maker.SpawnCompleteEffect();
    }
}

[Serializable]
public class MakerSlotSave
{
    public string makerId;
    public List<string> inputItemNames = new List<string>();
    public bool isProducing;
    public string resultItemName;
    public double craftEndUtcSeconds;
}

[Serializable]
public class MakerSaveData
{
    public List<MakerSlotSave> makers = new List<MakerSlotSave>();
}
