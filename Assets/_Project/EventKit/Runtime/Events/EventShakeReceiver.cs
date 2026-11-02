using Unity.Cinemachine;
using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// 「カメラシェイク」コマンドでプレイヤーのカメラも揺らすための部品。
    /// プレイヤーの CinemachineCamera（Noise に Basic Multi Channel Perlin を設定したもの）に付ける。
    /// 揺れの設定を自分で制御しているカメラは、これを付けずに EventCamera.ShakeAmplitude を足し込んでもよい。
    /// </summary>
    [RequireComponent(typeof(CinemachineBasicMultiChannelPerlin))]
    public class EventShakeReceiver : MonoBehaviour
    {
        [Tooltip("揺れているときの周波数")]
        public float shakeFrequency = 3f;

        CinemachineBasicMultiChannelPerlin perlin;
        float baseAmplitude, baseFrequency;
        bool shaking;

        void Awake() => perlin = GetComponent<CinemachineBasicMultiChannelPerlin>();

        void LateUpdate()
        {
            float shake = EventCamera.ShakeAmplitude;
            if (shake > 0f)
            {
                if (!shaking)
                {
                    baseAmplitude = perlin.AmplitudeGain;
                    baseFrequency = perlin.FrequencyGain;
                    shaking = true;
                }
                perlin.AmplitudeGain = baseAmplitude + shake;
                perlin.FrequencyGain = Mathf.Max(baseFrequency, shakeFrequency);
            }
            else if (shaking)
            {
                perlin.AmplitudeGain = baseAmplitude;
                perlin.FrequencyGain = baseFrequency;
                shaking = false;
            }
        }
    }
}
