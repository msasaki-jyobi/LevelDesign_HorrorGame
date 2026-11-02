using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using EventKit.EditorTools;

namespace HorrorKit.EditorTools
{
    /// <summary>Hierarchy の右クリック「HorrorKit」メニュー（ホラー用の追加パック分）。イベント・ドア等は EventKit 側。</summary>
    public static class HorrorKitMenus
    {
        static GameObject NewObject(string name, MenuCommand cmd) => EventKitMenus.NewObject(name, cmd);

        [MenuItem("GameObject/HorrorKit/ジャンプスケア", false, 13)]
        static void CreateJumpScareMenu(MenuCommand cmd)
        {
            var go = NewObject("JumpScare", cmd);
            var js = go.AddComponent<JumpScare>();
            var impulse = go.AddComponent<CinemachineImpulseSource>();
            js.impulse = impulse;

            var scare = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            scare.name = "ScareObject（差し替えてください）";
            Object.DestroyImmediate(scare.GetComponent<Collider>());
            scare.transform.SetParent(go.transform, false);
            scare.transform.localPosition = new Vector3(0f, 1f, 0f);
            scare.SetActive(false);
            js.scareObject = scare;
        }

        [MenuItem("GameObject/HorrorKit/ちらつくライト", false, 14)]
        static void CreateFlickerLightMenu(MenuCommand cmd)
        {
            var go = NewObject("FlickerLight", cmd);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 5f;
            l.intensity = 1f;
            l.color = new Color(0.75f, 0.85f, 1f);
            go.AddComponent<FlickerLight>();
        }
    }
}
