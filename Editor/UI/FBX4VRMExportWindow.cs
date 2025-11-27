using System.IO;
using UnityEditor;
using UnityEngine;
using DSGarage.FBX4VRM.Editor.Processors;
using DSGarage.FBX4VRM.Editor.Reports;

namespace DSGarage.FBX4VRM.Editor.UI
{
    /// <summary>
    /// FBX4VRM Export ウィンドウ
    /// Phase 0: 最小限のPrefabインスタンス→VRM Exportを提供
    /// </summary>
    public class FBX4VRMExportWindow : EditorWindow
    {
        private GameObject _selectedRoot;
        private string _outputPath = "";
        private int _vrmVersion = 1; // 0 = VRM 0.x, 1 = VRM 1.0
        private Vector2 _scrollPosition;
        private bool _showAdvanced;

        private ProcessorPipeline _pipeline;
        private PipelineResult _lastResult;

        [MenuItem("Tools/FBX4VRM/Export Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<FBX4VRMExportWindow>();
            window.titleContent = new GUIContent("FBX4VRM Export");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            _pipeline = new ProcessorPipeline();
            _pipeline.RegisterDefaultProcessors();

            // デフォルト出力先
            if (string.IsNullOrEmpty(_outputPath))
            {
                _outputPath = Path.Combine(Application.dataPath, "..", "VRM_Export");
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawSourceSelection();
            EditorGUILayout.Space(10);

            DrawExportSettings();
            EditorGUILayout.Space(10);

            DrawExportButton();
            EditorGUILayout.Space(10);

            DrawLastResult();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("FBX4VRM Conversion", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "PrefabインスタンスをVRMとしてExportします。\n" +
                "元アセットは変更されません（非破壊）。",
                MessageType.Info);
        }

        private void DrawSourceSelection()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _selectedRoot = (GameObject)EditorGUILayout.ObjectField(
                    "Root Object",
                    _selectedRoot,
                    typeof(GameObject),
                    true);

                if (GUILayout.Button("Use Selection", GUILayout.Width(100)))
                {
                    if (Selection.activeGameObject != null)
                    {
                        _selectedRoot = Selection.activeGameObject;
                    }
                }
            }

            // バリデーション表示
            if (_selectedRoot != null)
            {
                var validation = ValidateRoot(_selectedRoot);
                if (!string.IsNullOrEmpty(validation))
                {
                    EditorGUILayout.HelpBox(validation, MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Valid: Ready to export", MessageType.None);
                }
            }
        }

        private void DrawExportSettings()
        {
            EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);

            // VRMバージョン選択
            _vrmVersion = EditorGUILayout.Popup("VRM Version",
                _vrmVersion,
                new string[] { "VRM 0.x", "VRM 1.0" });

            // 出力先
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputPath = EditorGUILayout.TextField("Output Folder", _outputPath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var path = EditorUtility.OpenFolderPanel("Select Output Folder", _outputPath, "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _outputPath = path;
                    }
                }
            }

