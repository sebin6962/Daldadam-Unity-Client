using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ScenePortal : MonoBehaviour
{
    [Header("이 문을 통해 전환될 씬 이름")]
    public string targetScene;

    [Header("도착 씬의 스폰 지점 이름")]
    public string entranceID;

    [Header("상호작용 UI")]
    [SerializeField] private GameObject interactionUI;

    private bool isInTrigger = false;

    [SerializeField] private float sfxLead = 0.06f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            isInTrigger = true;

        //UI

        if (interactionUI != null)
            interactionUI.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isInTrigger = false;

            //UI
            if (interactionUI != null)
                interactionUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isInTrigger) return;
        if (!Input.GetKeyDown(KeyCode.E)) return;

        // 아이템을 들고 있으면 씬 이동과 효과음을 모두 실행하지 않는다.
        if (HeldItemManager.Instance != null &&
            HeldItemManager.Instance.IsHoldingItem())
        {
            Debug.Log("[Portal] 아이템을 내려놓아야 이동할 수 있습니다.");
            return;
        }

        var flow = TutorialFlowManager.Instance;

        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "VillageScene" && targetScene == "MillScene")
        {
            var tutMgr = TutorialManager.Instance;

            if (flow != null &&
                tutMgr != null &&
                flow.currentStep == GlobalTutorialStep.Village_Second &&
                tutMgr.IsVillageSecondTutorialRunning &&
                tutMgr.IsCurrentStep(VillageSecondStep.GoToMill))
            {
                Debug.Log("VillageSecond 튜토리얼 완료");
                tutMgr.CompleteVillageSecondTutorial();
            }
        }

        //PlayerStoreScene에서 나갈 때 Store튜토리얼 종료 처리
        if (currentScene == "PlayerStoreScene" && StoreTutorialManager.Instance.IsCurrentStep(StoreTutorialStep.StoreFirst_Finish))
        {
            var storeTut = StoreTutorialManager.Instance;

            if (flow != null && storeTut != null)
            {
                Debug.Log($"[Portal] E pressed in PlayerStoreScene, " +
                          $"flowStep={flow.currentStep}, storeRunning={storeTut.IsStoreTutorialRunning}, step={storeTut.currentStep}");

                if (flow.currentStep == GlobalTutorialStep.PlayerStore_First &&
                    storeTut.IsStoreTutorialRunning)
                {
                    storeTut.CompleteStoreTutorial();
                }
            }
        }


        //튜토리얼 진행중일때 scene이동 막기
        if (flow != null && flow.IsScenePortalLocked)
            return;

        // 씬이 바뀌기 전에 현재 위치와 방향을 저장한다.
        var playerManager = FindObjectOfType<PlayerManager>();
        if (playerManager != null)
            playerManager.SaveCurrentLocation();

        // 1 전역 전환 정보
        if (SceneTransitionInfo.Instance != null)
        {
            SceneTransitionInfo.Instance.fromScene = SceneManager.GetActiveScene().name;
            SceneTransitionInfo.Instance.toScene = targetScene;
            SceneTransitionInfo.Instance.entranceID = entranceID;
        }

        // 2 세이프 브리지
        PlayerPrefs.SetString("__fromScene", SceneManager.GetActiveScene().name);
        PlayerPrefs.SetString("__toScene", targetScene ?? "");
        PlayerPrefs.SetString("__entranceID", entranceID ?? "");
        PlayerPrefs.Save();

        //DogamIntro -> PlayerStore_first 튜토리얼 스텝 설정
        var flow2 = TutorialFlowManager.Instance;
        if (flow2 != null && flow2.currentStep != GlobalTutorialStep.Done)
        {
            if (targetScene == "PlayerStoreScene" && flow2.currentStep == GlobalTutorialStep.Village_First)
            {
                flow2.SetStep(GlobalTutorialStep.PlayerStore_First);
            }
        }

        if (currentScene == "PlayerStoreScene")
        {
            FindObjectOfType<CustomerSpawner>()?.SetAllowNewCustomers(false);
        }

        StartCoroutine(PlaySfxThenFade());

    }

    private IEnumerator PlaySfxThenFade()
    {

        if (SFXManager.Instance != null)
        {
            // 현재 씬(출발 씬)
            string fromScene = SceneManager.GetActiveScene().name;

            if (targetScene == "TreeScene" || fromScene == "TreeScene")
            {
                SFXManager.Instance.PlayTreeEnterSFX();
            }
            else
            {
                SFXManager.Instance.PlayDoorOpenSFX();
            }
        }

        // FadeManager가 timescale을 건드려도 안전하게: 실시간 대기
        if (sfxLead > 0f) yield return new WaitForSecondsRealtime(sfxLead);

        Debug.Log($"[Portal] to={targetScene}, id={entranceID}");
        FadeManager.Instance.FadeToScene(targetScene, 0.5f);
    }
}

