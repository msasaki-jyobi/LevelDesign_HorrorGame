using UnityEngine;

namespace HorrorKit
{
    /// <summary>懐中電灯。視点に少し遅れて追従し、電池残量が減ると暗く・不安定になる。</summary>
    public class Flashlight : MonoBehaviour
    {
        public static Flashlight Instance { get; private set; }

        [Header("参照")]
        public Light spot;
        [Tooltip("追従する視点（CameraRoot）")]
        public Transform follow;
        [Tooltip("大きいほど視点にぴったり追従する")]
        public float followSharpness = 14f;

        [Header("状態")]
        public bool isOn = true;
        [Tooltip("所持していないと点けられないようにする")]
        public bool requireItem;
        [ItemId] public string requiredItemId;

        [Header("電池")]
        public bool useBattery = true;
        [Range(0f, 1f)] public float battery = 1f;
        [Tooltip("1秒あたりの消費量（1 = 満タン）")]
        public float drainPerSecond = 0.002f;
        [Range(0f, 1f)] public float lowBatteryThreshold = 0.2f;

        [Header("音（任意）")]
        public AudioClip toggleSound;

        float baseIntensity;
        float flickerTimer;
        AudioSource audioSource;

        void Awake()
        {
            Instance = this;
            if (spot == null) spot = GetComponentInChildren<Light>();
            baseIntensity = spot != null ? spot.intensity : 1f;
            audioSource = GetComponent<AudioSource>();
            HorrorInput.Ensure();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool CanUse => !requireItem || GameState.HasItem(requiredItemId);

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            if (!InputLock.IsLocked && HorrorInput.Flashlight.WasPressedThisFrame() && CanUse) Toggle();

            if (follow != null)
            {
                transform.position = follow.position;
                transform.rotation = Quaternion.Slerp(transform.rotation, follow.rotation, 1f - Mathf.Exp(-followSharpness * dt));
            }

            if (spot == null) return;

            bool lit = isOn && CanUse && (!useBattery || battery > 0f);
            if (lit && useBattery) battery = Mathf.Max(0f, battery - drainPerSecond * dt);

            float intensity = baseIntensity;
            if (useBattery) intensity *= Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(battery / Mathf.Max(0.001f, lowBatteryThreshold)));

            if (lit && useBattery && battery < lowBatteryThreshold && flickerTimer <= 0f && Random.value < 0.8f * dt)
                flickerTimer = Random.Range(0.05f, 0.35f);

            if (flickerTimer > 0f)
            {
                flickerTimer -= dt;
                intensity *= Random.value < 0.5f ? 0.02f : Random.Range(0.3f, 0.8f);
            }

            spot.enabled = lit;
            spot.intensity = intensity;
        }

        public void Toggle()
        {
            isOn = !isOn;
            if (toggleSound != null && audioSource != null) audioSource.PlayOneShot(toggleSound);
        }

        /// <summary>一定時間、懐中電灯を明滅させる。</summary>
        public void Flicker(float duration) => flickerTimer = Mathf.Max(flickerTimer, duration);

        public void AddBattery(float amount) => battery = Mathf.Clamp01(battery + amount);
    }
}
