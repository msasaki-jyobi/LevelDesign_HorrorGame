using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// シーン遷移で到着する場所。プレイヤーはこの位置・向き（Y回転）に立つ。
    /// イベントの「シーン遷移」コマンドで、遷移先シーンの出現地点を ID で選ぶ。
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [Tooltip("シーン遷移コマンドで選ぶときの名前（同じシーン内で重複しないように）")]
        public string spawnId = "start";

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var color = new Color(0.3f, 1f, 0.6f, 0.9f);
            Gizmos.color = color;
            Vector3 p = transform.position;
            // 立ち位置（人の大きさの目安）と向き
            Gizmos.DrawWireSphere(p + Vector3.up * 0.3f, 0.3f);
            Gizmos.DrawWireSphere(p + Vector3.up * 1.45f, 0.3f);
            Gizmos.DrawLine(p + new Vector3(0.3f, 0.3f, 0f), p + new Vector3(0.3f, 1.45f, 0f));
            Gizmos.DrawLine(p + new Vector3(-0.3f, 0.3f, 0f), p + new Vector3(-0.3f, 1.45f, 0f));
            Vector3 fwd = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
            Vector3 eye = p + Vector3.up * 1.6f;
            Gizmos.DrawLine(eye, eye + fwd * 0.9f);
            Gizmos.DrawLine(eye + fwd * 0.9f, eye + fwd * 0.65f + Quaternion.Euler(0f, 90f, 0f) * fwd * 0.18f);
            Gizmos.DrawLine(eye + fwd * 0.9f, eye + fwd * 0.65f - Quaternion.Euler(0f, 90f, 0f) * fwd * 0.18f);
            var style = new GUIStyle(UnityEditor.EditorStyles.miniBoldLabel) { normal = { textColor = color } };
            UnityEditor.Handles.Label(p + Vector3.up * 2.0f, "▼ 出現地点：" + spawnId, style);
        }
#endif
    }
}
