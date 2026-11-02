using System;
using System.Collections;
using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// 追加パックで作る「拡張コマンド」の基底クラス。
    /// これを継承して [EventCommand("グループ/表示名")] を付けると、イベントエディタの「コマンドを挿入」に自動で並ぶ。
    /// public なフィールド（[SerializeField] も可）は、そのままコマンドの設定欄になる。
    /// <code>
    /// [Serializable, EventCommand("アクション/敵を出現させる")]
    /// public class SpawnEnemyCommand : CustomCommand
    /// {
    ///     public GameObject enemyPrefab;
    ///     public Transform at;
    ///     public override string Summary => $"敵を出現：{Name(enemyPrefab)}";
    ///     public override IEnumerator Run(EventContext context)
    ///     {
    ///         UnityEngine.Object.Instantiate(enemyPrefab, at.position, at.rotation);
    ///         yield break;
    ///     }
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public abstract class CustomCommand
    {
        /// <summary>イベントエディタの ◆ 行に出す説明（「◆」は自動で付く）</summary>
        public virtual string Summary => GetType().Name;

        /// <summary>実行中に会話ウィンドウを閉じるか（演出系は true にすると見やすい）</summary>
        public virtual bool HidesDialogue => false;

        /// <summary>コマンドの処理。yield return で待つと、終わるまで次のコマンドへ進まない。</summary>
        public abstract IEnumerator Run(EventContext context);

        protected static string Name(UnityEngine.Object o) => o != null ? o.name : "（未設定）";
    }

    /// <summary>拡張コマンドの挿入メニュー上の場所（例："ホラー/懐中電灯の明滅"）。</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class EventCommandAttribute : Attribute
    {
        public string Path { get; }
        public EventCommandAttribute(string path) => Path = path;
    }

    /// <summary>拡張コマンドに渡される実行時の情報。</summary>
    public sealed class EventContext
    {
        /// <summary>コマンドを実行しているランナー（StartCoroutine に使える）</summary>
        public EventRunner Runner { get; }
        /// <summary>実行中のイベント（呼び出し元。メッセージだけの簡易イベントでは null）</summary>
        public GameEvent Source { get; }
        public EventHUD HUD => EventHUD.Instance;
        public IEventPlayer Player => EventPlayer.Current;

        public EventContext(EventRunner runner, GameEvent source)
        {
            Runner = runner;
            Source = source;
        }
    }

    /// <summary>
    /// 「演出オブジェクトを再生」コマンドで再生できるもの（ジャンプスケア、カットシーン等）。
    /// Play の yield が終わるまでイベントは次へ進まない。
    /// </summary>
    public interface IEventPerformance
    {
        IEnumerator Play();
    }
}
