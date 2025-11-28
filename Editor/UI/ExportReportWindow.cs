using System.Linq;
using UnityEditor;
using UnityEngine;
using DSGarage.FBX4VRM.Editor.Localization;
using DSGarage.FBX4VRM.Editor.Processors;
using DSGarage.FBX4VRM.Editor.Reports;

namespace DSGarage.FBX4VRM.Editor.UI
{
    /// <summary>
    /// Export後レポートウィンドウ
    /// 変換結果のサマリーと通知一覧を表示
    /// </summary>
    public class ExportReportWindow : EditorWindow
    {
        private ExportReport _report;
        private Vector2 _scrollPosition;
        private bool _showInfos = true;
        private bool _showWarnings = true;
        private bool _showErrors = true;

        // スタイルキャッシュ
        private GUIStyle _headerStyle;
        private GUIStyle _infoStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _successStyle;
        private GUIStyle _failStyle;

        public static void Show(ExportReport report)
        {
            var window = GetWindow<ExportReportWindow>();
            window.titleContent = new GUIContent(Localize.Get("エクスポートレポート", "Export Report"));
            window._report = report;
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        public static void Show(PipelineResult pipelineResult, ExportContext context)
        {
            var report = ExportReport.FromPipelineResult(pipelineResult, context);
            Show(report);
        }

        private void InitStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };

            _infoStyle = new GUIStyle(EditorStyles.helpBox);
            _warningStyle = new GUIStyle(EditorStyles.helpBox);
            _errorStyle = new GUIStyle(EditorStyles.helpBox);

