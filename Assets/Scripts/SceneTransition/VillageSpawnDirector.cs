using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using Cinemachine;

public class VillageSpawnDirector : MonoBehaviour
{
    [Tooltip("이 씬의 스폰 지점 매핑")]
    public SpawnPointCollection spawnPoints;

    [SerializeField] private Transform defaultSpawnPoint; 
    [SerializeField] private string playerActionMapName = "Player"; 

    private bool _spawnedOnce;

    void Awake()
    {
        if (spawnPoints == null)
            spawnPoints = GetComponent<SpawnPointCollection>();
    }

    void Start()
    {
        StartCoroutine(SpawnWhenReady());
    }

    private IEnumerator SpawnWhenReady()
    {
        // 1) 플레이어가 등장할 때까지 대기
        GameObject player = null;
        while ((player = GameObject.FindGameObjectWithTag("Player")) == null)
            yield return null;

        yield return null;

        PlayerManager playerManager = player.GetComponent<PlayerManager>();

        if (playerManager != null)
        {
            string currentScene = SceneManager.GetActiveScene().name;

            if (currentScene == "VillageScene")
            {
                playerManager.SetFacing(PlayerManager.InitialFacing.Down);
            }
            else if (currentScene == "TreeScene")
            {
                playerManager.SetFacing(PlayerManager.InitialFacing.Up);
            }
            else
            {
                playerManager.SetFacing(PlayerManager.InitialFacing.Up);
            }
        }

        // 2) entranceID 확보 (SceneTransitionInfo 우선, 없으면 PlayerPrefs 폴백)
        string id = null;
        SceneTransitionInfo info = null;

        const int maxFrames = 2;
        for (int f = 0; f < maxFrames; f++)
        {
            info = SceneTransitionInfo.Instance;
            if (info != null && !string.IsNullOrEmpty(info.entranceID))
            {
                id = info.entranceID;
                break;
            }

            var pf = PlayerPrefs.GetString("__entranceID", "");
            if (!string.IsNullOrEmpty(pf))
            {
                id = pf;
                break;
            }

            yield return null;
        }

        if (string.IsNullOrEmpty(id))
        {
            bool restored =
                playerManager != null &&
                playerManager.TryRestoreSavedLocationForCurrentScene();

            if (!restored && defaultSpawnPoint != null)
            {
                Vector3 defaultPosition = defaultSpawnPoint.position;
                defaultPosition.z = 0f;
                player.transform.position = defaultPosition;
            }

            var directVcam =
                FindObjectOfType<CinemachineVirtualCamera>();

            if (directVcam != null)
                directVcam.PreviousStateIsValid = false;

            Debug.Log(
                restored
                    ? "[VillageSpawnDirector] 저장 위치 복원 완료"
                    : "[VillageSpawnDirector] 저장 위치 없음 → 기본 위치 적용"
            );

            Time.timeScale = 1f;

#if ENABLE_INPUT_SYSTEM
    var pi = FindObjectOfType<UnityEngine.InputSystem.PlayerInput>();
    if (pi != null)
    {
        if (!pi.enabled) pi.enabled = true;
        if (!string.IsNullOrEmpty(playerActionMapName) &&
            (pi.currentActionMap == null || pi.currentActionMap.name != playerActionMapName))
        {
            pi.SwitchCurrentActionMap(playerActionMapName);
        }
    }
#endif

            // 브리지 키 정리(혹시 남아 있던 값 제거)
            PlayerPrefs.DeleteKey("__entranceID");
            PlayerPrefs.DeleteKey("__fromScene");
            PlayerPrefs.DeleteKey("__toScene");

            _spawnedOnce = true;
            yield break;
        }

        // 3) 스폰 지점 찾기 (매핑 우선, 실패 시 이름 탐색 폴백)
        if (spawnPoints == null) spawnPoints = GetComponent<SpawnPointCollection>();

        Transform t = null;
        if (spawnPoints != null && spawnPoints.TryGet(id, out var mapped) && mapped != null)
            t = mapped;
        else
        {
            var go = GameObject.Find(id); // 최종 폴백
            if (go != null) t = go.transform;
        }

        if (t != null)
        {
            var pos = t.position; pos.z = 0f;
            player.transform.position = pos;


            var vcam = FindObjectOfType<CinemachineVirtualCamera>();
            if (vcam) vcam.PreviousStateIsValid = false;

            // 4) 값 소비 및 정리
            if (info != null) info.entranceID = null;
            PlayerPrefs.DeleteKey("__entranceID");
            PlayerPrefs.DeleteKey("__fromScene");
            PlayerPrefs.DeleteKey("__toScene");
        }
        else
        {
            Debug.LogWarning($"[VillageSpawnDirector] 스폰 실패: id={id}");
        }
    }
}


