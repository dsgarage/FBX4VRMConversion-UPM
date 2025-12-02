using System;
using System.IO;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Reports
{
    /// <summary>
    /// レポート管理
    /// </summary>
    public static class ReportManager
    {
        private const string ReportFolderName = "FBX4VRM_Reports";

        /// <summary>
        /// レポート保存先ディレクトリを取得
        /// </summary>
        public static string GetReportDirectory()
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            var reportDir = Path.Combine(projectPath, ReportFolderName);

            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            return reportDir;
        }

        /// <summary>
        /// レポートを保存
        /// </summary>
        public static string SaveReport(ExportReport report)
        {
            var fileName = $"export_report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var path = Path.Combine(GetReportDirectory(), fileName);
            report.SaveAsJson(path);
            return path;
        }

        /// <summary>
        /// レポートをConsoleにログ出力
        /// </summary>
        public static void LogReport(ExportReport report)
        {
            Debug.Log($"[FBX4VRM] === Export Report ===");
            Debug.Log($"[FBX4VRM] Source: {report.SourceAssetPath}");
            Debug.Log($"[FBX4VRM] Output: {report.OutputPath}");
            Debug.Log($"[FBX4VRM] Result: {(report.Success ? "Success" : "Failed")}");

            foreach (var notification in report.Notifications)
            {
                var logMethod = notification.Level switch
                {
                    "Error" => (Action<object>)Debug.LogError,
                    "Warning" => Debug.LogWarning,
                    _ => Debug.Log
                };

                logMethod($"[FBX4VRM] [{notification.Level}] {notification.ProcessorId}: {notification.Message}");
            }
        }
    }
}
