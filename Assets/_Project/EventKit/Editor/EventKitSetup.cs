using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    /// <summary>EventKit のセットアップと書き出し（他のプロジェクトへ移すとき用）。</summary>
    public static class EventKitSetup
    {
        /// <summary>今のシーンに、イベントの実行役（EventRunner）と画面UI（EventHUD）を置く。既にあれば何もしない。</summary>
        [MenuItem("EventKit/セットアップ/シーンにイベント基盤を配置", priority = 50)]
        public static void PlaceSystems()
        {
            EventKitEditorUtil.EnsureDatabase();
            UIPrefabTools.EnsureSettings();
            if (EventKitEditorUtil.FindAll<EventRunner>().Length == 0)
                Undo.RegisterCreatedObjectUndo(new GameObject("[EventKit Systems]", typeof(EventRunner)), "EventKit");
            if (EventKitEditorUtil.FindAll<EventHUD>().Length == 0)
                Undo.RegisterCreatedObjectUndo(new GameObject("[EventKit HUD]", typeof(EventHUD)), "EventKit");
            Debug.Log("[EventKit] イベント基盤を配置しました。プレイヤーには IEventPlayer を実装し、PlayerInteractor を付けてください（マニュアル参照）。");
        }

        [MenuItem("EventKit/セットアップ/標準UIプレハブを用意", priority = 51)]
        static void PrepareUI()
        {
            var s = UIPrefabTools.EnsureSettings();
            Selection.activeObject = s;
            UIPrefabWindow.Open();
        }

        // ───────── 書き出し ─────────

        [MenuItem("EventKit/書き出し/EventKit だけ（.unitypackage）", priority = 80)]
        static void ExportCore() => Export(false);

        [MenuItem("EventKit/書き出し/EventKit ＋ 追加パック（.unitypackage）", priority = 81)]
        static void ExportWithAddons() => Export(true);

        static void Export(bool withAddons)
        {
            var folders = new List<string> { EventKitEditorUtil.KitRoot };
            if (withAddons)
            {
                // EventKit を参照している asmdef のフォルダ＝追加パック
                foreach (var guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.StartsWith(EventKitEditorUtil.KitRoot)) continue;
                    if (!File.ReadAllText(path).Contains("\"EventKit.Runtime\"")) continue;
                    string dir = Path.GetDirectoryName(path).Replace('\\', '/');
                    // Scripts / Editor の1つ上をパックのフォルダとみなす
                    string name = Path.GetFileName(dir);
                    if (name == "Scripts" || name == "Editor" || name == "Runtime") dir = Path.GetDirectoryName(dir).Replace('\\', '/');
                    if (!folders.Contains(dir)) folders.Add(dir);
                }
            }
            string file = EditorUtility.SaveFilePanel("書き出し", "", withAddons ? "EventKit_with_addons.unitypackage" : "EventKit.unitypackage", "unitypackage");
            if (string.IsNullOrEmpty(file)) return;
            AssetDatabase.ExportPackage(folders.ToArray(), file, ExportPackageOptions.Recurse);
            EditorUtility.RevealInFinder(file);
            Debug.Log("[EventKit] 書き出しました: " + string.Join(", ", folders) +
                      "\n移植先に必要なパッケージ: Input System / Cinemachine 3 / Unity UI（uGUI）。URP はプレビュー画質のためだけに使用（無くても可）。");
        }
    }
}
