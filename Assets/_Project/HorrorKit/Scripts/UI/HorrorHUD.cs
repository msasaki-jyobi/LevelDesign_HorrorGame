using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorKit
{
    /// <summary>
    /// 画面UI一式（照準・調べるプロンプト・会話ウィンドウ・所持品・フェード）を実行時に生成する。
    /// 日本語はOSのフォント（游ゴシック/メイリオ等）で表示する。任意のフォントを font に指定してもよい。
    /// </summary>
    public class HorrorHUD : MonoBehaviour
    {
        public static HorrorHUD Instance { get; private set; }

        [Header("フォント（未設定ならOSの日本語フォント）")]
        public Font font;
        public string[] osFontNames = { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic", "Arial" };

        [Header("会話")]
        public float charactersPerSecond = 32f;
        public int messageFontSize = 34;
        public Color speakerColor = new Color(1f, 0.82f, 0.62f);
        public Color panelColor = new Color(0f, 0f, 0f, 0.8f);
        public AudioClip typeSound;

        [Header("プロンプト")]
        public string interactKeyLabel = "E";

        Text prompt, speakerText, bodyText, nextIndicator, inventoryList, inventoryDescription;
        Image crosshair, fade;
        GameObject dialogueRoot, speakerBox, inventoryRoot;
        AudioSource audioSource;
        bool inventoryOpen, navHeld, advanceRequested;
        int inventoryIndex;

        public bool IsDialogueOpen => dialogueRoot != null && dialogueRoot.activeSelf;
        public bool IsInventoryOpen => inventoryOpen;

        void Awake()
        {
            Instance = this;
            HorrorInput.Ensure();
            if (font == null) font = Font.CreateDynamicFontFromOSFont(osFontNames, 32);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            Build();
        }

        void OnEnable() => GameState.Changed += RefreshInventory;
        void OnDisable() => GameState.Changed -= RefreshInventory;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (inventoryOpen) InputLock.Pop();
        }

        // ───────── UI 構築 ─────────

        void Build()
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // 照準
            crosshair = NewImage("Crosshair", root, new Color(1f, 1f, 1f, 0.45f));
            Place(crosshair.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));

            // 調べるプロンプト
            prompt = NewText("Prompt", root, 28, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.92f));
            Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(800, 50));
            prompt.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            prompt.gameObject.SetActive(false);

            // フェード（会話ウィンドウより下＝暗転中も文字は見える）
            fade = NewImage("Fade", root, new Color(0, 0, 0, 0));
            Stretch(fade.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 会話ウィンドウ
            var panel = NewImage("Dialogue", root, panelColor);
            dialogueRoot = panel.gameObject;
            Stretch(panel.rectTransform, new Vector2(0.14f, 0.04f), new Vector2(0.86f, 0.27f), Vector2.zero, Vector2.zero);
            var topLine = NewImage("TopLine", panel.transform, new Color(0.6f, 0.15f, 0.12f, 0.8f));
            Stretch(topLine.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0, -2), Vector2.zero);

            var sBox = NewImage("SpeakerBox", panel.transform, new Color(0.22f, 0.02f, 0.02f, 0.92f));
            speakerBox = sBox.gameObject;
            var sRect = sBox.rectTransform;
            sRect.anchorMin = sRect.anchorMax = new Vector2(0f, 1f);
            sRect.pivot = new Vector2(0f, 0f);
            sRect.anchoredPosition = new Vector2(24, 4);
            sRect.sizeDelta = new Vector2(320, 50);
            speakerText = NewText("Speaker", sBox.transform, 28, TextAnchor.MiddleCenter, speakerColor);
            Stretch(speakerText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            bodyText = NewText("Body", panel.transform, messageFontSize, TextAnchor.UpperLeft, new Color(0.93f, 0.93f, 0.9f));
            bodyText.lineSpacing = 1.15f;
            Stretch(bodyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(48, 28), new Vector2(-48, -34));

            nextIndicator = NewText("Next", panel.transform, 26, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.8f));
            nextIndicator.text = "▼";
            Stretch(nextIndicator.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 14), new Vector2(-28, 0));
            dialogueRoot.SetActive(false);

            // 所持品
            var inv = NewImage("Inventory", root, new Color(0f, 0f, 0f, 0.88f));
            inventoryRoot = inv.gameObject;
            Stretch(inv.rectTransform, new Vector2(0.6f, 0.18f), new Vector2(0.95f, 0.88f), Vector2.zero, Vector2.zero);
            var title = NewText("Title", inv.transform, 32, TextAnchor.UpperLeft, speakerColor);
            title.text = "所持品　<size=22><color=#888888>[Tab] 閉じる　[W/S] 選択</color></size>";
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(32, -70), new Vector2(-32, -20));
            inventoryList = NewText("List", inv.transform, 30, TextAnchor.UpperLeft, Color.white);
            inventoryList.lineSpacing = 1.3f;
            Stretch(inventoryList.rectTransform, new Vector2(0, 0.38f), Vector2.one, new Vector2(40, 0), new Vector2(-32, -90));
            inventoryDescription = NewText("Description", inv.transform, 24, TextAnchor.UpperLeft, new Color(0.75f, 0.75f, 0.72f));
            Stretch(inventoryDescription.rectTransform, Vector2.zero, new Vector2(1, 0.36f), new Vector2(32, 24), new Vector2(-32, 0));
            inventoryRoot.SetActive(false);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        Text NewText(string name, Transform parent, int size, TextAnchor anchor, Color color)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        // ───────── 更新 ─────────

        void Update()
        {
            if (HorrorInput.Inventory.WasPressedThisFrame() && (inventoryOpen || !EventRunner.IsBusy)) ToggleInventory();

            if (inventoryOpen)
            {
                float y = HorrorInput.Move.ReadValue<Vector2>().y;
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

            crosshair.enabled = !IsDialogueOpen && !inventoryOpen;
            if (nextIndicator.enabled) nextIndicator.color = new Color(1f, 1f, 1f, 0.4f + 0.4f * Mathf.PingPong(Time.time * 2f, 1f));
        }

        public void SetPrompt(string action)
        {
            bool show = !string.IsNullOrEmpty(action) && !IsDialogueOpen && !inventoryOpen;
            prompt.gameObject.SetActive(show);
            if (show) prompt.text = $"<color=#d9c38c>[{interactKeyLabel}]</color> {action}";
            crosshair.rectTransform.sizeDelta = show ? new Vector2(12, 12) : new Vector2(6, 6);
        }

        // ───────── 会話 ─────────

        public IEnumerator ShowMessage(string speaker, string text)
        {
            text = text ?? "";
            dialogueRoot.SetActive(true);
            prompt.gameObject.SetActive(false);
            speakerBox.SetActive(!string.IsNullOrEmpty(speaker));
            speakerText.text = speaker;
            bodyText.text = "";
            nextIndicator.enabled = false;
            advanceRequested = false;

            yield return null; // 開始フレームの決定入力を無視する

            float shown = 0f;
            int lastCount = 0;
            while (lastCount < text.Length)
            {
                if (SubmitPressed()) shown = text.Length;
                else shown += charactersPerSecond * Time.deltaTime;

                int count = Mathf.Min(text.Length, Mathf.FloorToInt(shown));
                count = SkipRichTextTags(text, count);
                if (count != lastCount)
                {
                    bodyText.text = CloseOpenTags(text.Substring(0, count));
                    if (typeSound != null && count > lastCount && !char.IsWhiteSpace(text[count - 1])) audioSource.PlayOneShot(typeSound, 0.5f);
                    lastCount = count;
                }
                yield return null;
            }

            nextIndicator.enabled = true;
            yield return null;
            while (!SubmitPressed()) yield return null;
            nextIndicator.enabled = false;
        }

        /// <summary>会話を送る（UIボタン・タッチ操作・テスト用）。</summary>
        public void RequestAdvance() => advanceRequested = true;

        bool SubmitPressed()
        {
            bool pressed = HorrorInput.Submit.WasPressedThisFrame() || advanceRequested;
            advanceRequested = false;
            return pressed;
        }

        /// <summary>途中表示でリッチテキストのタグが切れないよう、タグの途中なら閉じ '>' まで進める。</summary>
        static int SkipRichTextTags(string text, int count)
        {
            int open = text.LastIndexOf('<', Mathf.Max(0, count - 1));
            if (open >= 0 && open < count)
            {
                int close = text.IndexOf('>', open);
                if (close >= count) return Mathf.Min(text.Length, close + 1);
            }
            return count;
        }

        /// <summary>途中まで表示した文字列で開いたままのタグ（&lt;size&gt; 等）を閉じる。</summary>
        static string CloseOpenTags(string partial)
        {
            var stack = new System.Collections.Generic.List<string>();
            int i = 0;
            while ((i = partial.IndexOf('<', i)) >= 0)
            {
                int end = partial.IndexOf('>', i);
                if (end < 0) break;
                string tag = partial.Substring(i + 1, end - i - 1);
                if (tag.StartsWith("/"))
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                }
                else
                {
                    int sep = tag.IndexOfAny(new[] { '=', ' ' });
                    stack.Add(sep >= 0 ? tag.Substring(0, sep) : tag);
                }
                i = end + 1;
            }
            for (int k = stack.Count - 1; k >= 0; k--) partial += "</" + stack[k] + ">";
            return partial;
        }

        public void HideDialogue()
        {
            if (dialogueRoot != null) dialogueRoot.SetActive(false);
        }

        public IEnumerator Fade(float targetAlpha, float duration)
        {
            float start = fade.color.a;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                fade.color = new Color(0, 0, 0, Mathf.Lerp(start, targetAlpha, t / duration));
                yield return null;
            }
            fade.color = new Color(0, 0, 0, targetAlpha);
        }

        // ───────── 所持品 ─────────

        public void ToggleInventory()
        {
            inventoryOpen = !inventoryOpen;
            inventoryRoot.SetActive(inventoryOpen);
            if (inventoryOpen) InputLock.Push();
            else InputLock.Pop();
            RefreshInventory();
        }

        void RefreshInventory()
        {
            if (!inventoryOpen || inventoryList == null) return;
            var items = GameState.Inventory;
            if (items.Count == 0)
            {
                inventoryList.text = "<color=#777777>何も持っていない</color>";
                inventoryDescription.text = "";
                return;
            }
            inventoryIndex = (inventoryIndex % items.Count + items.Count) % items.Count;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                string itemName = HorrorDatabase.ItemName(items[i]);
                sb.AppendLine(i == inventoryIndex ? $"<color=#e8c872>▶ {itemName}</color>" : $"　 {itemName}");
            }
            inventoryList.text = sb.ToString();
            var def = HorrorDatabase.Instance != null ? HorrorDatabase.Instance.GetItem(items[inventoryIndex]) : null;
            inventoryDescription.text = def != null ? def.description : "";
        }
    }
}
