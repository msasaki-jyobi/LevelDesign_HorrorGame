using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// イベント用の画面UI。UIプレハブ管理（EventUISettings）に指定したプレハブを生成して使う：
    /// 会話ウィンドウ・選択肢・調べるプロンプト（照準）・所持品・フェード。
    /// シーンに1つ置く（EventKit > セットアップ > シーンにイベント基盤を配置）。
    /// </summary>
    public class EventHUD : MonoBehaviour
    {
        public static EventHUD Instance { get; private set; }

        [Tooltip("このシーンだけ別の UI を使うときに指定（未設定なら Resources/EventUISettings）")]
        public EventUISettings settingsOverride;
        [Tooltip("所持品ウィンドウを Inventory キーで開けるようにする")]
        public bool inventoryEnabled = true;

        DialogueWindow dialogue;
        ChoiceWindow choices;
        InteractPrompt prompt;
        InventoryWindow inventory;
        ScreenFader fader;

        /// <summary>直前の選択肢で選ばれた番号（0始まり）。</summary>
        public int LastChoice { get; private set; }
        int pendingChoice = -1;
        bool inventoryOpen, navHeld, advanceRequested;
        int inventoryIndex;

        public bool IsDialogueOpen => dialogue != null && dialogue.IsOpen;
        public bool IsInventoryOpen => inventoryOpen;
        public EventUISettings Settings => settingsOverride != null ? settingsOverride : EventUISettings.Instance;

        public DialogueWindow Dialogue => dialogue;
        public ChoiceWindow Choices => choices;
        public InteractPrompt Prompt => prompt;
        public InventoryWindow Inventory => inventory;
        public ScreenFader Fader => fader;

        void Awake()
        {
            Instance = this;
            EventInput.Ensure();
            Build();
        }

        void OnEnable() => GameState.Changed += RefreshInventory;
        void OnDisable() => GameState.Changed -= RefreshInventory;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (inventoryOpen) InputLock.Pop();
        }

        void Build()
        {
            var s = Settings;
            if (s == null)
            {
                Debug.LogWarning("[EventKit] UIプレハブ管理（Resources/EventUISettings）が見つかりません。メニュー EventKit > UIプレハブ管理 で作成してください。");
                return;
            }
            var root = EventUISettings.CreateCanvas(transform, "HUD Canvas", s.sortingOrder).transform;
            // 奥から順に：プロンプト → フェード → 会話 → 選択肢 → 所持品（暗転中も会話は見える）
            prompt = Spawn(s.prompt, root);
            fader = Spawn(s.fade, root);
            dialogue = Spawn(s.dialogue, root);
            choices = Spawn(s.choices, root);
            inventory = Spawn(s.inventory, root);

            if (prompt != null) prompt.Show(null);
            if (fader != null) fader.Alpha = 0f;
            if (dialogue != null) dialogue.Close();
            if (choices != null) choices.Close();
            if (inventory != null) inventory.Close();
        }

        static T Spawn<T>(T prefab, Transform parent) where T : Component
        {
            if (prefab == null) return null;
            var instance = Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            EventUIFont.Apply(instance.gameObject);
            return instance;
        }

        // ───────── 更新 ─────────

        void Update()
        {
            if (inventoryEnabled && inventory != null && EventInput.Inventory.WasPressedThisFrame() && (inventoryOpen || !EventRunner.IsBusy))
                ToggleInventory();

            if (inventoryOpen)
            {
                float y = EventInput.Move.ReadValue<Vector2>().y;
                if (Mathf.Abs(y) > 0.5f)
                {
                    if (!navHeld)
                    {
                        inventoryIndex += y > 0 ? -1 : 1;
                        navHeld = true;
                        RefreshInventory();
                    }
                }
                else navHeld = false;
            }

            if (prompt != null) prompt.SetVisible(!IsDialogueOpen && !inventoryOpen);
        }

        /// <summary>調べられる物を見ているときの行動名（null で消す）。</summary>
        public void SetPrompt(string action)
        {
            if (prompt == null) return;
            bool show = !string.IsNullOrEmpty(action) && !IsDialogueOpen && !inventoryOpen;
            prompt.Show(show ? action : null);
        }

        // ───────── 会話 ─────────

        public IEnumerator ShowMessage(string speaker, string text)
        {
            if (dialogue == null) yield break;
            if (prompt != null) prompt.Show(null);
            advanceRequested = false;
            yield return dialogue.Show(speaker, text, SubmitPressed);
        }

        /// <summary>会話を送る（UIボタン・タッチ操作・テスト用）。</summary>
        public void RequestAdvance() => advanceRequested = true;

        bool SubmitPressed()
        {
            bool pressed = EventInput.Submit.WasPressedThisFrame() || advanceRequested;
            advanceRequested = false;
            return pressed;
        }

        public void HideDialogue()
        {
            if (dialogue != null) dialogue.Close();
        }

        /// <summary>選択肢を出して、決定されるまで待つ。結果は LastChoice に入る。上下キー・スティックで選択、決定キーで確定。</summary>
        public IEnumerator ShowChoices(IList<string> options)
        {
            int count = options.Count;
            int index = 0;
            bool held = true;
            LastChoice = 0;
            if (choices == null || count == 0) yield break;
            if (prompt != null) prompt.Show(null);
            choices.Open(options);
            advanceRequested = false;
            yield return null; // 開始フレームの決定入力を無視する

            while (true)
            {
                float y = EventInput.Move.ReadValue<Vector2>().y;
                if (Mathf.Abs(y) > 0.5f)
                {
                    if (!held)
                    {
                        index = (index + (y > 0 ? -1 : 1) + count) % count;
                        held = true;
                        choices.Select(index);
                    }
                }
                else held = false;

                if (pendingChoice >= 0)
                {
                    index = Mathf.Clamp(pendingChoice, 0, count - 1);
                    pendingChoice = -1;
                    choices.Select(index);
                }

                if (SubmitPressed()) break;
                yield return null;
            }

            LastChoice = index;
            choices.Close();
        }

        /// <summary>選択肢の番号を外部から決める（UIボタン・テスト用）。次の決定でその番号が確定する。</summary>
        public void SelectChoice(int index) => pendingChoice = index;

        public IEnumerator Fade(float targetAlpha, float duration)
        {
            if (fader == null) yield break;
            yield return fader.FadeTo(targetAlpha, duration);
        }

        // ───────── 所持品 ─────────

        public void ToggleInventory()
        {
            if (inventory == null) return;
            inventoryOpen = !inventoryOpen;
            if (inventoryOpen)
            {
                inventory.Open();
                InputLock.Push();
            }
            else
            {
                inventory.Close();
                InputLock.Pop();
            }
            RefreshInventory();
        }

        void RefreshInventory()
        {
            if (!inventoryOpen || inventory == null) return;
            var items = GameState.Inventory;
            if (items.Count > 0) inventoryIndex = (inventoryIndex % items.Count + items.Count) % items.Count;
            inventory.Refresh(items, inventoryIndex);
        }
    }
}
