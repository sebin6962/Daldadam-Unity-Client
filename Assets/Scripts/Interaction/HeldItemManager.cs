using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeldItemManager : MonoBehaviour
{
    public static HeldItemManager Instance;

    public Image heldItemImage; 
    public Transform player;   

    [Header("Held Item Position")]
    [Tooltip("Player 위치를 기준으로 든 아이템의 위치를 조절합니다. Y 값을 낮추면 아이템이 아래로 내려갑니다.")]
    [SerializeField] private Vector2 heldItemPositionOffset = new Vector2(0f, 1.5f);

    private Sprite currentHeldSprite;
    private string heldItemName;

    private Vector2 baseSize;
    private bool baseSizeInitialized = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (heldItemImage != null)
        {
            RectTransform rt = heldItemImage.GetComponent<RectTransform>();
            if (rt != null)
            {
                baseSize = rt.sizeDelta;   
                baseSizeInitialized = true;
            }
        }
    }

    private void LateUpdate()
    {
        if (!IsHoldingItem())
        {
            if (heldItemImage.enabled)
                heldItemImage.enabled = false;
            return;
        }

        if (heldItemImage.enabled)
        {
            Vector3 offset = new Vector3(
                heldItemPositionOffset.x,
                heldItemPositionOffset.y,
                0f
            );
            heldItemImage.rectTransform.position = player.position + offset;

            //village2 튜토리얼 진행 트리거 3
            var tm = FindObjectOfType<TutorialManager>();
            if (tm != null &&
                tm.IsCurrentStep(VillageSecondStep.PickUpSeed) &&
                HeldItemManager.Instance.GetHeldItemName() == "Danhobak_seedBag")
            {
                tm.GoToNextVillageSecondStep();
            }

            var st = StoreTutorialManager.Instance;
            if (st != null &&
                st.IsCurrentStep(StoreTutorialStep.OpenStorage) && HeldItemManager.Instance.GetHeldItemName() == "Mepssalgaru")
            {
                st.GoToNextStep();
            }

            var ss = SecondStoreTutorialManager.Instance;
            if (ss != null &&
                ss.IsCurrentStep(SecondStoreTutorialStep.OpenStorage) && HeldItemManager.Instance.GetHeldItemName() == "Danhobakgaru")
            {
                ss.GoToNextStep();
            }
        }
    }

    public string GetHeldItemName()
    {
        return heldItemName;
    }

    public void ShowHeldItem(Sprite sprite, string itemName = null)
    {
        // NPC와 대화할 수 있는 범위이거나 대화 진행 중이면
        // 같은 E 입력으로 물뿌리개/아이템을 동시에 들지 않도록 차단한다.
        if (Input.GetKeyDown(KeyCode.E) &&
            NPCInteractable.BlocksOtherWorldInteraction)
            return;

        if (sprite == null)
        {
            Debug.LogWarning("ShowHeldItem: 스프라이트가 null입니다.");
            return;
        }
        if (heldItemImage == null)
        {
            Debug.LogError("heldItemImage가 연결되지 않았습니다. Inspector에서 Image를 드래그해 연결하세요.");
            return;
        }

        currentHeldSprite = sprite;
        heldItemName = itemName;

        heldItemImage.sprite = sprite;
        heldItemImage.enabled = true;

        RectTransform rt = heldItemImage.GetComponent<RectTransform>();
        if (rt != null)
        {
            if (!baseSizeInitialized)
            {
                baseSize = rt.sizeDelta;
                baseSizeInitialized = true;
            }

            rt.sizeDelta = baseSize;
        }

        heldItemImage.preserveAspect = true;

        Debug.Log("ShowHeldItem: 아이템 표시됨 - " + sprite.name);
    }

    public void HideHeldItem()
    {
        string prevItemName = heldItemName;

        heldItemImage.enabled = false;
        currentHeldSprite = null;
        heldItemName = null;

        //village2 튜토리얼 진행 트리거 5
        var tm = TutorialManager.Instance;
        if (tm != null &&
            tm.IsCurrentStep(VillageSecondStep.RestoreSeed) && prevItemName == "Danhobak_seedBag")
        {
            tm.GoToNextVillageSecondStep();
        }
    }

    public bool IsHoldingItem()
    {
        return currentHeldSprite != null;
    }

    public Sprite GetHeldItemSprite()
    {
        return currentHeldSprite;
    }

    public void SetHeldItemVisualVisible(bool visible)
    {
        if (heldItemImage == null) return;

        // 실제로 아이템을 들고 있을 때만 다시 보이게 한다.
        if (visible)
        {
            heldItemImage.enabled = IsHoldingItem();
        }
        else
        {
            heldItemImage.enabled = false;
        }
    }
}
