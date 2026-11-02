using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EventKit
{
    /// <summary>イベントコマンドを順番に実行する。同時に実行されるイベントは1つだけ。</summary>
    public class EventRunner : MonoBehaviour
    {
        static EventRunner instance;
        static int running;

        public static bool IsBusy => running > 0 || SceneTransfer.IsTransitioning;
        /// <summary>最後にイベントが終わったフレーム（決定キーの二重入力防止用）。</summary>
        public static int LastEndFrame { get; private set; } = -10;

        public static EventRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<EventRunner>();
                    if (instance == null) instance = new GameObject("[EventRunner]").AddComponent<EventRunner>();
                }
                return instance;
            }
        }

        [Tooltip("アイテム入手時に鳴らす音（任意）")]
        public AudioClip itemGetSound;

        AudioSource audioSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            running = 0;
            LastEndFrame = -10;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        public void Run(GameEvent source, int pageIndex)
        {
            if (IsBusy || source == null || pageIndex < 0 || pageIndex >= source.pages.Count) return;
            var page = source.pages[pageIndex];
            if (page.runOnce || page.trigger == EventTrigger.AutoStart)
                GameState.MarkPageFinished(source.PageKey(pageIndex));
            StartCoroutine(Routine(page.commands, source));
        }

        public void RunCommands(IList<EventCommand> commands)
        {
            if (IsBusy || commands == null) return;
            StartCoroutine(Routine(commands, null));
        }

        /// <summary>メッセージだけを表示する簡易イベント。</summary>
        public void ShowMessages(params string[] lines)
        {
            var list = new List<EventCommand>();
            foreach (var l in lines)
                if (!string.IsNullOrEmpty(l)) list.Add(new EventCommand { type = CommandType.Message, text = l });
            RunCommands(list);
        }

        IEnumerator Routine(IList<EventCommand> commands, GameEvent source)
        {
            running++;
            context = new EventContext(this, source);
            InputLock.Push();
            aborted = false;
            try
            {
                yield return RunList(commands, 0);
            }
            finally
            {
                if (EventHUD.Instance != null) EventHUD.Instance.HideDialogue();
                // 視点変更を解除し忘れても、イベントが終わったらプレイヤーの視点に戻す
                EventCamera.Reset(0.5f);
                InputLock.Pop();
                running--;
                LastEndFrame = Time.frameCount;
            }
        }

        bool aborted;
        EventContext context;

        // ───────── 追加パック用の処理の登録 ─────────

        static readonly Dictionary<CommandType, System.Func<EventCommand, IEnumerator>> handlers = new Dictionary<CommandType, System.Func<EventCommand, IEnumerator>>();

        /// <summary>
        /// 組み込みコマンドの処理を追加パックから登録する（例：懐中電灯の明滅）。
        /// 新しいコマンドを作るときは <see cref="CustomCommand"/> を使う方が簡単。
        /// </summary>
        public static void RegisterHandler(CommandType type, System.Func<EventCommand, IEnumerator> handler) => handlers[type] = handler;
        const int MaxCallDepth = 8;

        /// <summary>コマンド列を上から実行する。条件分岐・選択肢はブロックの印をたどって飛ぶ。</summary>
        IEnumerator RunList(IList<EventCommand> commands, int depth)
        {
            int i = 0;
            while (i < commands.Count && !aborted)
            {
                var c = commands[i];
                if (c == null) { i++; continue; }

                switch (c.type)
                {
                    case CommandType.EndEvent:
                        aborted = true;
                        yield break;

                    case CommandType.TransferScene:
                    case CommandType.TransferSceneLoading:
                        // シーンが切り替わるとこのイベントは消えるので、ここでイベントを終えて遷移に任せる
                        SceneTransfer.Begin(c.scenePath, c.spawnId, c.duration, c.fadeInDuration,
                            c.type == CommandType.TransferSceneLoading ? c.loadingPrefab : null, c.type == CommandType.TransferSceneLoading);
                        aborted = true;
                        yield break;

                    case CommandType.If:
                        // 条件を満たせば中へ。満たさなければ「それ以外のとき」の次か、分岐終了の次へ
                        if (c.condition == null || c.condition.IsMet()) i++;
                        else i = CommandTypes.ScanLevel(commands, i + 1, t => t == CommandType.Else) + 1;
                        continue;

                    case CommandType.Else:
                    case CommandType.When:
                        // 前の分岐を最後まで実行してここに来た → ブロックの終わりへ
                        i = CommandTypes.ScanLevel(commands, i + 1, null) + 1;
                        continue;

                    case CommandType.EndIf:
                    case CommandType.EndChoices:
                        i++;
                        continue;

                    case CommandType.ShowChoices:
                    {
                        int selected = 0;
                        var hud = EventHUD.Instance;
                        if (hud != null && c.choices != null && c.choices.Count > 0)
                        {
                            yield return hud.ShowChoices(c.choices);
                            selected = hud.LastChoice;
                        }
                        int j = i + 1;
                        while (true)
                        {
                            j = CommandTypes.ScanLevel(commands, j, t => t == CommandType.When);
                            if (j >= commands.Count || commands[j].type != CommandType.When) { i = j + 1; break; }
                            if (commands[j].choiceIndex == selected) { i = j + 1; break; }
                            j++;
                        }
                        continue;
                    }

                    case CommandType.CallEvent:
                        if (c.callEvent != null && depth < MaxCallDepth)
                        {
                            int page = c.callEvent.ActivePageIndex;
                            if (page >= 0)
                            {
                                var p = c.callEvent.pages[page];
                                if (p.runOnce || p.trigger == EventTrigger.AutoStart)
                                    GameState.MarkPageFinished(c.callEvent.PageKey(page));
                                yield return RunList(p.commands, depth + 1);
                            }
                        }
                        i++;
                        continue;
                }

                bool hides = c.type == CommandType.Custom ? c.custom != null && c.custom.HidesDialogue : HidesDialogue(c.type);
                if (hides && EventHUD.Instance != null) EventHUD.Instance.HideDialogue();
                yield return Execute(c);
                i++;
            }
        }

        static bool HidesDialogue(CommandType t) =>
            t == CommandType.JumpScare || t == CommandType.Wait || t == CommandType.FadeOut || t == CommandType.FadeIn
            || t == CommandType.LookAt || t == CommandType.CameraView || t == CommandType.CameraReset || t == CommandType.Warp;

        /// <summary>音を鳴らす。at を指定するとその位置から立体音響で鳴る（振り向きの合図に使う）。</summary>
        public void PlaySound(AudioClip clip, GameObject at = null)
        {
            if (clip == null) return;
            if (at == null)
            {
                audioSource.PlayOneShot(clip);
                return;
            }
            var go = new GameObject("[Sound] " + clip.name);
            go.transform.position = at.transform.position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = 30f;
            source.Play();
            Destroy(go, clip.length + 0.2f);
        }

        /// <summary>プレイヤーの視点を target に向ける（振り返り）。</summary>
        static IEnumerator TurnTo(GameObject target, float seconds)
        {
            var player = EventPlayer.Current;
            if (player == null || target == null) yield break;
            seconds = Mathf.Max(0.05f, seconds);
            yield return player.LookAt(target.transform, seconds, seconds);
        }

        IEnumerator Execute(EventCommand c)
        {
            var hud = EventHUD.Instance;
            switch (c.type)
            {
                case CommandType.Message:
                    // 振り向く先があれば：音（その位置から）→ 振り向く → 会話
                    PlaySound(c.clip, c.target);
                    if (c.target != null)
                    {
                        if (hud != null) hud.HideDialogue();
                        yield return TurnTo(c.target, c.duration);
                    }
                    if (hud != null) yield return hud.ShowMessage(c.speaker, c.text);
                    break;

                case CommandType.LookAt:
                    yield return TurnTo(c.target, c.duration);
                    break;

                case CommandType.GiveItem:
                    GameState.AddItem(c.itemId);
                    if (itemGetSound != null) audioSource.PlayOneShot(itemGetSound);
                    if (!c.silent && hud != null)
                        yield return hud.ShowMessage("", $"「{EventDatabase.ItemName(c.itemId)}」を手に入れた。");
                    break;

                case CommandType.RemoveItem:
                    GameState.RemoveItem(c.itemId);
                    if (!c.silent && hud != null)
                        yield return hud.ShowMessage("", $"「{EventDatabase.ItemName(c.itemId)}」を使った。");
                    break;

                case CommandType.SetFlag:
                    GameState.SetFlag(c.flagId, c.boolValue);
                    break;

                case CommandType.OpenDoor:
                    if (c.door != null) c.door.Open();
                    break;
                case CommandType.CloseDoor:
                    if (c.door != null) c.door.Close();
                    break;
                case CommandType.LockDoor:
                    if (c.door != null) c.door.Lock();
                    break;
                case CommandType.UnlockDoor:
                    if (c.door != null) c.door.Unlock();
                    break;

                case CommandType.JumpScare:
                    if (c.jumpScare is IEventPerformance performance) yield return performance.Play();
                    break;

                case CommandType.Custom:
                    if (c.custom != null) yield return c.custom.Run(context ?? new EventContext(this, null));
                    break;

                case CommandType.PlaySound:
                    if (c.clip != null)
                    {
                        PlaySound(c.clip, c.target);
                        if (c.boolValue) yield return new WaitForSeconds(c.clip.length);
                    }
                    break;

                case CommandType.FlickerFlashlight:
                    // 追加パック（HorrorKit 等）が RegisterHandler で処理を登録する
                    if (handlers.TryGetValue(c.type, out var flicker)) yield return flicker(c);
                    break;

                case CommandType.FadeOut:
                    if (hud != null) yield return hud.Fade(1f, c.duration);
                    break;
                case CommandType.FadeIn:
                    if (hud != null) yield return hud.Fade(0f, c.duration);
                    break;

                case CommandType.Wait:
                    yield return new WaitForSeconds(c.duration);
                    break;

                case CommandType.SetActive:
                    if (c.target != null) c.target.SetActive(c.boolValue);
                    break;
                case CommandType.ActivateObject:
                    if (c.target != null) c.target.SetActive(true);
                    break;
                case CommandType.DeactivateObject:
                    if (c.target != null) c.target.SetActive(false);
                    break;

                case CommandType.CameraView:
                    EventCamera.Show(c.viewPosition, Quaternion.Euler(c.viewRotation), c.viewFov, c.duration);
                    yield return WaitIfNeeded(c, WaitSeconds(c.duration));
                    break;
                case CommandType.CameraReset:
                    EventCamera.Reset(c.duration);
                    yield return WaitIfNeeded(c, WaitSeconds(c.duration));
                    break;
                case CommandType.CameraShake:
                    yield return WaitIfNeeded(c, EventCamera.Shake(c.duration, c.strength));
                    break;

                case CommandType.MoveObject:
                    if (c.target != null) yield return WaitIfNeeded(c, MoveRoutine(c.target.transform, c.vector, c.localSpace, c.duration));
                    break;
                case CommandType.RotateObject:
                    if (c.target != null) yield return WaitIfNeeded(c, RotateRoutine(c.target.transform, c.vector, c.rotateMode, c.duration));
                    break;
                case CommandType.ScaleObject:
                    if (c.target != null) yield return WaitIfNeeded(c, ScaleRoutine(c.target.transform, c.vector, c.duration));
                    break;

                case CommandType.PlayAnimation:
                    if (c.target != null) yield return WaitIfNeeded(c, AnimationRoutine(c.target, c.animMode, c.animName, c.animClip, c.duration));
                    break;

                case CommandType.InvokeUnityEvent:
                    c.unityEvent?.Invoke();
                    break;

                case CommandType.Warp:
                    // boolValue＝フェードあり
                    if (c.target != null)
                        yield return SceneTransfer.Warp(c.target.transform, c.boolValue ? c.duration : 0f, c.boolValue ? c.fadeInDuration : 0f);
                    break;
            }
        }

        // ───────── 時間のかかる演出 ─────────

        /// <summary>「終了するまで待機」なら終わるまで待ち、「そのまま次へ」なら並行して動かす。</summary>
        IEnumerator WaitIfNeeded(EventCommand c, IEnumerator routine)
        {
            if (c.waitMode == WaitMode.WaitForEnd) yield return routine;
            else StartCoroutine(routine);
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
        }

        /// <summary>0→1 をなめらかに進める（seconds が 0 なら即座に 1）。</summary>
        static IEnumerator Tween(float seconds, System.Action<float> apply)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                apply(Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            apply(1f);
        }

        static IEnumerator MoveRoutine(Transform tr, Vector3 offset, bool local, float seconds)
        {
            Vector3 start = tr.position;
            Vector3 delta = local ? tr.rotation * offset : offset;
            yield return Tween(seconds, k => { if (tr != null) tr.position = start + delta * k; });
        }

        static IEnumerator RotateRoutine(Transform tr, Vector3 euler, RotateMode mode, float seconds)
        {
            Quaternion startLocal = tr.localRotation, startWorld = tr.rotation;
            yield return Tween(seconds, k =>
            {
                if (tr == null) return;
                switch (mode)
                {
                    case RotateMode.Local: tr.localRotation = Quaternion.Slerp(startLocal, Quaternion.Euler(euler), k); break;
                    case RotateMode.World: tr.rotation = Quaternion.Slerp(startWorld, Quaternion.Euler(euler), k); break;
                    // 加算は 180 度を超える回転（一回転など）もそのまま回る
                    default: tr.localRotation = startLocal * Quaternion.Euler(euler * k); break;
                }
            });
        }

        /// <summary>
        /// モーション再生。fade 秒で切り替え、終わるまで（ループするモーションは1周）待つ。
        /// クリップを直接再生した場合は、終わったら元の Animator Controller の動きに戻る。
        /// </summary>
        static IEnumerator AnimationRoutine(GameObject target, AnimationPlayMode mode, string name, AnimationClip clip, float fade)
        {
            var animator = target.GetComponentInChildren<Animator>();
            if (mode == AnimationPlayMode.Clip)
            {
                if (clip == null) yield break;
                if (animator == null) animator = target.AddComponent<Animator>();
                var playable = UnityEngine.Playables.AnimationPlayableUtilities.PlayClip(animator, clip, out var graph);
                float end = Time.time + clip.length;
                while (Time.time < end && graph.IsValid()) yield return null;
                if (graph.IsValid()) graph.Destroy();
                yield break;
            }

            if (animator == null || string.IsNullOrEmpty(name)) yield break;
            if (mode == AnimationPlayMode.Trigger) animator.SetTrigger(name);
            else animator.CrossFadeInFixedTime(name, Mathf.Max(0f, fade), -1);

            // 切り替わるのを待つ（最大 fade + 1 秒）
            float limit = Time.time + fade + 1f;
            yield return null;
            while (animator != null && animator.IsInTransition(0) && Time.time < limit) yield return null;
            if (animator == null) yield break;

            // 今のステートが1周するまで待つ
            var info = animator.GetCurrentAnimatorStateInfo(0);
            float remain = info.length * (info.loop ? 1f : Mathf.Max(0f, 1f - info.normalizedTime));
            if (remain > 0f) yield return new WaitForSeconds(remain);
        }

        static IEnumerator ScaleRoutine(Transform tr, Vector3 scale, float seconds)
        {
            Vector3 start = tr.localScale;
            yield return Tween(seconds, k => { if (tr != null) tr.localScale = Vector3.LerpUnclamped(start, scale, k); });
        }
    }
}
