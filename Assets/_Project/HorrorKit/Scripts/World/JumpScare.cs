using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace HorrorKit
{
    /// <summary>
    /// ジャンプスケア演出。イベントの「演出/ジャンプスケア」コマンドから呼ぶか、Trigger() を直接呼ぶ。
    /// 出現 → 突進 → 強制注視 → 画面揺れ(Impulse) → 画角キック → 懐中電灯明滅 をまとめて行う。
    /// </summary>
    public class JumpScare : MonoBehaviour
    {
        [Header("出現するもの")]
        [Tooltip("演出中だけ表示されるオブジェクト（非アクティブにしておく）")]
        public GameObject scareObject;
        [Tooltip("ここへ向かって突進する（任意）")]
        public Transform lungeTarget;
        public float lungeTime = 0.3f;
        public bool hideAfter = true;

        [Header("カメラ（Cinemachine）")]
        public bool forceLook = true;
        [Tooltip("注視する点（未設定なら scareObject）")]
        public Transform lookPoint;
        public float lookBlendTime = 0.12f;
        public float fovKick = -14f;
        public CinemachineImpulseSource impulse;
        public float impulseForce = 1.5f;
        [Tooltip("演出中だけ切り替えるカメラ（任意）")]
        public CinemachineCamera focusCamera;
        public int focusPriority = 100;

        [Header("音・光")]
        public AudioClip scareSound;
        [Range(0f, 1f)] public float volume = 1f;
        public bool flickerFlashlight = true;

        [Header("タイミング")]
        public float duration = 1.3f;
        public bool playOnce = true;

        bool played;
        AudioSource audioSource;

        public void Trigger() => StartCoroutine(Play());

        public IEnumerator Play()
        {
            if (playOnce && played) yield break;
            played = true;

            Vector3 startPos = scareObject != null ? scareObject.transform.position : Vector3.zero;
            if (scareObject != null) scareObject.SetActive(true);

            if (scareSound != null)
            {
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                }
                audioSource.PlayOneShot(scareSound, volume);
            }

            if (impulse != null) impulse.GenerateImpulseWithForce(impulseForce);
            if (flickerFlashlight && Flashlight.Instance != null) Flashlight.Instance.Flicker(duration * 0.8f);

            var player = FirstPersonController.Instance;
            var look = lookPoint != null ? lookPoint : scareObject != null ? scareObject.transform : null;
            if (player != null)
            {
                if (!Mathf.Approximately(fovKick, 0f)) player.KickFov(fovKick, duration);
                if (forceLook && look != null) player.StartCoroutine(player.LookAtRoutine(look, lookBlendTime, duration));
            }

            int previousPriority = 0;
            if (focusCamera != null)
            {
                previousPriority = focusCamera.Priority.Value;
                focusCamera.Priority.Enabled = true;
                focusCamera.Priority.Value = focusPriority;
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (scareObject != null && lungeTarget != null)
                {
                    float k = Mathf.Clamp01(t / Mathf.Max(0.01f, lungeTime));
                    scareObject.transform.position = Vector3.Lerp(startPos, lungeTarget.position, k * k);
                }
                yield return null;
            }

            if (focusCamera != null) focusCamera.Priority.Value = previousPriority;
            if (scareObject != null && hideAfter)
            {
                scareObject.SetActive(false);
                scareObject.transform.position = startPos;
            }
        }
    }
}
