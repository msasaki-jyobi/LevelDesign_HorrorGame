using System;
using System.Collections;
using EventKit;
using UnityEngine;

namespace HorrorKit
{
    // ───────── HorrorKit の拡張コマンド（追加パックの作り方の見本） ─────────
    // CustomCommand を継承して [EventCommand("グループ/表示名")] を付けると、
    // イベントエディタの「コマンドを挿入」の右端の列に自動で並ぶ。public フィールドがそのまま設定欄になる。

    [Serializable, EventCommand("ホラー/懐中電灯の明滅")]
    public class FlickerFlashlightCommand : CustomCommand
    {
        [EventLabel("秒数")] public float seconds = 1f;
        [EventLabel("終わるまで待つ"), Tooltip("明滅が終わるまで次へ進まない")]
        public bool waitUntilEnd;

        public override string Summary => $"懐中電灯の明滅：{seconds:0.##}秒" + (waitUntilEnd ? "（終了まで待機）" : "");

        public override IEnumerator Run(EventContext context)
        {
            if (Flashlight.Instance != null) Flashlight.Instance.Flicker(seconds);
            if (waitUntilEnd && seconds > 0f) yield return new WaitForSeconds(seconds);
        }
    }

    [Serializable, EventCommand("ホラー/懐中電灯のON・OFF")]
    public class SetFlashlightCommand : CustomCommand
    {
        [EventLabel("つける")] public bool turnOn = true;

        public override string Summary => "懐中電灯を" + (turnOn ? "つける" : "消す");

        public override IEnumerator Run(EventContext context)
        {
            var light = Flashlight.Instance;
            if (light != null && light.isOn != turnOn) light.Toggle();
            yield break;
        }
    }

    [Serializable, EventCommand("ホラー/懐中電灯の電池を補充")]
    public class AddBatteryCommand : CustomCommand
    {
        [EventLabel("補充量（0〜1）"), Tooltip("1 = 満タン分")]
        public float amount = 0.5f;

        public override string Summary => $"懐中電灯の電池を補充：{amount * 100f:0}%";

        public override IEnumerator Run(EventContext context)
        {
            if (Flashlight.Instance != null) Flashlight.Instance.AddBattery(Mathf.Clamp01(amount));
            yield break;
        }
    }

    /// <summary>組み込みコマンド「懐中電灯を明滅」（以前のデータ）を HorrorKit が処理する。</summary>
    static class HorrorKitCommandHandlers
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            EventRunner.RegisterHandler(CommandType.FlickerFlashlight, Flicker);
        }

        static IEnumerator Flicker(EventCommand c)
        {
            if (Flashlight.Instance != null) Flashlight.Instance.Flicker(c.duration);
            yield break;
        }
    }
}
