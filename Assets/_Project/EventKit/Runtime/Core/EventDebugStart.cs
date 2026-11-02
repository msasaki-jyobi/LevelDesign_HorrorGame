using System;
using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// デバッグ用：再生開始時のフラグ・所持品を上書きする。
    /// エディタの「EventKit > デバッグ」で設定した内容が Provider から渡される（ビルドでは何もしない）。
    /// </summary>
    public static class EventDebugStart
    {
        /// <summary>再生開始時に呼ばれる（エディタ側が設定する）</summary>
        public static Action Provider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            if (!Application.isEditor) return;
            Provider?.Invoke();
        }
    }
}
