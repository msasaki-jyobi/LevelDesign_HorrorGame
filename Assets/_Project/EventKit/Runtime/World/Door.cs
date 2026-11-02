using UnityEngine;

namespace EventKit
{
    public enum DoorMotion
    {
        [InspectorName("回転（開き戸）")] Hinged = 0,
        [InspectorName("横スライド（引き戸）")] Sliding = 1,
    }

    /// <summary>
    /// 開き戸（ヒンジで回転）または引き戸（横にスライド）。施錠時は鍵アイテムを持っていれば解錠できる。
    /// 解錠フラグを指定すると、そのフラグがONになった時点で自動で解錠される。
    /// pageEvent を指定すると、閉じたドアを調べたときの動き（条件・会話・解錠）をイベントのページで管理できる。
    /// 開き戸はヒンジ位置のオブジェクトに付け、扉の板は子に置く。
    /// 引き戸は「動く部分」に指定したオブジェクトが Slide Offset だけ移動する。
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [Header("動き")]
        public DoorMotion motion = DoorMotion.Hinged;
        [Tooltip("動く部分（開き戸ならヒンジ、引き戸なら扉の板の親）。未設定ならこのオブジェクト")]
        public Transform hinge;
        [Tooltip("開き戸：開いたときの角度（マイナスで逆向き）")]
        public float openAngle = 100f;
        [Tooltip("引き戸：開いたときに動く量（ローカル座標）。扉の幅ぶん横にずらす")]
        public Vector3 slideOffset = new Vector3(1.15f, 0f, 0f);
        public float openSpeed = 1.6f;
        public bool isOpen;

        [Header("鍵")]
        public bool locked;
        [ItemId] public string keyItemId;
        [Tooltip("解錠時に鍵アイテムを消費する")]
        public bool consumeKey = true;
        [TextArea(1, 3)] public string lockedMessage = "鍵がかかっている。";
        [TextArea(1, 3)] public string unlockMessage = "「{0}」で鍵を開けた。";
        [Tooltip("このフラグがONになると、鍵がなくても自動で解錠される（空欄なら使わない）")]
        [FlagId] public string requiredFlag;
        [Tooltip("解錠時にONにするフラグ")]
        [FlagId] public string unlockFlag;

        [Header("イベントページで管理")]
        [Tooltip("閉じたドアを調べると、このイベントの有効なページを実行する（無ければ上の鍵の設定で動く）")]
        public GameEvent pageEvent;

        [Header("操作")]
        [Tooltip("OFFにするとプレイヤーは直接操作できない（イベントからのみ開閉）")]
        public bool playerCanUse = true;

        [Header("音（任意）")]
        public AudioClip openSound;
        public AudioClip closeSound;
        public AudioClip lockedSound;
        public AudioClip unlockSound;

        Quaternion closedRotation;
        Vector3 closedPosition;
        float progress;
        AudioSource audioSource;

        void Awake()
        {
            if (hinge == null) hinge = transform;
            closedRotation = hinge.localRotation;
            closedPosition = hinge.localPosition;
            progress = isOpen ? 1f : 0f;
            audioSource = GetComponent<AudioSource>();
            Apply();
        }

        void Update()
        {
            // 解錠フラグがONになったら、その場で解錠する
            if (locked && !string.IsNullOrEmpty(requiredFlag) && GameState.GetFlag(requiredFlag)) Unlock();

            float target = isOpen ? 1f : 0f;
            if (Mathf.Approximately(progress, target)) return;
            progress = Mathf.MoveTowards(progress, target, openSpeed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            float k = Mathf.SmoothStep(0f, 1f, progress);
            if (motion == DoorMotion.Sliding)
                hinge.localPosition = closedPosition + closedRotation * slideOffset * k;
            else
                hinge.localRotation = closedRotation * Quaternion.Euler(0f, openAngle * k, 0f);
        }

        /// <summary>閉じているときに調べると、ページの有効なこのイベントを実行する（鍵・会話・条件をページで管理）。</summary>
        bool UsesPages(out GameEvent ev)
        {
            ev = pageEvent;
            if (isOpen || ev == null) return false;
            var p = ev.ActivePage;
            return p != null && p.trigger == EventTrigger.Interact && p.commands.Count > 0;
        }

        public string InteractPrompt =>
            UsesPages(out var ev) ? ev.InteractPrompt
            : isOpen ? "閉める" : locked ? "開ける（施錠）" : "開ける";
        public bool CanInteract => playerCanUse && !EventRunner.IsBusy;

        public void Interact(PlayerInteractor interactor)
        {
            if (UsesPages(out var ev))
            {
                ev.Interact(interactor);
                return;
            }

            if (locked)
            {
                if (!string.IsNullOrEmpty(keyItemId) && GameState.HasItem(keyItemId))
                {
                    string itemName = EventDatabase.ItemName(keyItemId);
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
