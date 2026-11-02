using System.Collections;
using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// イベントから操作されるプレイヤー。プレイヤーのスクリプト（一人称・三人称・アクション等）に実装して、
    /// Awake/OnEnable で <see cref="EventPlayer.Register"/> を呼ぶ。
    /// 使われる場面：シーン遷移・ワープ（Teleport）、振り向き・視点を向ける（LookAt）、エリアイベントの判定。
    /// </summary>
    public interface IEventPlayer
    {
        /// <summary>プレイヤーの Transform（エリアイベントの判定・ワープに使う）</summary>
        Transform PlayerTransform { get; }

        /// <summary>position に、Y回転 yaw の向きで立たせる。</summary>
        void Teleport(Vector3 position, float yaw);

        /// <summary>target の方へ blendTime 秒で向き、holdTime 秒のあいだ向き続ける（会話の振り向き・視点を向ける）。</summary>
        IEnumerator LookAt(Transform target, float blendTime, float holdTime);
    }

    /// <summary>今のシーンのプレイヤー（イベントが操作する相手）。</summary>
    public static class EventPlayer
    {
        public static IEventPlayer Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        public static void Register(IEventPlayer player) => Current = player;

        public static void Unregister(IEventPlayer player)
        {
            if (Current == player) Current = null;
        }

        /// <summary>コライダーがプレイヤーのものか（エリアイベントの判定）。</summary>
        public static bool IsPlayer(Collider other)
        {
            if (other == null) return false;
            if (other.GetComponentInParent<IEventPlayer>() != null) return true;
            var current = Current as Component;
            return current != null && other.transform.IsChildOf(current.transform);
        }
    }
}
