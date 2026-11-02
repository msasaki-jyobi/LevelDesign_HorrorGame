using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EventKit.EditorTools
{
    /// <summary>出現地点（SpawnPoint）とシーン遷移の設定を助けるエディタ用の処理。</summary>
    public static class SpawnPointTools
    {
        [MenuItem("GameObject/EventKit/出現地点（ワープ先・到着地点）", false, 15)]
        static void CreateMenu(MenuCommand cmd)
        {
            var parent = cmd?.context as GameObject;
            var scene = parent != null ? parent.scene : SceneManager.GetActiveScene();
            var sp = Create(scene, null);
            if (parent != null) Undo.SetTransformParent(sp.transform, parent.transform, "出現地点");
        }

        /// <summary>
        /// シーンビューで見ている場所（中央の地面）に、シーンビューの向きで出現地点を作る。
        /// </summary>
        public static SpawnPoint Create(Scene scene, string id)
        {
            var go = new GameObject();
            if (scene.IsValid() && scene.isLoaded && go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            var sp = go.AddComponent<SpawnPoint>();
            sp.spawnId = string.IsNullOrEmpty(id) ? UniqueId(scene) : id;
            go.name = "SpawnPoint_" + sp.spawnId;

            var sv = SceneView.lastActiveSceneView;
            Vector3 pos = sv != null ? sv.pivot : Vector3.zero;
            float yaw = sv != null ? sv.rotation.eulerAngles.y : 0f;
            // 地面に立たせる
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            Undo.RegisterCreatedObjectUndo(go, "出現地点を作る");
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            return sp;
        }

        static string UniqueId(Scene scene)
        {
            var used = new HashSet<string>();
            foreach (var sp in Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include))
                if (sp.gameObject.scene == scene) used.Add(sp.spawnId);
            if (!used.Contains("start")) return "start";
            for (int i = 1; ; i++)
                if (!used.Contains("spawn_" + i)) return "spawn_" + i;
        }

        /// <summary>シーンビューを出現地点（やオブジェクト）に向けて選択する。</summary>
        public static void Frame(GameObject go)
        {
            if (go == null) return;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        // ───────── シーンにある出現地点 ─────────

        public static SpawnPoint FindLoaded(string scenePath, string id)
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded) return null;
            foreach (var sp in Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include))
                if (sp.gameObject.scene == scene && sp.spawnId == id) return sp;
            return null;
        }

        static readonly Dictionary<string, (long stamp, List<string> ids)> fileCache = new Dictionary<string, (long, List<string>)>();
        static string scriptGuid;

        /// <summary>シーンにある出現地点の名前一覧。開いているシーンは実物から、閉じているシーンはファイルを読んで調べる（書き込みはしない）。</summary>
        public static List<string> Ids(string scenePath)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(scenePath)) return result;
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (scene.isLoaded)
            {
                foreach (var sp in Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include))
                    if (sp.gameObject.scene == scene && !result.Contains(sp.spawnId)) result.Add(sp.spawnId);
                result.Sort(System.StringComparer.Ordinal);
                return result;
            }
            if (!File.Exists(scenePath)) return result;
            long stamp = File.GetLastWriteTimeUtc(scenePath).Ticks;
            if (fileCache.TryGetValue(scenePath, out var cached) && cached.stamp == stamp) return new List<string>(cached.ids);

            scriptGuid ??= ScriptGuid();
            string text = File.ReadAllText(scenePath);
            if (!string.IsNullOrEmpty(scriptGuid) && text.Contains(scriptGuid))
            {
                foreach (var block in Regex.Split(text, @"^--- !u!", RegexOptions.Multiline))
                {
                    if (!block.StartsWith("114 ") || !block.Contains("guid: " + scriptGuid)) continue;
                    var m = Regex.Match(block, @"^  spawnId: (.*)$", RegexOptions.Multiline);
                    if (!m.Success) continue;
                    string id = m.Groups[1].Value.Trim().Trim('"', '\'');
                    if (!result.Contains(id)) result.Add(id);
                }
            }
            result.Sort(System.StringComparer.Ordinal);
            fileCache[scenePath] = (stamp, result);
            return new List<string>(result);
        }

        static string ScriptGuid()
        {
            foreach (var guid in AssetDatabase.FindAssets("SpawnPoint t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == typeof(SpawnPoint)) return guid;
            }
            return null;
        }

        // ───────── Build Settings ─────────

        public static bool InBuildSettings(string scenePath)
        {
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled && s.path == scenePath) return true;
            return false;
        }

        public static void AddToBuildSettings(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int i = list.FindIndex(s => s.path == scenePath);
            if (i >= 0) list[i].enabled = true;
            else
            {
                // 今のシーンが入っていなければ、最初のシーンとして一緒に入れる
                var active = SceneManager.GetActiveScene().path;
                if (list.Count == 0 && !string.IsNullOrEmpty(active) && active != scenePath)
                    list.Add(new EditorBuildSettingsScene(active, true));
                list.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
