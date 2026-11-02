using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace HorrorKit.EditorTools
{
    /// <summary>Hierarchy の右クリック「HorrorKit」メニュー（GameObject > HorrorKit）。</summary>
    public static class HorrorKitMenus
    {
        static Vector3 SpawnPosition()
        {
            var sv = SceneView.lastActiveSceneView;
            return sv != null ? sv.pivot : Vector3.zero;
        }

        static GameObject NewObject(string name, MenuCommand cmd)
        {
            var go = new GameObject(name);
            var parent = cmd?.context as GameObject;
            if (parent != null) GameObjectUtility.SetParentAndAlign(go, parent);
            else go.transform.position = SpawnPosition();
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Selection.activeGameObject = go;
            return go;
        }

        [MenuItem("GameObject/HorrorKit/調べるイベント", false, 10)]
        static void CreateInteractEventMenu(MenuCommand cmd) => CreateInteractEvent(cmd);

        public static HorrorEvent CreateInteractEvent(MenuCommand cmd)
        {
            var go = NewObject("Event_調べる", cmd);
            go.AddComponent<BoxCollider>().size = new Vector3(0.6f, 0.6f, 0.6f);
            var ev = go.AddComponent<HorrorEvent>();
            ev.eventName = go.name;
            ev.pages = new List<EventPage>
            {
                new EventPage
                {
                    trigger = EventTrigger.Interact,
                    commands = new List<EventCommand> { new EventCommand { type = CommandType.Message, text = "特に何もない。" } }
                }
            };
            return ev;
        }

        [MenuItem("GameObject/HorrorKit/エリアイベント", false, 11)]
        static void CreateAreaEventMenu(MenuCommand cmd) => CreateAreaEvent(cmd);

        public static HorrorEvent CreateAreaEvent(MenuCommand cmd)
        {
            var go = NewObject("Event_エリア", cmd);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2f, 2f, 2f);
            box.center = new Vector3(0f, 1f, 0f);
            var ev = go.AddComponent<HorrorEvent>();
            ev.eventName = go.name;
            ev.pages = new List<EventPage>
            {
                new EventPage
                {
                    trigger = EventTrigger.EnterArea,
                    runOnce = true,
                    commands = new List<EventCommand> { new EventCommand { type = CommandType.Message, text = "……何か、いる気がする。" } }
                }
            };
            return ev;
        }

        [MenuItem("GameObject/HorrorKit/ドア", false, 12)]
        static void CreateDoorMenu(MenuCommand cmd)
        {
            var go = NewObject("Door", cmd);
            BuildDoorVisual(go.transform, 1.2f, 2.2f, null);
            var door = go.AddComponent<Door>();
            door.hinge = go.transform;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
        }

        [MenuItem("GameObject/HorrorKit/引き戸（横開きドア）", false, 12)]
        static void CreateSlidingDoorMenu(MenuCommand cmd)
        {
            const float width = 1.2f, height = 2.2f;
            var go = NewObject("SlidingDoor", cmd);
            var slider = new GameObject("Slider").transform;
            slider.SetParent(go.transform, false);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(slider, false);
            panel.transform.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0f);
            panel.transform.localScale = new Vector3(width - 0.02f, height, 0.05f);

            // 引き手（くぼみ）
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            Object.DestroyImmediate(handle.GetComponent<Collider>());
            handle.transform.SetParent(slider, false);
            handle.transform.localPosition = new Vector3(width - 0.12f, 1.0f, 0f);
            handle.transform.localScale = new Vector3(0.04f, 0.16f, 0.07f);

            var door = go.AddComponent<Door>();
            door.motion = DoorMotion.Sliding;
            door.hinge = slider;
            door.slideOffset = new Vector3(width - 0.05f, 0f, 0f);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
        }

        [MenuItem("GameObject/HorrorKit/振り返り会話イベント（音＋振り向き）", false, 11)]
        static void CreateTurnAroundEventMenu(MenuCommand cmd)
        {
            var go = NewObject("Event_振り返り", cmd);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2f, 2f, 1f);
            box.center = new Vector3(0f, 1f, 0f);

            // 音が鳴る場所＝振り向く先。プレイヤーの背後に来るよう位置を調整して使う
            var point = new GameObject("SoundPoint");
            point.transform.SetParent(go.transform, false);
            point.transform.localPosition = new Vector3(0f, 1.5f, -3f);

            var ev = go.AddComponent<HorrorEvent>();
            ev.eventName = "振り返り会話";
            ev.pages = new List<EventPage>
            {
                new EventPage
                {
                    memo = "背後で音 → 振り向く → 会話",
                    trigger = EventTrigger.EnterArea,
                    runOnce = true,
                    commands = new List<EventCommand>
                    {
                        new EventCommand { type = CommandType.Message, text = "……！？　今、後ろで何か……", target = point, duration = 0.8f },
                        new EventCommand { type = CommandType.Message, speaker = "？？？", text = "……みつけた。" },
                    }
                }
            };
        }

        public static void BuildDoorVisual(Transform hinge, float width, float height, Material mat)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(hinge, false);
            panel.transform.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0f);
            panel.transform.localScale = new Vector3(width - 0.02f, height, 0.07f);
            if (mat != null) panel.GetComponent<Renderer>().sharedMaterial = mat;

            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "Knob";
            Object.DestroyImmediate(knob.GetComponent<Collider>());
            knob.transform.SetParent(hinge, false);
            knob.transform.localPosition = new Vector3(width - 0.12f, 1.0f, 0f);
            knob.transform.localScale = new Vector3(0.07f, 0.07f, 0.16f);
        }

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
