using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorKit.EditorTools
{
    /// <summary>雰囲気（ライティング/フォグ/ポストプロセス）、プレイヤー一式、デモシーンの自動構築。</summary>
    public static class HorrorKitSetup
    {
        static string Root => HorrorKitEditorUtil.KitRoot;
        static string MatDir => Root + "/Demo/Materials";
        static string ProfilePath => Root + "/Settings/HorrorVolumeProfile.asset";
        static string DemoScenePath => HorrorKitEditorUtil.ScenesFolder + "/HorrorDemo.unity";
        const string NoiseProfilePath = "Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset";

        // ───────── 雰囲気 ─────────

        [MenuItem("HorrorKit/セットアップ/ライティング・フォグ・ポストプロセスを適用", priority = 20)]
        static void ApplyAtmosphereMenu()
        {
            ApplyAtmosphere();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        public static void ApplyAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.022f, 0.03f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.15f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.085f;
            RenderSettings.fogColor = new Color(0.008f, 0.009f, 0.013f);

            foreach (var l in HorrorKitEditorUtil.FindAll<Light>())
            {
                if (l.type != LightType.Directional) continue;
                Undo.RecordObject(l, "HorrorKit Atmosphere");
                l.intensity = 0.03f;
                l.color = new Color(0.55f, 0.65f, 0.9f);
            }

            var profile = GetOrCreateProfile();
            var volume = HorrorKitEditorUtil.FindAll<Volume>().FirstOrDefault(v => v.isGlobal);
            if (volume == null)
            {
                var go = new GameObject("Global Volume");
                Undo.RegisterCreatedObjectUndo(go, "Create Volume");
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
            }
            Undo.RecordObject(volume, "HorrorKit Atmosphere");
            volume.sharedProfile = profile;

            foreach (var cam in HorrorKitEditorUtil.FindAll<Camera>())
            {
                Undo.RecordObject(cam, "HorrorKit Atmosphere");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                EditorUtility.SetDirty(data);
            }
        }

        static VolumeProfile GetOrCreateProfile()
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (p != null) return p;

            HorrorKitEditorUtil.EnsureFolder(Root + "/Settings");
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, ProfilePath);

            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var color = p.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.4f);
            color.contrast.Override(18f);
            color.saturation.Override(-45f);
            color.colorFilter.Override(new Color(0.86f, 0.93f, 1f));

            var smh = p.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.9f, 0.97f, 1.1f, -0.05f));

            var vignette = p.Add<Vignette>(true);
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.55f);
            vignette.color.Override(Color.black);

            var grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.45f);
            grain.response.Override(0.75f);

            var chroma = p.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0.15f);

            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.6f);

            var lens = p.Add<LensDistortion>(true);
            lens.intensity.Override(-0.1f);

            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        // ───────── プレイヤー ─────────

        [MenuItem("HorrorKit/セットアップ/プレイヤー一式を配置", priority = 21)]
        static void CreatePlayerRigMenu()
        {
            var pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            Selection.activeGameObject = CreatePlayerRig(new Vector3(pos.x, 0f, pos.z), 0f);
        }

        public static GameObject CreatePlayerRig(Vector3 position, float yaw)
        {
            HorrorKitEditorUtil.EnsureDatabase();

            if (HorrorKitEditorUtil.FindAll<EventRunner>().Length == 0)
            {
                var sys = new GameObject("[HorrorKit Systems]");
                Undo.RegisterCreatedObjectUndo(sys, "Create Systems");
                sys.AddComponent<AudioSource>().playOnAwake = false;
                sys.AddComponent<EventRunner>();
            }
            if (HorrorKitEditorUtil.FindAll<HorrorHUD>().Length == 0)
                Undo.RegisterCreatedObjectUndo(new GameObject("[HorrorKit HUD]", typeof(HorrorHUD)), "Create HUD");

            // プレイヤー本体
            var player = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(player, "Create Player");
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.875f, 0f);
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.03f;
            var fpc = player.AddComponent<FirstPersonController>();
            var steps = player.AddComponent<AudioSource>();
            steps.playOnAwake = false;
            steps.spatialBlend = 0f;
            fpc.footstepSource = steps;

            var cameraRoot = new GameObject("CameraRoot").transform;
            cameraRoot.SetParent(player.transform, false);
            cameraRoot.localPosition = new Vector3(0f, 1.63f, 0f);
            fpc.cameraRoot = cameraRoot;
            player.AddComponent<PlayerInteractor>().origin = cameraRoot;

            // 懐中電灯
            var flashlightGo = new GameObject("Flashlight");
            flashlightGo.transform.SetParent(player.transform, false);
            flashlightGo.transform.localPosition = cameraRoot.localPosition;
            var spot = flashlightGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 25f;
            spot.spotAngle = 60f;
            spot.innerSpotAngle = 20f;
            spot.intensity = 25f;
            spot.color = new Color(1f, 0.93f, 0.8f);
            spot.shadows = LightShadows.Soft;
            var flashlight = flashlightGo.AddComponent<Flashlight>();
            flashlight.spot = spot;
            flashlight.follow = cameraRoot;
            flashlightGo.AddComponent<AudioSource>().playOnAwake = false;

            // メインカメラ + Cinemachine Brain
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            Undo.RecordObject(cam, "Setup Camera");
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain == null) brain = Undo.AddComponent<CinemachineBrain>(cam.gameObject);
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.25f);

            // Cinemachine 一人称カメラ
            var vcamGo = new GameObject("PlayerCamera (Cinemachine)");
            Undo.RegisterCreatedObjectUndo(vcamGo, "Create Cinemachine Camera");
            vcamGo.transform.SetPositionAndRotation(cameraRoot.position, cameraRoot.rotation);
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Target.TrackingTarget = cameraRoot;
            vcam.Priority.Enabled = true;
            vcam.Priority.Value = 10;
            var lensSettings = vcam.Lens;
            lensSettings.FieldOfView = 68f;
            lensSettings.NearClipPlane = 0.05f;
            lensSettings.FarClipPlane = 120f;
            vcam.Lens = lensSettings;
            vcamGo.AddComponent<CinemachineHardLockToTarget>();
            vcamGo.AddComponent<CinemachineRotateWithFollowTarget>();
            var perlin = vcamGo.AddComponent<CinemachineBasicMultiChannelPerlin>();
            perlin.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(NoiseProfilePath);
            perlin.AmplitudeGain = 0.35f;
            perlin.FrequencyGain = 0.6f;
            vcamGo.AddComponent<CinemachineImpulseListener>();
            fpc.playerCamera = vcam;

            return player;
        }

        // ───────── デモシーン ─────────

        [MenuItem("HorrorKit/セットアップ/デモシーンを新規作成", priority = 40)]
        static void CreateDemoSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildDemoScene();
        }

        public static void BuildDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var db = HorrorKitEditorUtil.EnsureDatabase();
            AddDemoData(db);

            // マテリアル
            var wall = Mat("Wall", new Color(0.2f, 0.21f, 0.19f), 0.15f);
            var floor = Mat("Floor", new Color(0.16f, 0.11f, 0.08f), 0.35f);
            var ceiling = Mat("Ceiling", new Color(0.12f, 0.12f, 0.12f), 0.05f);
            var wood = Mat("Wood", new Color(0.22f, 0.14f, 0.08f), 0.25f);
            var doorMat = Mat("Door", new Color(0.17f, 0.11f, 0.07f), 0.3f);
            var metal = Mat("Metal", new Color(0.28f, 0.28f, 0.3f), 0.55f);
            var paper = Mat("Paper", new Color(0.78f, 0.75f, 0.66f), 0.1f);
            var keyMat = Mat("Key", new Color(0.6f, 0.45f, 0.15f), 0.75f, new Color(0.12f, 0.08f, 0.01f));
            var cardboard = Mat("Cardboard", new Color(0.32f, 0.24f, 0.15f), 0.1f);
            var ghostMat = Mat("Ghost", new Color(0.015f, 0.015f, 0.015f), 0.1f);
            var eyeMat = Mat("GhostEye", Color.black, 0.5f, new Color(2.5f, 0f, 0f));
            var bulbMat = Mat("Bulb", new Color(1f, 0.9f, 0.7f), 0.5f, new Color(2.5f, 1.8f, 1.0f));

            var env = new GameObject("Environment").transform;

            // 月明かり（ほぼ届かない）
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(35f, -40f, 0f);

            // 部屋A（開始地点） x:-3..3 z:-3..3
            var roomA = Group("Room_A", env);
            Box("Floor", roomA, new Vector3(0, -0.05f, 0), new Vector3(6.4f, 0.1f, 6.4f), floor);
            Box("Ceiling", roomA, new Vector3(0, 3.05f, 0), new Vector3(6.4f, 0.1f, 6.4f), ceiling);
            Box("Wall_W", roomA, new Vector3(-3.1f, 1.5f, 0), new Vector3(0.2f, 3f, 6.4f), wall);
            Box("Wall_E", roomA, new Vector3(3.1f, 1.5f, 0), new Vector3(0.2f, 3f, 6.4f), wall);
            Box("Wall_S", roomA, new Vector3(0, 1.5f, -3.1f), new Vector3(6.4f, 3f, 0.2f), wall);
            WallWithDoorway(roomA, 3.1f, -3.2f, 3.2f, wall);

            // 廊下 x:-1.25..1.25 z:3.2..17.2
            var corridor = Group("Corridor", env);
            Box("Floor", corridor, new Vector3(0, -0.05f, 10.2f), new Vector3(2.5f, 0.1f, 14f), floor);
            Box("Ceiling", corridor, new Vector3(0, 3.05f, 10.2f), new Vector3(2.5f, 0.1f, 14f), ceiling);
            Box("Wall_W", corridor, new Vector3(-1.35f, 1.5f, 10.2f), new Vector3(0.2f, 3f, 14f), wall);
            Box("Wall_E", corridor, new Vector3(1.35f, 1.5f, 10.2f), new Vector3(0.2f, 3f, 14f), wall);
            WallWithDoorway(corridor, 17.3f, -2.7f, 2.7f, wall);
            Box("Box_1", corridor, new Vector3(-0.9f, 0.25f, 5.6f), new Vector3(0.5f, 0.5f, 0.5f), cardboard).transform.rotation = Quaternion.Euler(0, 12, 0);
            Box("Box_2", corridor, new Vector3(-0.95f, 0.2f, 6.3f), new Vector3(0.4f, 0.4f, 0.4f), cardboard).transform.rotation = Quaternion.Euler(0, -20, 0);
            Box("Box_3", corridor, new Vector3(-0.9f, 0.65f, 5.65f), new Vector3(0.35f, 0.3f, 0.35f), cardboard).transform.rotation = Quaternion.Euler(0, 35, 0);

            // 部屋B（奥）
            var roomB = Group("Room_B", env);
            Box("Floor", roomB, new Vector3(0, -0.05f, 19.9f), new Vector3(5.4f, 0.1f, 5.2f), floor);
            Box("Ceiling", roomB, new Vector3(0, 3.05f, 19.9f), new Vector3(5.4f, 0.1f, 5.2f), ceiling);
            Box("Wall_W", roomB, new Vector3(-2.6f, 1.5f, 19.9f), new Vector3(0.2f, 3f, 5.2f), wall);
            Box("Wall_E", roomB, new Vector3(2.6f, 1.5f, 19.9f), new Vector3(0.2f, 3f, 5.2f), wall);
            Box("Wall_N", roomB, new Vector3(0, 1.5f, 22.5f), new Vector3(5.4f, 3f, 0.2f), wall);

            // 家具
            var props = Group("Props", env);
            Box("Desk", props, new Vector3(-2f, 0.37f, 2.2f), new Vector3(1.4f, 0.74f, 0.7f), wood);
            Box("Radio_Table", props, new Vector3(2.2f, 0.35f, 2.3f), new Vector3(0.8f, 0.7f, 0.6f), wood);
            Box("Bed", props, new Vector3(2.1f, 0.25f, -1.8f), new Vector3(1.4f, 0.5f, 2.2f), cardboard);
            var shelfRoot = Group("Shelf", props);
            Box("Back", shelfRoot, new Vector3(1.2f, 0.9f, 11f), new Vector3(0.05f, 1.8f, 1.2f), wood);
            Box("Side_S", shelfRoot, new Vector3(1.0f, 0.9f, 10.42f), new Vector3(0.4f, 1.8f, 0.04f), wood);
            Box("Side_N", shelfRoot, new Vector3(1.0f, 0.9f, 11.58f), new Vector3(0.4f, 1.8f, 0.04f), wood);
            foreach (float y in new[] { 0.05f, 0.5f, 1.05f, 1.6f })
                Box("Board", shelfRoot, new Vector3(1.0f, y, 11f), new Vector3(0.4f, 0.03f, 1.12f), wood);

            // 照明
            var lights = Group("Lights", env);
            var lamp = PointLight("DeskLamp", lights, new Vector3(-2.35f, 1.1f, 2.4f), new Color(1f, 0.62f, 0.32f), 1.4f, 4f, true);
            var bulb = Sphere("Bulb", lamp.transform, Vector3.zero, 0.08f, bulbMat);
            var lampFlicker = lamp.gameObject.AddComponent<FlickerLight>();
            lampFlicker.flickerAmount = 0.08f;
            lampFlicker.blackoutChance = 0.03f;
            lampFlicker.emissiveRenderer = bulb.GetComponent<Renderer>();
            var c1 = PointLight("CorridorLight_1", lights, new Vector3(0f, 2.75f, 7f), new Color(0.75f, 0.85f, 1f), 1.1f, 5.5f, true);
            var c1Flicker = c1.gameObject.AddComponent<FlickerLight>();
            c1Flicker.flickerAmount = 0.35f;
            c1Flicker.blackoutChance = 0.35f;
            c1Flicker.emissiveRenderer = Sphere("Bulb", c1.transform, new Vector3(0, 0.15f, 0), 0.1f, bulbMat).GetComponent<Renderer>();
            var c2 = PointLight("CorridorLight_2", lights, new Vector3(0f, 2.75f, 15f), new Color(0.75f, 0.85f, 1f), 0.5f, 4f, false);
            var c2Flicker = c2.gameObject.AddComponent<FlickerLight>();
            c2Flicker.flickerAmount = 0.5f;
            c2Flicker.blackoutChance = 0.7f;
            PointLight("RoomB_Red", lights, new Vector3(0f, 2.6f, 20.5f), new Color(1f, 0.12f, 0.08f), 0.9f, 5f, false);

            // プレイヤー・カメラ・UI
            CreatePlayerRig(new Vector3(0f, 0f, -1.8f), 0f);

            // ドア
            var doors = Group("Doors", env);
            var doorA = CreateDoor(doors, "Door_RoomA", new Vector3(-0.6f, 0f, 3.1f), doorMat, metal);
            doorA.openAngle = -100f;
            var doorB = CreateDoor(doors, "Door_Locked", new Vector3(-0.6f, 0f, 17.3f), doorMat, metal);
            doorB.openAngle = -100f;
            doorB.locked = true;
            doorB.keyItemId = "rusty_key";
            doorB.lockedMessage = "扉には鍵がかかっている。\n錆びついた鍵穴がある……。";
            doorB.unlockFlag = "door_b_opened";

            // ジャンプスケア（廊下の幽霊）
            var ghost = new GameObject("Ghost");
            ghost.transform.position = new Vector3(0f, 0f, 16.4f);
            ghost.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(ghost.transform, false);
            body.transform.localPosition = new Vector3(0, 0.95f, 0);
            body.transform.localScale = new Vector3(0.55f, 0.95f, 0.4f);
            body.GetComponent<Renderer>().sharedMaterial = ghostMat;
            var head = Sphere("Head", ghost.transform, new Vector3(0, 1.95f, 0), 0.34f, ghostMat);
            Sphere("Eye_L", head.transform, new Vector3(-0.18f, 0.05f, 0.42f), 0.14f, eyeMat);
            Sphere("Eye_R", head.transform, new Vector3(0.18f, 0.05f, 0.42f), 0.14f, eyeMat);
            ghost.SetActive(false);

            var scareGo = new GameObject("JumpScare_Ghost");
            scareGo.transform.position = new Vector3(0f, 1.5f, 15f);
            var lunge = new GameObject("LungeTarget").transform;
            lunge.SetParent(scareGo.transform, false);
            lunge.position = new Vector3(0f, 0f, 14.1f);
            ghost.transform.SetParent(scareGo.transform, true);
            var js = scareGo.AddComponent<JumpScare>();
            js.scareObject = ghost;
            js.lungeTarget = lunge;
            js.lookPoint = head.transform;
            js.impulse = scareGo.AddComponent<CinemachineImpulseSource>();
            js.impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion;
            js.impulse.ImpulseDefinition.ImpulseDuration = 0.6f;
            js.impulseForce = 1.2f;

            // 鍵の見た目（棚の上）
            var keyVisual = Box("RustyKey", props, new Vector3(1.0f, 1.075f, 11.1f), new Vector3(0.14f, 0.02f, 0.05f), keyMat);
            Object.DestroyImmediate(keyVisual.GetComponent<Collider>());

            // メモの見た目
            var note = Box("Note", props, new Vector3(-2.1f, 0.745f, 2.1f), new Vector3(0.2f, 0.005f, 0.28f), paper);
            note.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            note.GetComponent<BoxCollider>().size = new Vector3(2f, 30f, 1.6f);
            var radio = Box("Radio", props, new Vector3(2.2f, 0.83f, 2.3f), new Vector3(0.42f, 0.26f, 0.16f), metal);
            radio.GetComponent<BoxCollider>().size = new Vector3(1.4f, 1.6f, 2.5f);
            var shelf = shelfRoot.gameObject;

            // ───── イベント ─────
            var events = Group("Events", null);

            var intro = new GameObject("Event_Intro");
            intro.transform.SetParent(events, false);
            AddEvent(intro, "オープニング",
                Page("開始時に1回", EventTrigger.AutoStart, false, null,
                    Fade(CommandType.FadeOut, 0f),
                    Msg("……ここは、どこだ。"),
                    Fade(CommandType.FadeIn, 2f),
                    Msg("<size=26>[WASD] 移動　[マウス] 視点　[E / 左クリック] 調べる・送る\n[F] 懐中電灯　[Shift] 走る　[C] しゃがむ　[Tab] 所持品</size>")));

            AddEvent(note, "机のメモ",
                Page("メモを読んで拾う", EventTrigger.Interact, true, null,
                    Msg("机の上に古いメモが置いてある。"),
                    Msg("『鍵は廊下の棚に隠した。\n　……あれに見つかる前に、ここを出ろ。』"),
                    Item(CommandType.GiveItem, "torn_note"),
                    Flag("read_note", true),
                    Active(note, false)));

            AddEvent(radio, "古いラジオ",
                Page("通常", EventTrigger.Interact, false, null,
                    Msg("古いラジオだ。"),
                    Msg("……ザー……ノイズしか聞こえない。")),
                Page("メモを読んだ後（1回）", EventTrigger.Interact, true, new[] { Cond(ConditionType.FlagOn, "read_note") },
                    Msg("……ザ…ザザ……", "ラジオ"),
                    Msg("……聞こえるか……そこに…誰か、いるのか……", "ラジオ"),
                    Cmd(CommandType.FlickerFlashlight, 0.6f),
                    Msg("……廊下の奥には……行くな……", "ラジオ"),
                    Flag("heard_radio", true)),
                Page("鍵を持っている", EventTrigger.Interact, false, new[] { Cond(ConditionType.HasItem, "rusty_key") },
                    Msg("……もう……遅い……", "ラジオ"),
                    Msg("……振り向くな……", "ラジオ")),
                Page("あれを見た後", EventTrigger.Interact, false, new[] { Cond(ConditionType.FlagOn, "saw_ghost") },
                    Msg("ラジオは完全に沈黙している。")));

            AddEvent(shelf, "廊下の棚",
                Page("通常", EventTrigger.Interact, false, null,
                    Msg("埃をかぶった棚だ。")),
                Page("メモを読んだ後に鍵入手", EventTrigger.Interact, true, new[] { Cond(ConditionType.FlagOn, "read_note") },
                    Msg("棚の奥に手を伸ばす……"),
                    Msg("指先に、何か冷たいものが触れた。"),
                    Active(keyVisual, false),
                    Item(CommandType.GiveItem, "rusty_key")));

            var scareArea = new GameObject("Event_GhostArea");
            scareArea.transform.SetParent(events, false);
            scareArea.transform.position = new Vector3(0f, 0f, 12.9f);
            var scareBox = scareArea.AddComponent<BoxCollider>();
            scareBox.isTrigger = true;
            scareBox.center = new Vector3(0f, 1f, 0f);
            scareBox.size = new Vector3(2.5f, 2f, 0.8f);
            AddEvent(scareArea, "廊下のジャンプスケア",
                Page("鍵入手後に1回", EventTrigger.EnterArea, true, new[] { Cond(ConditionType.HasItem, "rusty_key") },
                    new EventCommand { type = CommandType.JumpScare, jumpScare = js },
                    Flag("saw_ghost", true),
                    Cmd(CommandType.Wait, 0.5f),
                    Msg("……今のは、何だ……？")));

            var endArea = new GameObject("Event_End");
            endArea.transform.SetParent(events, false);
            endArea.transform.position = new Vector3(0f, 0f, 20f);
            var endBox = endArea.AddComponent<BoxCollider>();
            endBox.isTrigger = true;
            endBox.center = new Vector3(0f, 1f, 0f);
            endBox.size = new Vector3(4.5f, 2f, 4f);
            AddEvent(endArea, "デモ終了",
                Page("部屋Bに入る", EventTrigger.EnterArea, true, null,
                    Msg("……行き止まりだ。"),
                    Fade(CommandType.FadeOut, 1.5f),
                    Msg("――デモはここまでです。"),
                    Msg("<size=26>メニュー「HorrorKit > マネージャー」で\nイベント・会話・アイテム・フラグを編集できます。</size>"),
                    Fade(CommandType.FadeIn, 1.5f)));

            ApplyAtmosphere();

            HorrorKitEditorUtil.EnsureFolder(HorrorKitEditorUtil.ScenesFolder);
            EditorSceneManager.SaveScene(scene, DemoScenePath);
            AddSceneToBuild(DemoScenePath);
            AssetDatabase.SaveAssets();
        }

        static void AddDemoData(HorrorDatabase db)
        {
            void AddItem(string id, string name, string desc)
            {
                if (db.GetItem(id) == null) db.items.Add(new ItemDefinition { id = id, displayName = name, description = desc });
            }
            void AddFlag(string id, string desc)
            {
                if (db.GetFlag(id) == null) db.flags.Add(new FlagDefinition { id = id, description = desc });
            }
            AddItem("torn_note", "破れたメモ", "『鍵は廊下の棚に隠した。……あれに見つかる前に、ここを出ろ。』");
            AddItem("rusty_key", "錆びた鍵", "廊下の棚で見つけた古い鍵。奥の扉に合いそうだ。");
            AddFlag("read_note", "机のメモを読んだ");
            AddFlag("heard_radio", "ラジオの声を聞いた");
            AddFlag("saw_ghost", "廊下で「あれ」を見た");
            AddFlag("door_b_opened", "奥の扉を開けた");
            EditorUtility.SetDirty(db);
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ───────── ヘルパー ─────────

        static Material Mat(string name, Color color, float smoothness, Color? emission = null)
        {
            HorrorKitEditorUtil.EnsureFolder(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Sphere(string name, Transform parent, Vector3 localPos, float size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>z 位置に幅 1.2m・高さ 2.2m の出入口付きの壁を作る。</summary>
        static void WallWithDoorway(Transform parent, float z, float xMin, float xMax, Material mat)
        {
            const float half = 0.6f, doorH = 2.2f, h = 3f;
            float leftW = -half - xMin, rightW = xMax - half;
            Box("Wall_Door_L", parent, new Vector3(xMin + leftW / 2f, h / 2f, z), new Vector3(leftW, h, 0.2f), mat);
            Box("Wall_Door_R", parent, new Vector3(half + rightW / 2f, h / 2f, z), new Vector3(rightW, h, 0.2f), mat);
            Box("Wall_Door_Top", parent, new Vector3(0f, doorH + (h - doorH) / 2f, z), new Vector3(half * 2f, h - doorH, 0.2f), mat);
        }

        static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        static Door CreateDoor(Transform parent, string name, Vector3 hingePos, Material mat, Material knobMat)
        {
            var hinge = new GameObject(name);
            hinge.transform.SetParent(parent, false);
            hinge.transform.position = hingePos;
            HorrorKitMenus.BuildDoorVisual(hinge.transform, 1.2f, 2.2f, mat);
            hinge.transform.Find("Knob").GetComponent<Renderer>().sharedMaterial = knobMat;
            var src = hinge.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            var door = hinge.AddComponent<Door>();
            door.hinge = hinge.transform;
            return door;
        }

        static HorrorEvent AddEvent(GameObject go, string name, params EventPage[] pages)
        {
            var ev = go.AddComponent<HorrorEvent>();
            ev.eventName = name;
            ev.pages = new List<EventPage>(pages);
            return ev;
        }

        static EventPage Page(string memo, EventTrigger trigger, bool once, EventCondition[] conditions, params EventCommand[] commands) =>
            new EventPage
            {
                memo = memo,
                trigger = trigger,
                runOnce = once,
                conditions = new List<EventCondition>(conditions ?? new EventCondition[0]),
                commands = new List<EventCommand>(commands)
            };

        static EventCondition Cond(ConditionType type, string id) => new EventCondition { type = type, id = id };
        static EventCommand Msg(string text, string speaker = "") => new EventCommand { type = CommandType.Message, text = text, speaker = speaker };
        static EventCommand Item(CommandType type, string id) => new EventCommand { type = type, itemId = id };
        static EventCommand Flag(string id, bool value) => new EventCommand { type = CommandType.SetFlag, flagId = id, boolValue = value };
        static EventCommand Active(GameObject target, bool value) => new EventCommand { type = CommandType.SetActive, target = target, boolValue = value };
        static EventCommand Cmd(CommandType type, float duration) => new EventCommand { type = type, duration = duration };
        static EventCommand Fade(CommandType type, float duration) => new EventCommand { type = type, duration = duration };
    }
}
