using UnityEngine;

namespace HorrorKit
{
    /// <summary>視線の先にある IInteractable（イベント・ドアなど）を調べる。</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("レイの発射位置（通常は CameraRoot）")]
        public Transform origin;
        public float distance = 2.2f;
        public LayerMask layers = ~0;

        public IInteractable Current { get; private set; }

        void Awake()
        {
            HorrorInput.Ensure();
            if (origin == null) origin = transform;
        }

        void Update()
        {
            Current = null;
            bool blocked = InputLock.IsLocked || EventRunner.IsBusy || Time.frameCount <= EventRunner.LastEndFrame + 1;
            if (!blocked && RaycastIgnoringSelf(out var hit))
            {
                var target = hit.collider.GetComponentInParent<IInteractable>();
                if (target != null && target.CanInteract) Current = target;
            }

            if (HorrorHUD.Instance != null) HorrorHUD.Instance.SetPrompt(Current?.InteractPrompt);

            if (Current != null && HorrorInput.Interact.WasPressedThisFrame())
                Current.Interact(this);
        }

        readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>プレイヤー自身のコライダーを無視して、最も近いヒットを返す。</summary>
        bool RaycastIgnoringSelf(out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(origin.position, origin.forward, hits, distance, layers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform)) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    nearest = hits[i];
                }
            }
            return best < float.MaxValue;
        }
    }
}
