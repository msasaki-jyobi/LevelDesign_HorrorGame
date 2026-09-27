using UnityEngine;

namespace HorrorKit
{
    /// <summary>
    /// ヒンジで回転するドア。施錠時は鍵アイテムを持っていれば解錠できる。
    /// このコンポーネントはヒンジ位置のオブジェクトに付け、扉の板は子に置く。
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [Header("動き")]
        public Transform hinge;
        [Tooltip("開いたときの角度（マイナスで逆向き）")]
        public float openAngle = 100f;
        public float openSpeed = 1.6f;
        public bool isOpen;

        [Header("鍵")]
        public bool locked;
        [ItemId] public string keyItemId;
        [Tooltip("解錠時に鍵アイテムを消費する")]
        public bool consumeKey = true;
        [TextArea(1, 3)] public string lockedMessage = "鍵がかかっている。";
        [TextArea(1, 3)] public string unlockMessage = "「{0}」で鍵を開けた。";
        [Tooltip("解錠時にONにするフラグ")]
        [FlagId] public string unlockFlag;

        [Header("操作")]
        [Tooltip("OFFにするとプレイヤーは直接操作できない（イベントからのみ開閉）")]
        public bool playerCanUse = true;

        [Header("音（任意）")]
        public AudioClip openSound;
        public AudioClip closeSound;
        public AudioClip lockedSound;
        public AudioClip unlockSound;

        Quaternion closedRotation;
        float progress;
        AudioSource audioSource;

        void Awake()
        {
            if (hinge == null) hinge = transform;
            closedRotation = hinge.localRotation;
            progress = isOpen ? 1f : 0f;
            audioSource = GetComponent<AudioSource>();
            Apply();
        }

        void Update()
        {
            float target = isOpen ? 1f : 0f;
            if (Mathf.Approximately(progress, target)) return;
            progress = Mathf.MoveTowards(progress, target, openSpeed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            float k = Mathf.SmoothStep(0f, 1f, progress);
            hinge.localRotation = closedRotation * Quaternion.Euler(0f, openAngle * k, 0f);
        }

        public string InteractPrompt => isOpen ? "閉める" : locked ? "開ける（施錠）" : "開ける";
        public bool CanInteract => playerCanUse && !EventRunner.IsBusy;

        public void Interact(PlayerInteractor interactor)
        {
            if (locked)
            {
                if (!string.IsNullOrEmpty(keyItemId) && GameState.HasItem(keyItemId))
                {
                    string itemName = HorrorDatabase.ItemName(keyItemId);
                    Unlock();
                    if (consumeKey) GameState.RemoveItem(keyItemId);
                    Open();
                    EventRunner.Instance.ShowMessages(string.Format(unlockMessage, itemName));
                }
                else
                {
                    Play(lockedSound);
                    EventRunner.Instance.ShowMessages(lockedMessage);
                }
                return;
            }

            if (isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (isOpen || locked) return;
            isOpen = true;
            Play(openSound);
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            Play(closeSound);
        }

        public void Lock()
        {
            locked = true;
            if (isOpen) Close();
        }

        public void Unlock()
        {
            if (!locked) return;
            locked = false;
            Play(unlockSound);
            if (!string.IsNullOrEmpty(unlockFlag)) GameState.SetFlag(unlockFlag, true);
        }

        void Play(AudioClip clip)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }
    }
}
