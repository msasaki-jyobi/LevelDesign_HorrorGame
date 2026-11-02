using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventKit
{
    /// <summary>フラグ・所持キーアイテム・実行済みイベントページを保持する実行時の状態。</summary>
    public static class GameState
    {
        static readonly Dictionary<string, bool> flags = new Dictionary<string, bool>();
        static readonly List<string> inventory = new List<string>();
        static readonly HashSet<string> finishedPages = new HashSet<string>();
        static bool initialized;

        /// <summary>フラグ・所持品などが変化したときに呼ばれる。</summary>
        public static event Action Changed;
        public static event Action<string> ItemAdded;
        public static event Action<string> ItemRemoved;
        /// <summary>フラグが変わったとき（id, 新しい値）</summary>
        public static event Action<string, bool> FlagChanged;

        public static IReadOnlyList<string> Inventory { get { EnsureInit(); return inventory; } }
        public static IReadOnlyDictionary<string, bool> Flags { get { EnsureInit(); return flags; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            flags.Clear();
            inventory.Clear();
            finishedPages.Clear();
            Changed = null;
            ItemAdded = null;
            ItemRemoved = null;
            FlagChanged = null;
            initialized = false;
        }

        static void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            var db = EventDatabase.Instance;
            if (db == null) return;
            foreach (var f in db.flags)
                if (!string.IsNullOrEmpty(f.id)) flags[f.id] = f.defaultValue;
        }

        public static bool GetFlag(string id)
        {
            EnsureInit();
            return !string.IsNullOrEmpty(id) && flags.TryGetValue(id, out var v) && v;
        }

        public static void SetFlag(string id, bool value)
        {
            if (string.IsNullOrEmpty(id)) return;
            EnsureInit();
            if (flags.TryGetValue(id, out var current) && current == value) return;
            flags[id] = value;
            FlagChanged?.Invoke(id, value);
            Changed?.Invoke();
        }

        public static bool HasItem(string id)
        {
            EnsureInit();
            return !string.IsNullOrEmpty(id) && inventory.Contains(id);
        }

        public static void AddItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            EnsureInit();
            if (inventory.Contains(id)) return;
            inventory.Add(id);
            ItemAdded?.Invoke(id);
            Changed?.Invoke();
        }

        public static void RemoveItem(string id)
        {
            EnsureInit();
            if (!inventory.Remove(id)) return;
            ItemRemoved?.Invoke(id);
            Changed?.Invoke();
        }

        public static bool IsPageFinished(string key) => finishedPages.Contains(key);

        public static void MarkPageFinished(string key)
        {
            if (finishedPages.Add(key)) Changed?.Invoke();
        }

        /// <summary>実行済みの印を外す（デバッグ用：1回だけのページをもう一度動かす）</summary>
        public static void UnmarkPageFinished(string key)
        {
            if (finishedPages.Remove(key)) Changed?.Invoke();
        }

        public static void ResetAll()
        {
            flags.Clear();
            inventory.Clear();
            finishedPages.Clear();
            initialized = false;
            EnsureInit();
            Changed?.Invoke();
        }
    }
}
