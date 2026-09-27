using UnityEngine;

namespace HorrorKit
{
    /// <summary>不安定にちらつく照明。たまに一瞬消える。</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [Tooltip("負の値なら Light の初期強度を使う")]
        public float baseIntensity = -1f;
        [Range(0f, 1f)] public float flickerAmount = 0.3f;
        public float speed = 6f;
        [Tooltip("1秒あたりに消灯が起こる確率")]
        [Range(0f, 2f)] public float blackoutChance = 0.2f;
        public Vector2 blackoutDuration = new Vector2(0.05f, 0.4f);
        [Tooltip("光に合わせて発光させるレンダラー（電球など・任意）")]
        public Renderer emissiveRenderer;

        Light targetLight;
        float seed, blackoutTimer;
        Color baseEmission;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            targetLight = GetComponent<Light>();
            if (baseIntensity < 0f) baseIntensity = targetLight.intensity;
            seed = Random.value * 100f;
            if (emissiveRenderer != null)
            {
                block = new MaterialPropertyBlock();
                baseEmission = emissiveRenderer.sharedMaterial.GetColor(EmissionId);
            }
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            float k = 1f - flickerAmount + flickerAmount * 2f * n;

            if (blackoutTimer > 0f)
            {
                blackoutTimer -= Time.deltaTime;
                k *= 0.03f;
            }
            else if (Random.value < blackoutChance * Time.deltaTime)
            {
                blackoutTimer = Random.Range(blackoutDuration.x, blackoutDuration.y);
            }

            targetLight.intensity = baseIntensity * k;

            if (emissiveRenderer != null)
            {
                emissiveRenderer.GetPropertyBlock(block);
                block.SetColor(EmissionId, baseEmission * k);
                emissiveRenderer.SetPropertyBlock(block);
            }
        }
    }
}
