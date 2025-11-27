using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Reports
{
    /// <summary>
    /// VRM Export レポート
    /// JSONとして保存可能
    /// </summary>
    [Serializable]
    public class ExportReport
    {
        public string SourceAssetPath;
        public string OutputPath;
        public string PresetName;
        public int VrmVersion;
        public DateTime ExportTime;
        public bool Success;
        public string StoppedAtProcessor;
        public List<NotificationEntry> Notifications = new List<NotificationEntry>();

        [Serializable]
        public class NotificationEntry
        {
            public string ProcessorId;
            public string Level;
            public string Message;
            public string Details;
            public string Timestamp;
        }

        /// <summary>
        /// PipelineResultからレポートを生成
        /// </summary>
        public static ExportReport FromPipelineResult(
            Processors.PipelineResult pipelineResult,
            Processors.ExportContext context)
        {
            var report = new ExportReport
            {
                SourceAssetPath = context.SourceRoot != null
                    ? UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(context.SourceRoot))
                    : "Unknown",
                OutputPath = context.OutputPath,
                PresetName = context.PresetName,
                VrmVersion = context.VrmVersion,
                ExportTime = DateTime.Now,
                Success = pipelineResult.Success,
                StoppedAtProcessor = pipelineResult.StoppedAtProcessorId
            };

            foreach (var notification in pipelineResult.GetAllNotifications())
            {
                report.Notifications.Add(new NotificationEntry
                {
                    ProcessorId = notification.ProcessorId,
                    Level = notification.Level.ToString(),
                    Message = notification.Message,
                    Details = notification.Details,
                    Timestamp = notification.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return report;
        }

        /// <summary>
        /// JSONとして保存
        /// </summary>
        public void SaveAsJson(string path)
        {
            var json = JsonUtility.ToJson(this, true);
            File.WriteAllText(path, json);
            Debug.Log($"[FBX4VRM] Report saved: {path}");
        }

        /// <summary>
        /// JSONから読み込み
        /// </summary>
        public static ExportReport LoadFromJson(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<ExportReport>(json);
        }

        /// <summary>
        /// サマリーを取得
        /// </summary>
        public string GetSummary()
        {
            var infoCount = Notifications.Count(n => n.Level == "Info");
            var warningCount = Notifications.Count(n => n.Level == "Warning");
            var errorCount = Notifications.Count(n => n.Level == "Error");

            return $"Export {(Success ? "Succeeded" : "Failed")}\n" +
                   $"Info: {infoCount}, Warning: {warningCount}, Error: {errorCount}";
        }
    }
}
