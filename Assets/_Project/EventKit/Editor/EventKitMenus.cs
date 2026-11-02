using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace EventKit.EditorTools
{
    /// <summary>Hierarchy の右クリック「EventKit」メニュー（GameObject > EventKit）。</summary>
    public static class EventKitMenus
    {
        static Vector3 SpawnPosition()
        {
            var sv = SceneView.lastActiveSceneView;
            return sv != null ? sv.pivot : Vector3.zero;
        }

        public static GameObject NewObject(string name, MenuCommand cmd)
        {
            var go = new GameObject(name);
            var parent = cmd?.context as GameObject;
            if (parent != null) GameObjectUtility.SetParentAndAlign(go, parent);
            else go.transform.position = SpawnPosition();
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Selection.activeGameObject = go;
            return go;
        }

        [MenuItem("GameObject/EventKit/調べるイベント", false, 10)]
        static void CreateInteractEventMenu(MenuCommand cmd) => CreateInteractEvent(cmd);

        public static GameEvent CreateInteractEvent(MenuCommand cmd)
        {
            var go = NewObject("Event_調べる", cmd);
            go.AddComponent<BoxCollider>().size = new Vector3(0.6f, 0.6f, 0.6f);
            var ev = go.AddComponent<GameEvent>();
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

        [MenuItem("GameObject/EventKit/エリアイベント", false, 11)]
        static void CreateAreaEventMenu(MenuCommand cmd) => CreateAreaEvent(cmd);

        public static GameEvent CreateAreaEvent(MenuCommand cmd)
        {
            var go = NewObject("Event_エリア", cmd);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2f, 2f, 2f);
            box.center = new Vector3(0f, 1f, 0f);
            var ev = go.AddComponent<GameEvent>();
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

        [MenuItem("GameObject/EventKit/ドア", false, 12)]
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

        [MenuItem("GameObject/EventKit/引き戸（横開きドア）", false, 12)]
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

        [MenuItem("GameObject/EventKit/振り返り会話イベント（音＋振り向き）", false, 11)]
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

            var ev = go.AddComponent<GameEvent>();
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
    }
}
