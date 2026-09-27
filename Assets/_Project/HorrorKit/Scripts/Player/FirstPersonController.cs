using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace HorrorKit
{
    /// <summary>
    /// 一人称プレイヤー操作（Input System）。
    /// 本体でヨー、cameraRoot でピッチを回し、Cinemachine カメラが cameraRoot を追従する。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public static FirstPersonController Instance { get; private set; }

        [Header("参照")]
        public Transform cameraRoot;
        public CinemachineCamera playerCamera;

        [Header("移動")]
        public float walkSpeed = 2.2f;
        public float sprintSpeed = 4.2f;
        public float crouchSpeed = 1.1f;
        public float acceleration = 10f;
        public float gravity = -15f;

        [Header("視点")]
        public float mouseSensitivity = 0.08f;
        public float stickSensitivity = 140f;
        public bool invertY;
        [Range(-89f, 0f)] public float minPitch = -80f;
        [Range(0f, 89f)] public float maxPitch = 80f;

        [Header("しゃがみ")]
        public float standHeight = 1.75f;
        public float crouchHeight = 1.1f;
        [Tooltip("頭頂から目までの距離")]
        public float eyeFromTop = 0.12f;
        public float crouchTransitionSpeed = 10f;

        [Header("カメラの揺れ（Cinemachine Noise の強さ）")]
        public float idleNoise = 0.35f;
        public float walkNoise = 1.0f;
        public float sprintNoise = 2.0f;

        [Header("足音（任意）")]
        public AudioSource footstepSource;
        public AudioClip[] footstepClips;
        public float walkStepInterval = 0.55f;
        public float sprintStepInterval = 0.34f;

        CharacterController controller;
        CinemachineBasicMultiChannelPerlin noise;
        Vector3 planarVelocity;
        float verticalVelocity, pitch, currentHeight, stepTimer, baseFov;
        bool crouching;
        Coroutine fovRoutine;

        public bool IsCrouching => crouching;
        public bool IsSprinting { get; private set; }
        public float Speed => planarVelocity.magnitude;

        void Awake()
        {
            Instance = this;
            controller = GetComponent<CharacterController>();
            HorrorInput.Ensure();
            currentHeight = standHeight;
            if (playerCamera != null)
            {
                noise = playerCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
                baseFov = playerCamera.Lens.FieldOfView;
            }
            if (cameraRoot != null) pitch = NormalizeAngle(cameraRoot.localEulerAngles.x);
            ApplyHeight();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable() => SetCursorLocked(true);
        void OnDisable() => SetCursorLocked(false);

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool locked = InputLock.IsLocked;

            if (!locked) Look();

            if (!locked && HorrorInput.Crouch.WasPressedThisFrame())
            {
                if (!crouching) crouching = true;
                else if (CanStand()) crouching = false;
            }

            float targetHeight = crouching ? crouchHeight : standHeight;
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, 1f - Mathf.Exp(-crouchTransitionSpeed * dt));
            ApplyHeight();

            Move(locked, dt);
            UpdateNoise(dt);
            UpdateFootsteps(dt);
        }

        void Look()
        {
            Vector2 d = HorrorInput.Look.ReadValue<Vector2>() * mouseSensitivity
                      + HorrorInput.LookStick.ReadValue<Vector2>() * (stickSensitivity * Time.deltaTime);
            if (invertY) d.y = -d.y;
            transform.Rotate(0f, d.x, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - d.y, minPitch, maxPitch);
        }

        void ApplyHeight()
        {
            controller.height = currentHeight;
            controller.center = new Vector3(0f, currentHeight * 0.5f, 0f);
            if (cameraRoot != null)
            {
                cameraRoot.localPosition = new Vector3(0f, currentHeight - eyeFromTop, 0f);
                cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        bool CanStand()
        {
            var origin = transform.position + Vector3.up * (currentHeight - 0.05f);
            return !Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.up, out _,
                standHeight - currentHeight + 0.1f, ~0, QueryTriggerInteraction.Ignore);
        }

        void Move(bool locked, float dt)
        {
            Vector2 input = locked ? Vector2.zero : Vector2.ClampMagnitude(HorrorInput.Move.ReadValue<Vector2>(), 1f);
            IsSprinting = !locked && !crouching && input.y > 0.1f && HorrorInput.Sprint.IsPressed();
            float speed = crouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;

            Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
            planarVelocity = Vector3.Lerp(planarVelocity, target, 1f - Mathf.Exp(-acceleration * dt));

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
        }

        void UpdateNoise(float dt)
        {
            if (noise == null) return;
            bool moving = planarVelocity.magnitude > 0.2f;
            float amp = !moving ? idleNoise : IsSprinting ? sprintNoise : walkNoise;
            float freq = !moving ? 0.6f : IsSprinting ? 1.8f : 1.1f;
            float k = 1f - Mathf.Exp(-4f * dt);
            noise.AmplitudeGain = Mathf.Lerp(noise.AmplitudeGain, amp, k);
            noise.FrequencyGain = Mathf.Lerp(noise.FrequencyGain, freq, k);
        }

        void UpdateFootsteps(float dt)
        {
            if (footstepSource == null || footstepClips == null || footstepClips.Length == 0) return;
            if (!controller.isGrounded || planarVelocity.magnitude < 0.5f)
            {
                stepTimer = 0.1f;
                return;
            }
            stepTimer -= dt;
            if (stepTimer > 0f) return;
            float interval = IsSprinting ? sprintStepInterval : walkStepInterval;
            if (crouching) interval *= 1.4f;
            stepTimer = interval;
            footstepSource.pitch = Random.Range(0.9f, 1.1f);
            footstepSource.PlayOneShot(footstepClips[Random.Range(0, footstepClips.Length)], crouching ? 0.4f : 1f);
        }

        /// <summary>対象の方向へ強制的に視点を向ける（ジャンプスケア等）。holdTime の間は追い続ける。</summary>
        public IEnumerator LookAtRoutine(Transform target, float blendTime, float holdTime)
        {
            if (target == null) yield break;
            float startYaw = transform.eulerAngles.y;
            float startPitch = pitch;
            float total = Mathf.Max(blendTime, holdTime);
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                if (target == null) yield break;
                Vector3 from = cameraRoot != null ? cameraRoot.position : transform.position;
                Vector3 dir = target.position - from;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    float p = Mathf.Clamp(-Mathf.Asin(dir.normalized.y) * Mathf.Rad2Deg, minPitch, maxPitch);
                    float k = blendTime <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, t / blendTime);
                    transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(startYaw, yaw, k), 0f);
                    pitch = Mathf.Lerp(startPitch, p, k);
                }
                yield return null;
            }
        }

        /// <summary>一瞬だけ画角を変える（ズーム／驚き演出）。</summary>
        public void KickFov(float delta, float duration)
        {
            if (playerCamera == null) return;
            if (fovRoutine != null) StopCoroutine(fovRoutine);
            fovRoutine = StartCoroutine(FovRoutine(delta, duration));
        }

        IEnumerator FovRoutine(float delta, float duration)
        {
            float attack = duration * 0.12f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t < attack ? t / attack : 1f - (t - attack) / (duration - attack);
                SetFov(baseFov + delta * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k)));
                yield return null;
            }
            SetFov(baseFov);
            fovRoutine = null;
        }

        void SetFov(float fov)
        {
            var lens = playerCamera.Lens;
            lens.FieldOfView = fov;
            playerCamera.Lens = lens;
        }

        public void Teleport(Vector3 position, float yaw)
        {
            controller.enabled = false;
            // 床にめり込んで落下しないよう少し浮かせ、落下速度もリセットする
            transform.SetPositionAndRotation(position + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        static float NormalizeAngle(float a)
        {
            a %= 360f;
            return a > 180f ? a - 360f : a;
        }
    }
}
