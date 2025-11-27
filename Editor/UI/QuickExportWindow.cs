using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DSGarage.FBX4VRM.Editor.Presets;
using DSGarage.FBX4VRM.Editor.Processors;
using DSGarage.FBX4VRM.Editor.Reports;

namespace DSGarage.FBX4VRM.Editor.UI
{
    /// <summary>
    /// ワンボタンVRM化ウィンドウ
    /// 選択オブジェクトを即座にVRMエクスポート
    /// </summary>
    public class QuickExportWindow : EditorWindow
    {
        private GameObject _selectedObject;
        private ExportPreset _selectedPreset;
        private List<ExportPreset> _availablePresets;
        private int _selectedPresetIndex;
        private string[] _presetNames;
        private Vector2 _scrollPosition;
        private bool _autoSelectFromHierarchy = true;

        private ProcessorPipeline _pipeline;
        private bool _isExporting;

        [MenuItem("Tools/FBX4VRM/Quick Export %#e", false, 0)] // Ctrl+Shift+E
        public static void ShowWindow()
        {
            var window = GetWindow<QuickExportWindow>();
            window.titleContent = new GUIContent("Quick VRM Export");
            window.minSize = new Vector2(350, 250);
            window.maxSize = new Vector2(450, 400);
        }

        /// <summary>
        /// 選択オブジェクトを指定してウィンドウを開く
        /// </summary>
        public static void ShowWithObject(GameObject obj)
        {
            var window = GetWindow<QuickExportWindow>();
            window.titleContent = new GUIContent("Quick VRM Export");
            window._selectedObject = obj;
            window.Show();
        }

        private void OnEnable()
        {
            _pipeline = new ProcessorPipeline();
            _pipeline.RegisterDefaultProcessors();

            RefreshPresets();

            // Hierarchyの選択を監視
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (_autoSelectFromHierarchy && Selection.activeGameObject != null)
            {
                var obj = Selection.activeGameObject;
                if (obj.scene.IsValid() && IsValidVrmSource(obj))
                {
                    _selectedObject = obj;
                    Repaint();
                }
            }
        }

        private void RefreshPresets()
        {
            _availablePresets = PresetManager.GetAllPresets();
            _presetNames = _availablePresets.Select(p => p.presetName).ToArray();

            // デフォルトプリセットを選択
            if (_selectedPreset == null && _availablePresets.Count > 0)
            {
                _selectedPresetIndex = 0;
                _selectedPreset = _availablePresets[0];
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(5);

            DrawObjectSelection();
            EditorGUILayout.Space(10);

            DrawPresetSelection();
            EditorGUILayout.Space(10);

            DrawQuickInfo();
            EditorGUILayout.Space(10);

            DrawExportButton();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Quick VRM Export", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "選択オブジェクトをワンボタンでVRM化します。\n" +
                "プリセットを選んで Export をクリック！",
                MessageType.Info);
        }

        private void DrawObjectSelection()
        {
            EditorGUILayout.LabelField("Export Target", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _selectedObject = (GameObject)EditorGUILayout.ObjectField(
                    _selectedObject,
                    typeof(GameObject),
                    true);

                if (GUILayout.Button("↻", GUILayout.Width(25)))
                {
                    if (Selection.activeGameObject != null)
                    {
                        _selectedObject = Selection.activeGameObject;
                    }
                }
            }

            _autoSelectFromHierarchy = EditorGUILayout.Toggle(
                "Auto-select from Hierarchy",
                _autoSelectFromHierarchy);

            // バリデーション表示
            if (_selectedObject != null)
            {
                var (isValid, message) = ValidateObject(_selectedObject);
                EditorGUILayout.HelpBox(
                    message,
                    isValid ? MessageType.None : MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Select a Humanoid model from the scene", MessageType.Warning);
            }
        }

        private void DrawPresetSelection()
        {
            EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);

            if (_presetNames == null || _presetNames.Length == 0)
            {
                EditorGUILayout.HelpBox("No presets found. Create one in Export Window.", MessageType.Warning);
                if (GUILayout.Button("Open Export Window"))
                {
                    FBX4VRMExportWindow.ShowWindow();
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var newIndex = EditorGUILayout.Popup(_selectedPresetIndex, _presetNames);
                if (newIndex != _selectedPresetIndex)
                {
                    _selectedPresetIndex = newIndex;
                    _selectedPreset = _availablePresets[newIndex];
                }

                if (GUILayout.Button("↻", GUILayout.Width(25)))
                {
                    RefreshPresets();
                }
            }

            // プリセット情報
            if (_selectedPreset != null)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField("Version", _selectedPreset.vrmVersion == 1 ? "VRM 1.0" : "VRM 0.x");
                    if (!string.IsNullOrEmpty(_selectedPreset.description))
                    {
                        EditorGUILayout.LabelField("Description", _selectedPreset.description, EditorStyles.wordWrappedLabel);
                    }
                    if (_selectedPreset.tags != null && _selectedPreset.tags.Length > 0)
                    {
                        EditorGUILayout.LabelField("Tags", string.Join(", ", _selectedPreset.tags));
                    }
                }
            }
        }

