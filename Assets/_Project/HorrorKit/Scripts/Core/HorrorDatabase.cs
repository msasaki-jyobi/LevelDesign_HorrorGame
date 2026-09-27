using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorKit
{
    [Serializable]
    public class ItemDefinition
    {
        public string id = "new_item";
        public string displayName = "新しいアイテム";
        [TextArea(2, 5)] public string description;
        public Sprite icon;
    }

    [Serializable]
    public class FlagDefinition
    {
        public string id = "new_flag";
        [TextArea(1, 3)] public string description;
        public bool defaultValue;
    }

    /// <summary>
    /// キーアイテムとフラグの定義をまとめたデータベース。
    /// Resources/HorrorDatabase.asset に置くと実行時に自動で読み込まれる。
    /// 編集はメニュー「HorrorKit > マネージャー」から行う。
    /// </summary>
    [CreateAssetMenu(menuName = "HorrorKit/Database", fileName = "HorrorDatabase")]
    public class HorrorDatabase : ScriptableObject
    {
        public const string ResourcePath = "HorrorDatabase";

        public List<ItemDefinition> items = new List<ItemDefinition>();
        public List<FlagDefinition> flags = new List<FlagDefinition>();

        static HorrorDatabase instance;

        public static HorrorDatabase Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<HorrorDatabase>(ResourcePath);
                return instance;
            }
        }

        public ItemDefinition GetItem(string id) => string.IsNullOrEmpty(id) ? null : items.Find(i => i.id == id);
        public FlagDefinition GetFlag(string id) => string.IsNullOrEmpty(id) ? null : flags.Find(f => f.id == id);

        public static string ItemName(string id)
        {
            var item = Instance != null ? Instance.GetItem(id) : null;
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : id;
        }
    }
}
