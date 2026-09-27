using System.Collections.Generic;
using UnityEngine;

namespace HorrorKit
{
    /// <summary>
    /// 調べる・エリア侵入・自動実行で動くイベント。
    /// ページは番号の大きいものから条件を判定し、最初に条件を満たしたページが有効になる。
    /// 会話・アイテム入手・フラグ操作・ドア・ジャンプスケアをコマンドとして並べて実行する。
    /// </summary>
    [DisallowMultipleComponent]
    public class HorrorEvent : MonoBehaviour, IInteractable
    {
        [Tooltip("マネージャーに表示される名前")]
        public string eventName;
        public List<EventPage> pages = new List<EventPage> { new EventPage() };

        [System.NonSerialized] string cachedPath;
        [System.NonSerialized] bool pendingAuto;
        [System.NonSerialized] bool pendingEnter;

        public string PageKey(int index)
        {
            if (string.IsNullOrEmpty(cachedPath)) cachedPath = BuildPath();
            return cachedPath + "#" + index;
        }

        string BuildPath()
        {
            var t = transform;
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return gameObject.scene.name + ":" + path;
        }

        public string DisplayName => string.IsNullOrEmpty(eventName) ? name : eventName;

        /// <summary>現在有効なページ番号（0始まり）。無ければ -1。</summary>
        public int ActivePageIndex
        {
            get
            {
                for (int i = pages.Count - 1; i >= 0; i--)
                {
                    var p = pages[i];
                    if (p == null || !p.ConditionsMet()) continue;
                    bool once = p.runOnce || p.trigger == EventTrigger.AutoStart;
                    if (once && Application.isPlaying && GameState.IsPageFinished(PageKey(i))) continue;
                    return i;
                }
                return -1;
            }
        }

        public EventPage ActivePage
        {
            get
            {
                int i = ActivePageIndex;
                return i >= 0 ? pages[i] : null;
            }
        }

        public string InteractPrompt
        {
            get
            {
                var p = ActivePage;
                return p == null ? null : string.IsNullOrEmpty(p.prompt) ? "調べる" : p.prompt;
            }
        }

        public bool CanInteract
        {
            get
            {
                var p = ActivePage;
                return p != null && p.trigger == EventTrigger.Interact && p.commands.Count > 0 && !EventRunner.IsBusy;
            }
        }

        public void Interact(PlayerInteractor interactor) => RunIfTrigger(EventTrigger.Interact);

        /// <summary>有効ページを強制実行する（デバッグ・外部スクリプト用）。</summary>
        public void RunActivePage()
        {
            int i = ActivePageIndex;
            if (i >= 0) EventRunner.Instance.Run(this, i);
        }

        bool RunIfTrigger(EventTrigger trigger)
        {
            if (EventRunner.IsBusy) return false;
            int i = ActivePageIndex;
            if (i < 0 || pages[i].trigger != trigger) return false;
            EventRunner.Instance.Run(this, i);
            return true;
        }

        void Start()
        {
            GameState.Changed += OnStateChanged;
            pendingAuto = true;
        }

        void OnDestroy() => GameState.Changed -= OnStateChanged;

        void OnStateChanged() => pendingAuto = true;

        void Update()
        {
            if (EventRunner.IsBusy) return;
            if (pendingAuto)
            {
                pendingAuto = false;
                RunIfTrigger(EventTrigger.AutoStart);
            }
            if (pendingEnter)
            {
                pendingEnter = false;
                RunIfTrigger(EventTrigger.EnterArea);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<FirstPersonController>() != null) pendingEnter = true;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            bool area = false;
            foreach (var p in pages)
                if (p != null && p.trigger == EventTrigger.EnterArea) area = true;
            var col = GetComponent<BoxCollider>();
            Gizmos.color = area ? new Color(1f, 0.55f, 0.1f, 0.6f) : new Color(0.3f, 0.9f, 1f, 0.6f);
            if (col != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(col.center, col.size);
                if (area)
                {
                    Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.08f);
                    Gizmos.DrawCube(col.center, col.size);
                    Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.6f);
                }
                Gizmos.matrix = Matrix4x4.identity;
            }
            var style = new GUIStyle(UnityEditor.EditorStyles.miniBoldLabel) { normal = { textColor = Gizmos.color } };
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, "◆ " + DisplayName, style);
        }
#endif
    }
}
