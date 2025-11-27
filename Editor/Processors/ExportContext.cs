using System.Collections.Generic;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// VRM Export変換コンテキスト
    /// 元アセットは非破壊で、複製に対して処理を適用する
    /// </summary>
    public class ExportContext
    {
        /// <summary>
        /// 元のPrefabインスタンスRoot（変更禁止）
        /// </summary>
        public GameObject SourceRoot { get; }

        /// <summary>
        /// 処理対象の複製Root（この複製に対して変換処理を適用）
        /// </summary>
        public GameObject ClonedRoot { get; set; }

        /// <summary>
        /// 出力先パス
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// 適用するプリセット名
        /// </summary>
        public string PresetName { get; set; }

        /// <summary>
        /// VRMバージョン（0 or 1）
        /// </summary>
        public int VrmVersion { get; set; } = 1;

        /// <summary>
        /// 各Processorからの通知を集約
        /// </summary>
        public List<ProcessorNotification> AllNotifications { get; } = new List<ProcessorNotification>();

        /// <summary>
        /// Processor間で共有するデータ
        /// </summary>
        public Dictionary<string, object> SharedData { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Errorがあるか
        /// </summary>
        public bool HasError { get; private set; }

        public ExportContext(GameObject sourceRoot)
        {
            SourceRoot = sourceRoot;
        }

        /// <summary>
        /// Processor結果をコンテキストにマージ
        /// </summary>
        public void MergeResult(ProcessorResult result)
        {
            AllNotifications.AddRange(result.Notifications);
            if (result.HasError)
            {
                HasError = true;
            }
        }

        /// <summary>
        /// 共有データを取得
        /// </summary>
        public T GetSharedData<T>(string key, T defaultValue = default)
        {
            if (SharedData.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 共有データを設定
        /// </summary>
        public void SetSharedData<T>(string key, T value)
        {
            SharedData[key] = value;
        }
    }
}
