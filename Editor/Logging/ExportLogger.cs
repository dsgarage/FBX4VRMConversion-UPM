using System.Text;
using UnityEngine;
using DSGarage.FBX4VRM.Editor.Localization;

namespace DSGarage.FBX4VRM.Editor.Logging
{
    /// <summary>
    /// エクスポートログ管理
    /// 見やすく整形されたログを出力
    /// </summary>
    public static class ExportLogger
    {
        private const string Tag = "FBX4VRM";
        private const string Separator = "────────────────────────────────────";
        private const string DoubleSeparator = "════════════════════════════════════";

        /// <summary>
        /// パイプライン開始ログ
        /// </summary>
        public static void LogPipelineStart(string rootName)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"[{Tag}] {DoubleSeparator}");
            sb.AppendLine($"[{Tag}] {Localize.PipelineStarted}: {rootName}");
            sb.AppendLine($"[{Tag}] {DoubleSeparator}");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// パイプライン完了ログ
        /// </summary>
        public static void LogPipelineComplete(bool success, string outputPath = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"[{Tag}] {DoubleSeparator}");

            if (success)
            {
                sb.AppendLine($"[{Tag}] ✓ {Localize.PipelineCompleted} - {Localize.Success}");
                if (!string.IsNullOrEmpty(outputPath))
                {
                    sb.AppendLine($"[{Tag}]   {Localize.Get("出力先", "Output")}: {outputPath}");
                }
            }
            else
            {
                sb.AppendLine($"[{Tag}] ✗ {Localize.PipelineCompleted} - {Localize.Failed}");
            }

            sb.AppendLine($"[{Tag}] {DoubleSeparator}");

            if (success)
                Debug.Log(sb.ToString());
            else
                Debug.LogError(sb.ToString());
        }

        /// <summary>
        /// Processor開始ログ
        /// </summary>
        public static void LogProcessorStart(string processorName, int order)
        {
            Debug.Log($"[{Tag}] ▶ [{order:D2}] {processorName}");
        }

        /// <summary>
        /// Processor完了ログ（結果サマリー付き）
        /// </summary>
        public static void LogProcessorComplete(string processorName, ProcessorResult result)
        {
            var sb = new StringBuilder();

            var statusIcon = result.CanContinue ? "✓" : "✗";
            var statusText = result.CanContinue ? Localize.ProcessorCompleted : Localize.ProcessorFailed;

            sb.Append($"[{Tag}]   {statusIcon} {statusText}");

            // 通知サマリー
            var infoCount = 0;
            var warnCount = 0;
            var errorCount = 0;

            foreach (var n in result.Notifications)
            {
                switch (n.Level)
                {
                    case NotificationLevel.Info: infoCount++; break;
                    case NotificationLevel.Warning: warnCount++; break;
                    case NotificationLevel.Error: errorCount++; break;
                }
            }

            if (infoCount > 0 || warnCount > 0 || errorCount > 0)
            {
                sb.Append(" (");
                var parts = new System.Collections.Generic.List<string>();
                if (infoCount > 0) parts.Add($"ℹ️{infoCount}");
                if (warnCount > 0) parts.Add($"⚠️{warnCount}");
                if (errorCount > 0) parts.Add($"❌{errorCount}");
                sb.Append(string.Join(" ", parts));
                sb.Append(")");
            }

            // 重要な通知の詳細を表示
            foreach (var n in result.Notifications)
            {
                if (n.Level == NotificationLevel.Warning || n.Level == NotificationLevel.Error)
                {
                    var icon = n.Level == NotificationLevel.Warning ? "⚠️" : "❌";
                    sb.AppendLine();
                    sb.Append($"[{Tag}]     {icon} {n.Message}");
                    if (!string.IsNullOrEmpty(n.Details))
                    {
                        // 詳細は1行に収める
                        var shortDetails = n.Details.Replace("\n", " | ");
                        if (shortDetails.Length > 60)
                        {
                            shortDetails = shortDetails.Substring(0, 57) + "...";
                        }
                        sb.Append($" ({shortDetails})");
                    }
                }
            }

            if (result.CanContinue)
                Debug.Log(sb.ToString());
            else
                Debug.LogError(sb.ToString());
        }

        /// <summary>
        /// 情報ログ
        /// </summary>
        public static void LogInfo(string message)
        {
            Debug.Log($"[{Tag}] ℹ️ {message}");
        }

        /// <summary>
        /// 警告ログ
        /// </summary>
        public static void LogWarning(string message)
        {
            Debug.LogWarning($"[{Tag}] ⚠️ {message}");
        }

        /// <summary>
        /// エラーログ
        /// </summary>
        public static void LogError(string message)
        {
            Debug.LogError($"[{Tag}] ❌ {message}");
        }

        /// <summary>
        /// 区切り線を出力
        /// </summary>
        public static void LogSeparator()
        {
            Debug.Log($"[{Tag}] {Separator}");
        }
    }
}
