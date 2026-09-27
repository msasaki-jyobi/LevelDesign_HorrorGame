using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorKit
{
    /// <summary>イベントコマンドを順番に実行する。同時に実行されるイベントは1つだけ。</summary>
    public class EventRunner : MonoBehaviour
    {
        static EventRunner instance;
        static int running;

        public static bool IsBusy => running > 0;
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

        public void Run(HorrorEvent source, int pageIndex)
        {
            if (IsBusy || source == null || pageIndex < 0 || pageIndex >= source.pages.Count) return;
            var page = source.pages[pageIndex];
            if (page.runOnce || page.trigger == EventTrigger.AutoStart)
                GameState.MarkPageFinished(source.PageKey(pageIndex));
            StartCoroutine(Routine(page.commands));
        }

        public void RunCommands(IList<EventCommand> commands)
        {
            if (IsBusy || commands == null) return;
            StartCoroutine(Routine(commands));
        }

        /// <summary>メッセージだけを表示する簡易イベント。</summary>
        public void ShowMessages(params string[] lines)
        {
            var list = new List<EventCommand>();
            foreach (var l in lines)
                if (!string.IsNullOrEmpty(l)) list.Add(new EventCommand { type = CommandType.Message, text = l });
            RunCommands(list);
        }

        IEnumerator Routine(IList<EventCommand> commands)
        {
            running++;
            InputLock.Push();
            try
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    var c = commands[i];
                    if (c == null) continue;
                    if (c.type == CommandType.EndEvent) break;
                    if (HidesDialogue(c.type) && HorrorHUD.Instance != null) HorrorHUD.Instance.HideDialogue();
                    yield return Execute(c);
                }
            }
            finally
            {
                if (HorrorHUD.Instance != null) HorrorHUD.Instance.HideDialogue();
                InputLock.Pop();
                running--;
                LastEndFrame = Time.frameCount;
            }
        }

        static bool HidesDialogue(CommandType t) =>
            t == CommandType.JumpScare || t == CommandType.Wait || t == CommandType.FadeOut || t == CommandType.FadeIn;

        IEnumerator Execute(EventCommand c)
        {
            var hud = HorrorHUD.Instance;
            switch (c.type)
            {
                case CommandType.Message:
                    if (hud != null) yield return hud.ShowMessage(c.speaker, c.text);
                    break;

                case CommandType.GiveItem:
                    GameState.AddItem(c.itemId);
                    if (itemGetSound != null) audioSource.PlayOneShot(itemGetSound);
                    if (!c.silent && hud != null)
                        yield return hud.ShowMessage("", $"「{HorrorDatabase.ItemName(c.itemId)}」を手に入れた。");
                    break;

                case CommandType.RemoveItem:
                    GameState.RemoveItem(c.itemId);
                    if (!c.silent && hud != null)
                        yield return hud.ShowMessage("", $"「{HorrorDatabase.ItemName(c.itemId)}」を使った。");
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
                    if (c.jumpScare != null) yield return c.jumpScare.Play();
                    break;

                case CommandType.PlaySound:
                    if (c.clip != null)
                    {
                        audioSource.PlayOneShot(c.clip);
                        if (c.boolValue) yield return new WaitForSeconds(c.clip.length);
                    }
                    break;

                case CommandType.FlickerFlashlight:
                    if (Flashlight.Instance != null) Flashlight.Instance.Flicker(c.duration);
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
            }
        }
    }
}
