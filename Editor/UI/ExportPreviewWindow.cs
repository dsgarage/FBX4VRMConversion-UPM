using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DSGarage.FBX4VRM.Editor.Processors;

namespace DSGarage.FBX4VRM.Editor.UI
{
    /// <summary>
    /// Export前チェック/プレビューウィンドウ
    /// Export実行前に検出された問題や変換内容を確認できる
    /// </summary>
    public class ExportPreviewWindow : EditorWindow
    {
        private GameObject _root;
        private int _vrmVersion;
        private ProcessorPipeline _pipeline;
        private List<PreviewItem> _previewItems = new List<PreviewItem>();
        private Vector2 _scrollPosition;
        private bool _isAnalyzing;

        private System.Action<bool> _onConfirm; // true=Export実行, false=キャンセル

        public class PreviewItem
        {
            public string ProcessorId;
            public string ProcessorName;
            public NotificationLevel Level;
            public string Message;
            public string Details;
            public bool CanContinue = true;
        }

        public static void Show(GameObject root, int vrmVersion, ProcessorPipeline pipeline, System.Action<bool> onConfirm)
        {
            var window = GetWindow<ExportPreviewWindow>();
            window.titleContent = new GUIContent("Export Preview");
            window._root = root;
            window._vrmVersion = vrmVersion;
            window._pipeline = pipeline;
            window._onConfirm = onConfirm;
            window.minSize = new Vector2(500, 400);
            window.AnalyzeAsync();
            window.Show();
        }

        private async void AnalyzeAsync()
        {
            _isAnalyzing = true;
            _previewItems.Clear();
            Repaint();

            await System.Threading.Tasks.Task.Yield(); // 1フレーム待つ

            try
            {
                // プレビュー用の一時複製を作成
                var tempClone = Instantiate(_root);
                tempClone.name = _root.name + "_PreviewTemp";
                tempClone.hideFlags = HideFlags.HideAndDontSave;

                try
                {
                    var context = new ExportContext(_root)
                    {
                        ClonedRoot = tempClone,
                        VrmVersion = _vrmVersion
                    };

                    // 各Processorでプレビュー実行
                    foreach (var processor in _pipeline.Processors.Where(p => p.Enabled))
                    {
                        var result = processor.Execute(context);
                        context.MergeResult(result);

                        foreach (var notification in result.Notifications)
                        {
                            _previewItems.Add(new PreviewItem
                            {
                                ProcessorId = processor.Id,
                                ProcessorName = processor.DisplayName,
                                Level = notification.Level,
                                Message = notification.Message,
                                Details = notification.Details,
                                CanContinue = notification.Level != NotificationLevel.Error
                            });
                        }

                        if (!result.CanContinue)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    DestroyImmediate(tempClone);
                }
            }
            catch (System.Exception ex)
            {
                _previewItems.Add(new PreviewItem
                {
                    ProcessorId = "preview",
                    ProcessorName = "Preview",
                    Level = NotificationLevel.Error,
                    Message = $"Preview analysis failed: {ex.Message}",
                    CanContinue = false
                });
            }

            _isAnalyzing = false;
            Repaint();
        }

        private void OnGUI()
        {
            if (_isAnalyzing)
            {
                EditorGUILayout.HelpBox("Analyzing...", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawSummary();
            EditorGUILayout.Space(10);

            DrawPreviewItems();
            EditorGUILayout.Space(10);

            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Export Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Target: {_root?.name ?? "None"}");
            EditorGUILayout.LabelField($"VRM Version: {(_vrmVersion == 1 ? "1.0" : "0.x")}");
        }

        private void DrawSummary()
        {
            var errorCount = _previewItems.Count(i => i.Level == NotificationLevel.Error);
            var warningCount = _previewItems.Count(i => i.Level == NotificationLevel.Warning);
            var infoCount = _previewItems.Count(i => i.Level == NotificationLevel.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Analysis Summary", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.color = Color.white;
                    EditorGUILayout.LabelField($"Info: {infoCount}", GUILayout.Width(80));

                    GUI.color = warningCount > 0 ? Color.yellow : Color.white;
                    EditorGUILayout.LabelField($"Warning: {warningCount}", GUILayout.Width(100));

                    GUI.color = errorCount > 0 ? Color.red : Color.white;
                    EditorGUILayout.LabelField($"Error: {errorCount}", GUILayout.Width(80));

                    GUI.color = Color.white;
                }

                EditorGUILayout.Space(5);

                if (errorCount > 0)
                {
                    EditorGUILayout.HelpBox(
                        "Errors detected. Export cannot proceed until errors are resolved.",
                        MessageType.Error);
                }
                else if (warningCount > 0)
                {
                    EditorGUILayout.HelpBox(
                        "Warnings detected. Export can proceed, but some features may not convert correctly.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "No issues detected. Ready to export.",
                        MessageType.Info);
                }
            }
        }

        private void DrawPreviewItems()
        {
            EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);

            if (_previewItems.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues or notifications.", MessageType.Info);
                return;
            }

            foreach (var item in _previewItems)
            {
                DrawPreviewItem(item);
            }
        }

        private void DrawPreviewItem(PreviewItem item)
        {
            var icon = item.Level switch
            {
                NotificationLevel.Error => "console.erroricon",
                NotificationLevel.Warning => "console.warnicon",
                _ => "console.infoicon"
            };

            var bgColor = item.Level switch
            {
                NotificationLevel.Error => new Color(1f, 0.3f, 0.3f, 0.2f),
                NotificationLevel.Warning => new Color(1f, 0.9f, 0.3f, 0.2f),
                _ => new Color(0.3f, 0.7f, 1f, 0.1f)
            };

            var originalBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = originalBg;

                using (new EditorGUILayout.HorizontalScope())
                {
                    var iconContent = EditorGUIUtility.IconContent(icon);
                    GUILayout.Label(iconContent, GUILayout.Width(20), GUILayout.Height(20));

                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField($"[{item.ProcessorName}]", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField(item.Message, EditorStyles.wordWrappedLabel);
                    }
                }

                if (!string.IsNullOrEmpty(item.Details))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(item.Details, EditorStyles.wordWrappedMiniLabel);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawActions()
        {
            var hasErrors = _previewItems.Any(i => i.Level == NotificationLevel.Error);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(35)))
                {
                    _onConfirm?.Invoke(false);
                    Close();
                }

                using (new EditorGUI.DisabledGroupScope(hasErrors))
                {
                    if (GUILayout.Button("Proceed with Export", GUILayout.Height(35)))
                    {
                        _onConfirm?.Invoke(true);
                        Close();
                    }
                }
            }

            if (hasErrors)
            {
                EditorGUILayout.HelpBox("Fix errors before proceeding.", MessageType.Error);
            }
        }

        private void OnDestroy()
        {
            // ウィンドウが閉じられたときにコールバックがまだ呼ばれていなければキャンセル扱い
            // （既に呼ばれている場合は二重呼び出しを避けるためチェック）
        }
    }
}
