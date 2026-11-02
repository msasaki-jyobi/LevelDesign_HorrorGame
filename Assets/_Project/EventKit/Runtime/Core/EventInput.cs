using UnityEngine;
using UnityEngine.InputSystem;

namespace EventKit
{
    /// <summary>
    /// イベントで使う入力（調べる・会話送り・選択肢の上下・所持品）。Input System のアクションをコードで定義する。
    /// 移植先のプロジェクトに自前の InputAction がある場合は <see cref="Use"/> で差し替えられる。
    /// </summary>
    public static class EventInput
    {
        /// <summary>選択肢・所持品の上下移動（y を見る）</summary>
        public static InputAction Move;
        /// <summary>調べる</summary>
        public static InputAction Interact;
        /// <summary>会話送り・選択肢の決定</summary>
        public static InputAction Submit;
        /// <summary>所持品ウィンドウの開閉</summary>
        public static InputAction Inventory;

        static InputActionMap map;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (map != null)
            {
                map.Disable();
                map.Dispose();
            }
            map = null;
            Move = Interact = Submit = Inventory = null;
        }

        public static void Ensure()
        {
            if (map != null || Submit != null) return;
            map = new InputActionMap("EventKit");

            Move = map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            Interact = map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            Interact.AddBinding("<Mouse>/leftButton");
            Interact.AddBinding("<Gamepad>/buttonSouth");

            Inventory = map.AddAction("Inventory", InputActionType.Button, "<Keyboard>/tab");
            Inventory.AddBinding("<Keyboard>/i");
            Inventory.AddBinding("<Gamepad>/select");

            Submit = map.AddAction("Submit", InputActionType.Button, "<Keyboard>/e");
            Submit.AddBinding("<Keyboard>/space");
            Submit.AddBinding("<Keyboard>/enter");
            Submit.AddBinding("<Mouse>/leftButton");
            Submit.AddBinding("<Gamepad>/buttonSouth");

            map.Enable();
        }

        /// <summary>
        /// プロジェクト側の InputAction を使う（null の項目は標準のまま）。ゲーム開始時に1回呼ぶ。
        /// 例：EventInput.Use(interact: myActions.Player.Interact, submit: myActions.UI.Submit);
        /// </summary>
        public static void Use(InputAction move = null, InputAction interact = null, InputAction submit = null, InputAction inventory = null)
        {
            Ensure();
            if (move != null) { Move = move; move.Enable(); }
            if (interact != null) { Interact = interact; interact.Enable(); }
            if (submit != null) { Submit = submit; submit.Enable(); }
            if (inventory != null) { Inventory = inventory; inventory.Enable(); }
        }
    }

    /// <summary>イベント中・所持品画面表示中などにプレイヤー操作を止めるためのカウンタ。プレイヤー側はこれを見て操作を止める。</summary>
    public static class InputLock
    {
        static int count;

        public static bool IsLocked => count > 0;
        public static void Push() => count++;
        public static void Pop() => count = Mathf.Max(0, count - 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => count = 0;
    }

    public interface IInteractable
    {
        /// <summary>画面に表示する行動名（例: 調べる / 開ける）。</summary>
        string InteractPrompt { get; }
        bool CanInteract { get; }
        void Interact(PlayerInteractor interactor);
    }

    /// <summary>インスペクターでアイテムIDをドロップダウン表示する。</summary>
    public class ItemIdAttribute : PropertyAttribute { }

    /// <summary>インスペクターでフラグIDをドロップダウン表示する。</summary>
    public class FlagIdAttribute : PropertyAttribute { }
}