        private void DrawQuickInfo()
        {
            if (_selectedObject == null || _selectedPreset == null) return;

            EditorGUILayout.LabelField("Export Info", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                var outputPath = GetOutputPath();
                EditorGUILayout.LabelField("Output", outputPath, EditorStyles.miniLabel);
            }
        }

        private void DrawExportButton()
        {
            var canExport = _selectedObject != null &&
                            _selectedPreset != null &&
                            IsValidVrmSource(_selectedObject) &&
                            !_isExporting;

            using (new EditorGUI.DisabledGroupScope(!canExport))
            {
                var buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };

                if (GUILayout.Button(_isExporting ? "Exporting..." : "⚡ Export VRM", buttonStyle, GUILayout.Height(50)))
                {
                    ExecuteQuickExport();
                }
            }

            // ショートカット表示
            EditorGUILayout.LabelField("Shortcut: Ctrl+Shift+E", EditorStyles.centeredGreyMiniLabel);
        }

        private void ExecuteQuickExport()
        {
            if (_selectedObject == null || _selectedPreset == null) return;

            _isExporting = true;

            try
            {
                var outputPath = GetOutputPath();
                var outputDir = Path.GetDirectoryName(outputPath);

                // 出力フォルダ作成
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 複製を作成
                var cloned = Instantiate(_selectedObject);
                cloned.name = _selectedObject.name + "_Export";

                try
                {
                    // プリセットからProcessor設定を適用
                    ApplyPresetToProcessors(_selectedPreset);

                    // コンテキスト作成
                    var context = new ExportContext(_selectedObject)
                    {
                        ClonedRoot = cloned,
                        OutputPath = outputPath,
                        VrmVersion = _selectedPreset.vrmVersion
                    };

                    // パイプライン実行
                    var pipelineResult = _pipeline.Execute(context);

                    // レポート生成
                    var report = ExportReport.FromPipelineResult(pipelineResult, context);

                    if (pipelineResult.Success)
                    {
                        // UniVRMでExport
                        ExportVrm(cloned, outputPath, _selectedPreset.vrmVersion);

                        // レポート保存
                        ReportManager.LogReport(report);
                        ReportManager.SaveReport(report);

                        Debug.Log($"[FBX4VRM] Quick export completed: {outputPath}");

                        // 成功通知
                        ShowExportSuccessDialog(outputPath, report);
                    }
                    else
                    {
                        Debug.LogError($"[FBX4VRM] Quick export failed at {pipelineResult.StoppedAtProcessorId}");
                        ExportReportWindow.Show(report);
                    }
                }
                finally
                {
                    DestroyImmediate(cloned);
                }
            }
            finally
            {
                _isExporting = false;
                Repaint();
            }
        }

        private void ApplyPresetToProcessors(ExportPreset preset)
        {
            // 無効化Processorを設定
            foreach (var processor in _pipeline.Processors)
            {
                processor.Enabled = true;

                if (preset.disabledProcessors != null &&
                    preset.disabledProcessors.Contains(processor.Id))
                {
                    processor.Enabled = false;
                }

                // 個別設定
                if (processor.Id == "liltoon_convert" && !preset.enableLilToonConversion)
                {
                    processor.Enabled = false;
                }
                if (processor.Id == "gltf_value_clamp" && !preset.enableHdrClamp)
                {
                    processor.Enabled = false;
                }
                if (processor.Id == "expressions_setup" && !preset.enableExpressionMapping)
                {
                    processor.Enabled = false;
                }
                if (processor.Id == "springbone_convert" && !preset.enableSpringBoneConversion)
                {
                    processor.Enabled = false;
                }
            }
        }

        private void ExportVrm(GameObject root, string path, int vrmVersion)
        {
            if (vrmVersion == 1)
            {
                var settings = new UniGLTF.GltfExportSettings();
                UniVRM10.VRM10ObjectMeta meta = null;

                if (root.TryGetComponent<UniVRM10.Vrm10Instance>(out var vrm10))
                {
                    meta = vrm10.Vrm?.Meta;
                }

                // メタデータがない場合はデフォルトを作成
                if (meta == null)
                {
                    meta = new UniVRM10.VRM10ObjectMeta
                    {
                        Name = root.name,
                        Version = "1.0",
                        Authors = new System.Collections.Generic.List<string> { "Unknown" },
                        CopyrightInformation = "",
                        ContactInformation = "",
                        References = new System.Collections.Generic.List<string>(),
                        ThirdPartyLicenses = "",
                        OtherLicenseUrl = "",
                        AvatarPermission = UniGLTF.Extensions.VRMC_vrm.AvatarPermissionType.onlyAuthor,
                        ViolentUsage = false,
                        SexualUsage = false,
                        CommercialUsage = UniGLTF.Extensions.VRMC_vrm.CommercialUsageType.personalNonProfit,
                        PoliticalOrReligiousUsage = false,
                        AntisocialOrHateUsage = false,
                        CreditNotation = UniGLTF.Extensions.VRMC_vrm.CreditNotationType.required,
                        Redistribution = false,
                        Modification = UniGLTF.Extensions.VRMC_vrm.ModificationType.prohibited
                    };
                }

                var bytes = UniVRM10.Vrm10Exporter.Export(settings, root, vrmMeta: meta);
                if (bytes != null)
                {
                    File.WriteAllBytes(path, bytes);
                }
            }
            else
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
            }
        }

        private void ShowExportSuccessDialog(string path, ExportReport report)
        {
            var warningCount = report.Notifications.Count(n => n.Level == "Warning");
            var message = $"VRM exported successfully!\n\n{path}";

            if (warningCount > 0)
            {
                message += $"\n\n⚠️ {warningCount} warning(s) - see report for details";
            }

            var result = EditorUtility.DisplayDialogComplex(
                "Export Complete",
                message,
                "Show in Finder",
                "Close",
                "View Report");

            switch (result)
            {
                case 0: // Show in Finder
                    EditorUtility.RevealInFinder(path);
                    break;
                case 2: // View Report
                    ExportReportWindow.Show(report);
                    break;
            }
        }

        private string GetOutputPath()
        {
            if (_selectedObject == null || _selectedPreset == null)
                return "";

            var folder = _selectedPreset.outputFolder;
            if (!Path.IsPathRooted(folder))
            {
                folder = Path.Combine(Application.dataPath, "..", folder);
            }

            return Path.Combine(folder, $"{_selectedObject.name}.vrm");
        }

        private (bool isValid, string message) ValidateObject(GameObject obj)
        {
            if (obj == null)
                return (false, "No object selected");

            if (!obj.scene.IsValid())
                return (false, "Select a scene object, not a prefab asset");

            var animator = obj.GetComponent<Animator>();
            if (animator == null)
                return (false, "No Animator component");

            if (animator.avatar == null || !animator.avatar.isHuman)
                return (false, "Avatar is not Humanoid");

            return (true, "✓ Ready to export");
        }

        private bool IsValidVrmSource(GameObject obj)
        {
            var (isValid, _) = ValidateObject(obj);
            return isValid;
        }
    }
}
