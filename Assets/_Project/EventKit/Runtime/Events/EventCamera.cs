using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// イベント用のカメラ。「視点変更」で指定の位置・向きの Cinemachine カメラに切り替え、
    /// 「視点変更解除」でプレイヤーのカメラへ戻す。切り替えは CinemachineBrain のブレンドで行う。
    /// 「カメラシェイク」の揺れの強さもここで管理する（プレイヤー・イベント両方のカメラに効く）。
    /// </summary>
    public static class EventCamera
    {
        const int LivePriority = 1000;

        static CinemachineCamera cam;
        static CinemachineBasicMultiChannelPerlin perlin;
        static float shakeAmplitude;

        /// <summary>イベントの視点に切り替わっているか</summary>
        public static bool IsActive => cam != null && cam.gameObject.activeSelf;

        /// <summary>カメラシェイクで上乗せする揺れの強さ。プレイヤー側のカメラはこれを読んで揺らす（EventShakeReceiver か自前で）</summary>
        public static float ShakeAmplitude => shakeAmplitude;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cam = null;
            perlin = null;
            lastPlayerCamera = null;
            shakeAmplitude = 0f;
        }

        /// <summary>イベント用カメラに切り替える前に映っていたカメラ（画角・揺れの設定を引き継ぐ）</summary>
        static CinemachineCamera PlayerCamera
        {
            get
            {
                var main = Camera.main;
                var brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
                var live = brain != null ? brain.ActiveVirtualCamera as CinemachineCamera : null;
                if (live != null && (cam == null || live.gameObject != cam.gameObject) && live.name != "[EventCamera]") lastPlayerCamera = live;
                return lastPlayerCamera;
            }
        }

        static CinemachineCamera lastPlayerCamera;

        static void Ensure()
        {
            if (cam != null) return;
            var go = new GameObject("[EventCamera]");
            go.SetActive(false);
            cam = go.AddComponent<CinemachineCamera>();
            cam.Priority = LivePriority;
            perlin = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
            var player = PlayerCamera;
            if (player != null)
            {
                var src = player.GetComponent<CinemachineBasicMultiChannelPerlin>();
                if (src != null) perlin.NoiseProfile = src.NoiseProfile;
            }
            perlin.AmplitudeGain = 0f;
        }

        static void SetBlend(float seconds)
        {
            var main = Camera.main;
            var brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
            if (brain == null) return;
            brain.DefaultBlend = seconds <= 0f
                ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, seconds);
        }

        /// <summary>指定の位置・向きへ視点を切り替える。fov が 0 以下ならプレイヤーと同じ画角。</summary>
        public static void Show(Vector3 position, Quaternion rotation, float fov, float blendSeconds)
        {
            Ensure();
            var lens = PlayerCamera != null ? PlayerCamera.Lens : LensSettings.Default;
            if (fov > 0f) lens.FieldOfView = fov;
            cam.Lens = lens;
            SetBlend(blendSeconds);
            // 視点変更中に別の視点へ切り替えるときも、ブレンドで移るよう一度切り替え直す
            if (cam.gameObject.activeSelf)
            {
                var clone = Object.Instantiate(cam.gameObject);
                clone.name = "[EventCamera]";
                cam.gameObject.SetActive(false);
                Object.Destroy(cam.gameObject, blendSeconds + 0.5f);
                cam = clone.GetComponent<CinemachineCamera>();
                perlin = clone.GetComponent<CinemachineBasicMultiChannelPerlin>();
            }
            cam.transform.SetPositionAndRotation(position, rotation);
            cam.gameObject.SetActive(true);
        }

        /// <summary>プレイヤーの視点に戻す。</summary>
        public static void Reset(float blendSeconds)
        {
            if (!IsActive) return;
            SetBlend(blendSeconds);
            cam.gameObject.SetActive(false);
        }

        /// <summary>seconds 秒間カメラを揺らす（だんだん弱くなる）。</summary>
        public static IEnumerator Shake(float seconds, float strength)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                shakeAmplitude = strength * 3f * (1f - t / seconds);
                if (perlin != null)
                {
                    perlin.AmplitudeGain = shakeAmplitude;
                    perlin.FrequencyGain = 3f;
                }
                yield return null;
            }
            shakeAmplitude = 0f;
            if (perlin != null) perlin.AmplitudeGain = 0f;
        }
    }
}
