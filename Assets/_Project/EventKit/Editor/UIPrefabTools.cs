using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EventKit.EditorTools
{
    /// <summary>
    /// 標準の UI プレハブ（会話・選択肢・プロンプト・所持品・フェード・ロード画面）と UIプレハブ管理アセットを用意する。
    /// 既にあるプレハブは作り直さない（中身を変えない）。
    /// </summary>
    public static class UIPrefabTools
    {
        public static string PrefabFolder => EventKitEditorUtil.KitRoot + "/Prefabs/UI";
        public static string SettingsPath => EventKitEditorUtil.KitRoot + "/Resources/EventUISettings.asset";

        static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ───────── UIプレハブ管理アセット ─────────

        /// <summary>UIプレハブ管理アセット。無ければ作り、空いている欄には標準プレハブを入れる。</summary>
        public static EventUISettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<EventUISettings>(SettingsPath);
            if (settings == null)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:EventUISettings"))
                {
                    settings = AssetDatabase.LoadAssetAtPath<EventUISettings>(AssetDatabase.GUIDToAssetPath(guid));
                    if (settings != null) break;
                }
            }
            if (settings == null)
            {
                EventKitEditorUtil.EnsureFolder(EventKitEditorUtil.KitRoot + "/Resources");
                settings = ScriptableObject.CreateInstance<EventUISettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            bool changed = false;
            if (settings.dialogue == null) { settings.dialogue = DefaultDialogue(); changed = true; }
            if (settings.choices == null) { settings.choices = DefaultChoices(); changed = true; }
            if (settings.prompt == null) { settings.prompt = DefaultPrompt(); changed = true; }
            if (settings.inventory == null) { settings.inventory = DefaultInventory(); changed = true; }
            if (settings.fade == null) { settings.fade = DefaultFade(); changed = true; }
            if (settings.loading == null) { settings.loading = DefaultPrefab().GetComponent<LoadingScreen>(); changed = true; }
            if (changed)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        // ───────── 標準プレハブ ─────────

        static T LoadOrBuild<T>(string fileName, System.Func<GameObject> build) where T : Component
        {
            string path = PrefabFolder + "/" + fileName + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<T>();
            EventKitEditorUtil.EnsureFolder(PrefabFolder);
            var root = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<T>();
        }

        public static DialogueWindow DefaultDialogue() => LoadOrBuild<DialogueWindow>("DialogueWindow", () =>
        {
            var panel = NewImage("DialogueWindow", null, new Color(0f, 0f, 0f, 0.8f));
            Stretch(panel.rectTransform, new Vector2(0.14f, 0.04f), new Vector2(0.86f, 0.27f), Vector2.zero, Vector2.zero);
            var w = panel.gameObject.AddComponent<DialogueWindow>();

            var topLine = NewImage("TopLine", panel.transform, new Color(0.6f, 0.15f, 0.12f, 0.8f));
            Stretch(topLine.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0, -2), Vector2.zero);

            var sBox = NewImage("SpeakerBox", panel.transform, new Color(0.22f, 0.02f, 0.02f, 0.92f));
            var sRect = sBox.rectTransform;
            sRect.anchorMin = sRect.anchorMax = new Vector2(0f, 1f);
            sRect.pivot = Vector2.zero;
            sRect.anchoredPosition = new Vector2(24, 4);
            sRect.sizeDelta = new Vector2(320, 50);
            var speaker = NewText("Speaker", sBox.transform, "話者", 28, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.62f));
            Stretch(speaker.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var body = NewText("Body", panel.transform, "本文がここに表示されます。", 34, TextAnchor.UpperLeft, new Color(0.93f, 0.93f, 0.9f));
            body.lineSpacing = 1.15f;
            Stretch(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(48, 28), new Vector2(-48, -34));

            var next = NewText("Next", panel.transform, "▼", 26, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.8f));
            Stretch(next.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 14), new Vector2(-28, 0));

            w.speakerRoot = sBox.gameObject;
            w.speakerText = speaker;
            w.bodyText = body;
            w.nextIndicator = next;
            return panel.gameObject;
        });

        public static ChoiceWindow DefaultChoices() => LoadOrBuild<ChoiceWindow>("ChoiceWindow", () =>
        {
            var bg = NewImage("ChoiceWindow", null, new Color(0f, 0f, 0f, 0.85f));
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.86f, 0.27f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(0, 12);
            rt.sizeDelta = new Vector2(420, 140);
            var layout = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 24, 18, 18);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var line = NewImage("Line", bg.transform, new Color(0.6f, 0.15f, 0.12f, 0.8f));
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(line.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0, -2), Vector2.zero);

            var item = NewText("ItemTemplate", bg.transform, "選択肢", 32, TextAnchor.MiddleLeft, new Color(0.93f, 0.93f, 0.9f));
            item.gameObject.AddComponent<LayoutElement>().minHeight = 48;
            item.gameObject.SetActive(false);

            var w = bg.gameObject.AddComponent<ChoiceWindow>();
            w.itemContainer = rt;
            w.itemTemplate = item;
            return bg.gameObject;
        });

        public static InteractPrompt DefaultPrompt() => LoadOrBuild<InteractPrompt>("InteractPrompt", () =>
        {
            var root = NewRect("InteractPrompt", null);
            Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var w = root.gameObject.AddComponent<InteractPrompt>();

            var cross = NewImage("Crosshair", root, new Color(1f, 1f, 1f, 0.45f));
            Place(cross.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));

            var text = NewText("Prompt", root, "[E] 調べる", 28, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.92f));
            Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(800, 50));
            text.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);

            w.crosshair = cross;
            w.promptRoot = text.gameObject;
            w.promptText = text;
            return root.gameObject;
        });

        public static InventoryWindow DefaultInventory() => LoadOrBuild<InventoryWindow>("InventoryWindow", () =>
        {
            var inv = NewImage("InventoryWindow", null, new Color(0f, 0f, 0f, 0.88f));
            Stretch(inv.rectTransform, new Vector2(0.6f, 0.18f), new Vector2(0.95f, 0.88f), Vector2.zero, Vector2.zero);
            var w = inv.gameObject.AddComponent<InventoryWindow>();

            var title = NewText("Title", inv.transform, "所持品　<size=22><color=#888888>[Tab] 閉じる　[W/S] 選択</color></size>", 32, TextAnchor.UpperLeft, new Color(1f, 0.82f, 0.62f));
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(32, -70), new Vector2(-32, -20));

            var list = NewText("List", inv.transform, "▶ アイテム名", 30, TextAnchor.UpperLeft, Color.white);
            list.lineSpacing = 1.3f;
            Stretch(list.rectTransform, new Vector2(0, 0.38f), Vector2.one, new Vector2(40, 0), new Vector2(-32, -90));

            var desc = NewText("Description", inv.transform, "アイテムの説明", 24, TextAnchor.UpperLeft, new Color(0.75f, 0.75f, 0.72f));
            Stretch(desc.rectTransform, Vector2.zero, new Vector2(1, 0.36f), new Vector2(32, 24), new Vector2(-140, 0));

            var icon = NewImage("Icon", inv.transform, Color.white);
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(1f, 0.36f);
            irt.pivot = new Vector2(1f, 1f);
            irt.anchoredPosition = new Vector2(-28, -4);
            irt.sizeDelta = new Vector2(96, 96);
            icon.preserveAspect = true;
            icon.enabled = false;

            w.listText = list;
            w.descriptionText = desc;
            w.iconImage = icon;
            return inv.gameObject;
        });

        public static ScreenFader DefaultFade() => LoadOrBuild<ScreenFader>("ScreenFader", () =>
        {
            var img = NewImage("ScreenFader", null, Color.black);
            Stretch(img.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var group = img.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            img.gameObject.AddComponent<ScreenFader>();
            return img.gameObject;
        });

        /// <summary>標準のロード画面プレハブ（ロード画面付きシーン遷移の初期値）。</summary>
        public static GameObject DefaultPrefab() => LoadOrBuild<LoadingScreen>("LoadingScreen", () =>
        {
            var root = new GameObject("LoadingScreen", typeof(RectTransform), typeof(Image), typeof(LoadingScreen));
            Stretch((RectTransform)root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = Color.black;
            var screen = root.GetComponent<LoadingScreen>();

            var title = NewText("NowLoading", root.transform, "Now Loading...", 40, TextAnchor.LowerRight, new Color(0.9f, 0.9f, 0.88f));
            PlaceBR(title.rectTransform, new Vector2(-80f, 120f), new Vector2(600f, 60f));
            screen.blink = title;

            var percent = NewText("Percent", root.transform, "0%", 24, TextAnchor.LowerRight, new Color(0.7f, 0.7f, 0.7f));
            PlaceBR(percent.rectTransform, new Vector2(-80f, 80f), new Vector2(200f, 36f));
            screen.progressText = percent;

            var barBg = NewImage("BarBack", root.transform, new Color(1f, 1f, 1f, 0.12f));
            PlaceBR(barBg.rectTransform, new Vector2(-80f, 60f), new Vector2(600f, 4f));

            var fill = NewImage("BarFill", barBg.transform, new Color(0.85f, 0.15f, 0.12f));
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            screen.progressFill = fill;
            return root;
        }).gameObject;

        // ───────── 複製して差し替え ─────────

        /// <summary>プレハブを複製して保存先を選ばせる。複製したプレハブ（のルートの T）を返す。キャンセルなら null。</summary>
        public static T Duplicate<T>(T source, string suggestedName) where T : Component
        {
            if (source == null) return null;
            string srcPath = AssetDatabase.GetAssetPath(source.gameObject);
            string folder = EventKitEditorUtil.ProjectUIFolder;
            EventKitEditorUtil.EnsureFolder(folder);
            string path = EditorUtility.SaveFilePanelInProject("UIプレハブを複製", suggestedName, "prefab", "複製したプレハブの保存先を選んでください（EventKit の外に置くと、EventKit を更新しても上書きされません）", folder);
            if (string.IsNullOrEmpty(path)) return null;
            if (!AssetDatabase.CopyAsset(srcPath, path)) return null;
            AssetDatabase.ImportAsset(path);
            var copy = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return copy != null ? copy.GetComponent<T>() : null;
        }

        // ───────── 部品 ─────────

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text NewText(string name, Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<Text>();
            t.font = BuiltinFont; // 実行時は日本語フォントに置き換わる（EventUIFont）
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void PlaceBR(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