            _successStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.2f, 0.8f, 0.2f) }
            };

            _failStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.9f, 0.2f, 0.2f) }
            };
        }

        private void OnGUI()
        {
            InitStyles();

            if (_report == null)
            {
                EditorGUILayout.HelpBox(Localize.Get("レポートデータがありません", "No report data."), MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawSummary();
            EditorGUILayout.Space(10);

            DrawFilters();
            EditorGUILayout.Space(5);

            DrawNotifications();
            EditorGUILayout.Space(10);

            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(Localize.Get("FBX4VRM エクスポートレポート", "FBX4VRM Export Report"), _headerStyle);
            EditorGUILayout.LabelField($"{Localize.Get("エクスポート時刻", "Export Time")}: {_report.ExportTime:yyyy-MM-dd HH:mm:ss}");
        }

        private void DrawSummary()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(Localize.Get("サマリー", "Summary"), EditorStyles.boldLabel);

                // 成功/失敗
                if (_report.Success)
                {
                    EditorGUILayout.LabelField($"{Localize.Get("状態", "Status")}: {Localize.Success}", _successStyle);
                }
                else
                {
                    EditorGUILayout.LabelField($"{Localize.Get("状態", "Status")}: {Localize.Failed} ({_report.StoppedAtProcessor})", _failStyle);
                }

                EditorGUILayout.Space(5);

                // ファイル情報
                EditorGUILayout.LabelField($"{Localize.Get("ソース", "Source")}: {_report.SourceAssetPath}");
                EditorGUILayout.LabelField($"{Localize.Get("出力先", "Output")}: {_report.OutputPath}");
                EditorGUILayout.LabelField($"{Localize.VrmVersion}: {(_report.VrmVersion == 1 ? "1.0" : "0.x")}");
                if (!string.IsNullOrEmpty(_report.PresetName))
                {
                    EditorGUILayout.LabelField($"{Localize.Get("プリセット", "Preset")}: {_report.PresetName}");
                }

                EditorGUILayout.Space(5);

                // 通知カウント
                var infoCount = _report.Notifications.Count(n => n.Level == "Info");
                var warningCount = _report.Notifications.Count(n => n.Level == "Warning");
                var errorCount = _report.Notifications.Count(n => n.Level == "Error");

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{Localize.Info}: {infoCount}", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{Localize.Warning}: {warningCount}", GUILayout.Width(100));
                    EditorGUILayout.LabelField($"{Localize.Error}: {errorCount}", GUILayout.Width(80));
                }
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(Localize.Get("フィルター:", "Filter:"), GUILayout.Width(60));
                _showInfos = GUILayout.Toggle(_showInfos, Localize.Info, "Button", GUILayout.Width(60));
                _showWarnings = GUILayout.Toggle(_showWarnings, Localize.Warning, "Button", GUILayout.Width(70));
                _showErrors = GUILayout.Toggle(_showErrors, Localize.Error, "Button", GUILayout.Width(60));
            }
        }

        private void DrawNotifications()
        {
            EditorGUILayout.LabelField(Localize.Get("通知", "Notifications"), EditorStyles.boldLabel);

            var filteredNotifications = _report.Notifications.Where(n =>
                (n.Level == "Info" && _showInfos) ||
                (n.Level == "Warning" && _showWarnings) ||
                (n.Level == "Error" && _showErrors)
            ).ToList();

            if (filteredNotifications.Count == 0)
            {
                EditorGUILayout.HelpBox(Localize.Get("表示する通知がありません", "No notifications to display."), MessageType.Info);
                return;
            }

            foreach (var notification in filteredNotifications)
            {
                DrawNotification(notification);
            }
        }

        private void DrawNotification(ExportReport.NotificationEntry notification)
        {
            var messageType = notification.Level switch
            {
                "Error" => MessageType.Error,
                "Warning" => MessageType.Warning,
                _ => MessageType.Info
            };

            var icon = notification.Level switch
            {
                "Error" => "console.erroricon",
                "Warning" => "console.warnicon",
                _ => "console.infoicon"
            };

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // アイコン
                    var iconContent = EditorGUIUtility.IconContent(icon);
                    GUILayout.Label(iconContent, GUILayout.Width(20), GUILayout.Height(20));

                    // Processor ID と メッセージ
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField($"[{notification.ProcessorId}]", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField(notification.Message, EditorStyles.wordWrappedLabel);
                    }
                }

                // 詳細（あれば）
                if (!string.IsNullOrEmpty(notification.Details))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(notification.Details, EditorStyles.wordWrappedMiniLabel);
                    EditorGUI.indentLevel--;
                }

                // タイムスタンプ
                EditorGUILayout.LabelField(notification.Timestamp, EditorStyles.miniLabel);
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Localize.SaveReport, GUILayout.Height(30)))
                {
                    SaveReportAsJson();
                }

                if (GUILayout.Button(Localize.CopyToClipboard, GUILayout.Height(30)))
                {
                    CopyToClipboard();
                }

                if (_report.Success && !string.IsNullOrEmpty(_report.OutputPath))
                {
                    if (GUILayout.Button(Localize.ShowInFinder, GUILayout.Height(30)))
                    {
                        EditorUtility.RevealInFinder(_report.OutputPath);
                    }
                }
            }
        }

        private void SaveReportAsJson()
        {
            var path = EditorUtility.SaveFilePanel(
                "Save Export Report",
                ReportManager.GetReportDirectory(),
                $"export_report_{System.DateTime.Now:yyyyMMdd_HHmmss}.json",
                "json");

            if (!string.IsNullOrEmpty(path))
            {
                _report.SaveAsJson(path);
                Debug.Log($"[FBX4VRM] Report saved: {path}");
            }
        }

        private void CopyToClipboard()
        {
            var text = _report.GetSummary() + "\n\n";

            foreach (var notification in _report.Notifications)
            {
                text += $"[{notification.Level}] {notification.ProcessorId}: {notification.Message}\n";
                if (!string.IsNullOrEmpty(notification.Details))
                {
                    text += $"  Details: {notification.Details}\n";
                }
            }

            EditorGUIUtility.systemCopyBuffer = text;
            Debug.Log("[FBX4VRM] Report copied to clipboard");
        }
    }
}
