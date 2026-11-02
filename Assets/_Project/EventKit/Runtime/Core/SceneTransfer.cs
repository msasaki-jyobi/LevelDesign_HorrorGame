using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EventKit
{
    /// <summary>
    /// シーン遷移（フェードアウト → 読み込み → 出現地点へ移動 → フェードイン）。
    /// 遷移中もシーンをまたいで残る（DontDestroyOnLoad）。フラグ・所持品は GameState にあるので引き継がれる。
    /// ローディング用UIプレハブを渡すと、読み込みの間それを表示する。
    /// </summary>
    public class SceneTransfer : MonoBehaviour
    {
        static SceneTransfer instance;

        /// <summary>遷移中か（この間はイベントも起動しない）</summary>
        public static bool IsTransitioning { get; private set; }

        /// <summary>ローディング画面を最低これだけは表示する（一瞬だけ映るのを防ぐ）</summary>
        public const float MinLoadingSeconds = 0.6f;

        CanvasGroup fade;
        Canvas loadingCanvas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            IsTransitioning = false;
        }

        static SceneTransfer Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[SceneTransfer]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<SceneTransfer>();
                    instance.Build();
                }
                return instance;
            }
        }

        void Build()
        {
            // いちばん手前に出る幕（UIプレハブ管理のフェードを使う。無ければ黒い幕を作る）
            var fadeCanvas = EventUISettings.CreateCanvas(transform, "Fade", 5000);
            var settings = EventUISettings.Instance;
            if (settings != null && settings.fade != null)
            {
                var f = Instantiate(settings.fade, fadeCanvas.transform, false);
                fade = f.GetComponent<CanvasGroup>();
            }
            else
            {
                var black = new GameObject("Black", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                black.transform.SetParent(fadeCanvas.transform, false);
                var rt = (RectTransform)black.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                black.GetComponent<Image>().color = Color.black;
                fade = black.GetComponent<CanvasGroup>();
            }
            fade.alpha = 0f;
            fade.blocksRaycasts = false;

            // ローディングUIを載せる場所（幕より手前）
            loadingCanvas = EventUISettings.CreateCanvas(transform, "Loading", 5001);
        }

        /// <summary>
        /// 遷移を始める。scenePath は "Assets/…/Name.unity"。spawnId が空ならシーンの初期位置のまま。
        /// loadingPrefab を指定すると読み込み中に表示する。
        /// </summary>
        public static void Begin(string scenePath, string spawnId, float fadeOutSeconds, float fadeInSeconds, GameObject loadingPrefab = null, bool useDefaultLoading = false)
        {
            if (IsTransitioning || string.IsNullOrEmpty(scenePath)) return;
            var t = Instance;
            t.StartCoroutine(t.Routine(scenePath, spawnId, fadeOutSeconds, fadeInSeconds, loadingPrefab, useDefaultLoading));
        }

        IEnumerator Routine(string scenePath, string spawnId, float fadeOut, float fadeIn, GameObject loadingPrefab, bool useDefaultLoading)
        {
            IsTransitioning = true;
            InputLock.Push();
            try
            {
                yield return FadeTo(1f, fadeOut);

                GameObject loadingUI = null;
                LoadingScreen screen = null;
                // コマンドにプレハブが無ければ UIプレハブ管理のロード画面を使う
                if (loadingPrefab == null && useDefaultLoading && EventUISettings.Instance != null && EventUISettings.Instance.loading != null)
                    loadingPrefab = EventUISettings.Instance.loading.gameObject;
                if (loadingPrefab != null)
                {
                    loadingUI = Instantiate(loadingPrefab);
                    var ownCanvas = loadingUI.GetComponent<Canvas>();
                    if (ownCanvas != null)
                    {
                        // 自前の Canvas を持つプレハブは、そのまま一番手前に出す
                        DontDestroyOnLoad(loadingUI);
                        ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        ownCanvas.sortingOrder = 5001;
                    }
                    else
                    {
                        loadingUI.transform.SetParent(loadingCanvas.transform, false);
                    }
                    screen = loadingUI.GetComponentInChildren<LoadingScreen>(true);
                    if (screen != null) screen.SetProgress(0f);
                }

                float started = Time.unscaledTime;
                var op = LoadAsync(scenePath);
                if (op != null)
                {
                    while (!op.isDone)
                    {
                        if (screen != null) screen.SetProgress(Mathf.Clamp01(op.progress / 0.9f));
                        yield return null;
                    }
                }
                else Debug.LogError($"[EventKit] シーンを読み込めません: {scenePath}（Build Settings に入っているか確認してください）");

                if (screen != null) screen.SetProgress(1f);
                if (loadingUI != null)
                    while (Time.unscaledTime - started < MinLoadingSeconds) yield return null;

                // 新しいシーンの Awake/Start を待ってから出現地点へ
                yield return null;
                MovePlayerToSpawn(spawnId);
                yield return null;

                if (loadingUI != null) Destroy(loadingUI);
                yield return FadeTo(0f, fadeIn);
            }
            finally
            {
                InputLock.Pop();
                IsTransitioning = false;
            }
        }

        /// <summary>同じシーンの中で、to の位置・向き（Y回転）へプレイヤーを移す。秒数 0 ならフェードなし。</summary>
        public static IEnumerator Warp(Transform to, float fadeOutSeconds, float fadeInSeconds)
        {
            var player = EventPlayer.Current;
            if (to == null || player == null) yield break;
            var t = Instance;
            if (fadeOutSeconds > 0f) yield return t.FadeTo(1f, fadeOutSeconds);
            player.Teleport(to.position, to.eulerAngles.y);
            yield return null;
            if (fadeInSeconds > 0f || t.fade.alpha > 0f) yield return t.FadeTo(0f, fadeInSeconds);
        }

        static AsyncOperation LoadAsync(string scenePath)
        {
#if UNITY_EDITOR
            // エディタでは Build Settings に入っていなくても読み込めるようにする
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(scenePath) != null)
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
        }

        static void MovePlayerToSpawn(string spawnId)
        {
            if (string.IsNullOrEmpty(spawnId)) return;
            var player = EventPlayer.Current;
            if (player == null) return;
            foreach (var sp in FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude))
            {
                if (sp.spawnId != spawnId) continue;
                player.Teleport(sp.transform.position, sp.transform.eulerAngles.y);
                return;
            }
            Debug.LogWarning($"[EventKit] 出現地点「{spawnId}」が遷移先シーンにありません。");
        }

        IEnumerator FadeTo(float target, float seconds)
        {
            float from = fade.alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                fade.alpha = Mathf.Lerp(from, target, t / seconds);
                yield return null;
            }
            fade.alpha = target;
        }
    }
}
