using EventKit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorKit
{
    /// <summary>
    /// 一人称プレイヤー用の入力（視点・ダッシュ・しゃがみ・懐中電灯）。
    /// 移動・調べる・会話送り・所持品は EventKit の <see cref="EventInput"/> を使う。
    /// </summary>
    public static class HorrorInput
    {
        public static InputAction Look, LookStick, Sprint, Crouch, Flashlight;

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
            EventInput.Ensure();
            if (map != null) return;
            map = new InputActionMap("HorrorKit");

            Look = map.AddAction("Look", InputActionType.PassThrough, "<Mouse>/delta");
            LookStick = map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            Sprint = map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Crouch = map.AddAction("Crouch", InputActionType.Button, "<Keyboard>/c");
            Crouch.AddBinding("<Keyboard>/leftCtrl");
            Crouch.AddBinding("<Gamepad>/buttonEast");

            Flashlight = map.AddAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
            Flashlight.AddBinding("<Gamepad>/buttonNorth");

            map.Enable();
        }
    }
}