            // 詳細設定
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced");
            if (_showAdvanced)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("(Phase 1以降で追加予定)", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawExportButton()
        {
            var canExport = _selectedRoot != null &&
                            !string.IsNullOrEmpty(_outputPath) &&
                            string.IsNullOrEmpty(ValidateRoot(_selectedRoot));

            using (new EditorGUI.DisabledGroupScope(!canExport))
            {
                if (GUILayout.Button("Export VRM", GUILayout.Height(40)))
                {
                    ExecuteExport();
                }
            }
        }

        private void DrawLastResult()
        {
            if (_lastResult == null) return;

            EditorGUILayout.LabelField("Last Export Result", EditorStyles.boldLabel);

            var style = _lastResult.Success ? EditorStyles.helpBox : EditorStyles.helpBox;
            var icon = _lastResult.Success ? MessageType.Info : MessageType.Error;

            EditorGUILayout.HelpBox(
                _lastResult.Success ? "Export succeeded!" : $"Export failed at: {_lastResult.StoppedAtProcessorId}",
                icon);

            // 通知一覧
            foreach (var notification in _lastResult.GetAllNotifications())
            {
                var notifIcon = notification.Level switch
                {
                    NotificationLevel.Error => MessageType.Error,
                    NotificationLevel.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox($"[{notification.ProcessorId}] {notification.Message}", notifIcon);
            }
        }

        private string ValidateRoot(GameObject root)
        {
            if (root == null) return "Root is null";

            // シーン上のオブジェクトか確認
            if (!root.scene.IsValid())
            {
                return "Please select a scene object (not a prefab asset)";
            }

            // Animatorがあるか確認
            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                return "No Animator component found";
            }

            // Humanoidか確認
            if (animator.avatar == null || !animator.avatar.isHuman)
            {
                return "Avatar is not Humanoid";
            }

            return null; // Valid
        }

        private void ExecuteExport()
        {
            if (_selectedRoot == null) return;

            // 出力フォルダ作成
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }

            // 出力ファイル名
            var fileName = $"{_selectedRoot.name}.vrm";
            var fullPath = Path.Combine(_outputPath, fileName);

            // 複製を作成（非破壊処理）
            var cloned = Instantiate(_selectedRoot);
            cloned.name = _selectedRoot.name + "_Export";

            try
            {
                // コンテキスト作成
                var context = new ExportContext(_selectedRoot)
                {
                    ClonedRoot = cloned,
                    OutputPath = fullPath,
                    VrmVersion = _vrmVersion
                };

                // パイプライン実行
                _lastResult = _pipeline.Execute(context);

                if (_lastResult.Success)
                {
                    // UniVRMでExport
                    ExportWithUniVRM(cloned, fullPath);

                    // レポート生成
                    var report = ExportReport.FromPipelineResult(_lastResult, context);
                    ReportManager.LogReport(report);
                    ReportManager.SaveReport(report);

                    Debug.Log($"[FBX4VRM] Export completed: {fullPath}");
                    EditorUtility.DisplayDialog("Export Complete",
                        $"VRM exported successfully!\n\n{fullPath}", "OK");
                }
                else
                {
                    Debug.LogError($"[FBX4VRM] Export failed at {_lastResult.StoppedAtProcessorId}");
                    EditorUtility.DisplayDialog("Export Failed",
                        $"Export failed at processor: {_lastResult.StoppedAtProcessorId}\n\n" +
                        "See Console for details.", "OK");
                }
            }
            finally
            {
                // 複製を削除
                DestroyImmediate(cloned);
            }

            Repaint();
        }

        private void ExportWithUniVRM(GameObject root, string path)
        {
            if (_vrmVersion == 1)
            {
                // VRM 1.0
                ExportVrm10(root, path);
            }
            else
            {
                // VRM 0.x
                ExportVrm0(root, path);
            }
        }

        private void ExportVrm10(GameObject root, string path)
        {
            var settings = new UniGLTF.GltfExportSettings();

            // VRM10Objectコンポーネントからメタ取得
            UniVRM10.VRM10ObjectMeta meta = null;
            if (root.TryGetComponent<UniVRM10.Vrm10Instance>(out var vrm10))
            {
                meta = vrm10.Vrm?.Meta;
            }

            var bytes = UniVRM10.Vrm10Exporter.Export(settings, root, vrmMeta: meta);
            if (bytes != null)
            {
                File.WriteAllBytes(path, bytes);
            }
            else
            {
                throw new System.Exception("VRM 1.0 export returned null");
            }
        }

        private void ExportVrm0(GameObject root, string path)
        {
            var settings = new UniGLTF.GltfExportSettings
            {
                InverseAxis = UniGLTF.Axes.Z
            };

            var data = VRM.VRMExporter.Export(settings, root, new UniGLTF.RuntimeTextureSerializer());
            if (data != null)
            {
                var bytes = data.ToGlbBytes();
                File.WriteAllBytes(path, bytes);
            }
            else
            {
                throw new System.Exception("VRM 0.x export returned null");
            }
        }
    }
}
