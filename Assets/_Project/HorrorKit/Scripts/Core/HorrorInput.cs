using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorKit
{
    /// <summary>Input System のアクションをコードで定義する（キーボード＋マウス / ゲームパッド）。</summary>
    public static class HorrorInput
    {
        public static InputAction Move, Look, LookStick, Sprint, Crouch, Interact, Flashlight, Inventory, Submit;

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
        }

        public static void Ensure()
        {
            if (map != null) return;
            map = new InputActionMap("HorrorKit");

            Move = map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            Look = map.AddAction("Look", InputActionType.PassThrough, "<Mouse>/delta");
            LookStick = map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            Sprint = map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Crouch = map.AddAction("Crouch", InputActionType.Button, "<Keyboard>/c");
            Crouch.AddBinding("<Keyboard>/leftCtrl");
            Crouch.AddBinding("<Gamepad>/buttonEast");

            Interact = map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            Interact.AddBinding("<Mouse>/leftButton");
            Interact.AddBinding("<Gamepad>/buttonSouth");

            Flashlight = map.AddAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
            Flashlight.AddBinding("<Gamepad>/buttonNorth");

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
    }

    /// <summary>イベント中・所持品画面表示中などにプレイヤー操作を止めるためのカウンタ。</summary>
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
