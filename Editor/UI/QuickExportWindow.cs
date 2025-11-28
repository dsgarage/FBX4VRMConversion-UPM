using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DSGarage.FBX4VRM.Editor.Localization;
using DSGarage.FBX4VRM.Editor.Logging;
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
                // シーンオブジェクトまたはPrefab Assetの場合に選択
                if (IsValidVrmSource(obj))
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

            // VRChatプリセットをデフォルトで選択
            if (_selectedPreset == null && _availablePresets.Count > 0)
            {
                // VRChatプリセットを優先的に探す
                var vrchatIndex = _availablePresets.FindIndex(p => p.presetName == "VRChat");
                if (vrchatIndex >= 0)
                {
                    _selectedPresetIndex = vrchatIndex;
                    _selectedPreset = _availablePresets[vrchatIndex];
                }
                else
                {
                    _selectedPresetIndex = 0;
                    _selectedPreset = _availablePresets[0];
                }
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
                var messageType = isValid
                    ? (IsPrefabAsset(_selectedObject) ? MessageType.Info : MessageType.None)
                    : MessageType.Warning;
                EditorGUILayout.HelpBox(message, messageType);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    Localize.Get("HumanoidモデルをシーンまたはAssetsから選択してください", "Select a Humanoid model from the scene or Assets"),
                    MessageType.Warning);
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
                    EditorGUILayout.LabelField("Version", _selectedPreset.vrmVersion == 1 ? "VRM 1.0 (Avatar構築にバグあり)" : "VRM 0.x");
                    if (_selectedPreset.vrmVersion == 1)
                    {
                        EditorGUILayout.HelpBox(
                            Localize.Get(
                                "⚠️ VRM 1.0 はAvatar構築にバグがあります。",
                                "⚠️ VRM 1.0 has a bug in Avatar construction."),
                            MessageType.Warning);
                    }
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

            // Prefab Assetの場合は一時的にシーンへインスタンス化
            GameObject sourceInstance = null;
            bool isPrefabAsset = IsPrefabAsset(_selectedObject);

            try
            {
                var outputPath = GetOutputPath();
                var outputDir = Path.GetDirectoryName(outputPath);

                // 出力フォルダ作成
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                if (isPrefabAsset)
                {
                    sourceInstance = (GameObject)PrefabUtility.InstantiatePrefab(_selectedObject);
                    sourceInstance.name = _selectedObject.name + "_TempInstance";
                    ExportLogger.LogInfo(Localize.Get(
                        $"Prefabアセットをシーンにロード: {_selectedObject.name}",
                        $"Loaded prefab asset to scene: {_selectedObject.name}"));
                }

                var sourceRoot = isPrefabAsset ? sourceInstance : _selectedObject;

                // 複製を作成
                var cloned = Instantiate(sourceRoot);
                cloned.name = _selectedObject.name + "_Export";

                // Avatarアセット参照を確実に維持
                EnsureAvatarReference(sourceRoot, cloned);

                try
                {
                    // プリセットからProcessor設定を適用
                    ApplyPresetToProcessors(_selectedPreset);

                    // コンテキスト作成
                    var context = new ExportContext(sourceRoot)
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

                        // パイプライン完了ログ
                        ExportLogger.LogPipelineComplete(true, outputPath);

                        // 成功通知
                        ShowExportSuccessDialog(outputPath, report);
                    }
                    else
                    {
                        // パイプライン失敗ログ
                        ExportLogger.LogPipelineComplete(false);
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
                // Prefab Assetから作成した一時インスタンスを削除
                if (sourceInstance != null)
                {
                    DestroyImmediate(sourceInstance);
                    ExportLogger.LogInfo(Localize.Get(
                        "一時インスタンスを削除しました",
                        "Temporary instance removed"));
                }

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
                    if (File.Exists(path))
                    {
                        ExportLogger.LogInfo(Localize.Get(
                            $"既存ファイルを上書き: {path}",
                            $"Overwriting existing file: {path}"));
                    }
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
                    if (File.Exists(path))
                    {
                        ExportLogger.LogInfo(Localize.Get(
                            $"既存ファイルを上書き: {path}",
                            $"Overwriting existing file: {path}"));
                    }
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
                return (false, Localize.Get("オブジェクトが選択されていません", "No object selected"));

            var animator = obj.GetComponent<Animator>();
            if (animator == null)
                return (false, Localize.NoAnimator);

            if (animator.avatar == null || !animator.avatar.isHuman)
                return (false, Localize.NotHumanoid);

            // Prefab Assetの場合は追加情報を表示
            if (IsPrefabAsset(obj))
            {
                return (true, Localize.Get(
                    "✓ Prefabアセット - エクスポート時に一時的にシーンへロードされます",
                    "✓ Prefab Asset - Will be temporarily loaded to scene for export"));
            }

            return (true, Localize.ReadyToExport);
        }

        private bool IsValidVrmSource(GameObject obj)
        {
            var (isValid, _) = ValidateObject(obj);
            return isValid;
        }

        /// <summary>
        /// オブジェクトがAssets内のPrefabかどうかを判定
        /// </summary>
        private bool IsPrefabAsset(GameObject obj)
        {
            if (obj == null) return false;
            return !obj.scene.IsValid() && PrefabUtility.IsPartOfPrefabAsset(obj);
        }

        /// <summary>
        /// クローンにAvatarアセット参照を確実にコピー
        /// </summary>
        private void EnsureAvatarReference(GameObject source, GameObject clone)
        {
            var sourceAnimator = source.GetComponent<Animator>();
            var cloneAnimator = clone.GetComponent<Animator>();

            if (sourceAnimator == null || cloneAnimator == null) return;

            // Avatarが null または参照が切れている場合
            if (cloneAnimator.avatar == null && sourceAnimator.avatar != null)
            {
                cloneAnimator.avatar = sourceAnimator.avatar;
                ExportLogger.LogInfo(Localize.Get(
                    $"Avatarアセットを明示的にコピー: {sourceAnimator.avatar.name}",
                    $"Explicitly copied Avatar asset: {sourceAnimator.avatar.name}"));
            }

            // Prefabの場合、元のFBXからAvatarを取得を試みる
            if (cloneAnimator.avatar == null)
            {
                var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    // 依存アセットからAvatarを探す（FBXのサブアセット含む）
                    var dependencies = AssetDatabase.GetDependencies(prefabPath, true);
                    foreach (var dep in dependencies)
                    {
                        // FBXやモデルファイルの場合、サブアセットからAvatarを探す
                        var allAssets = AssetDatabase.LoadAllAssetsAtPath(dep);
                        foreach (var asset in allAssets)
                        {
                            if (asset is Avatar avatar && avatar.isHuman)
                            {
                                cloneAnimator.avatar = avatar;
                                ExportLogger.LogInfo(Localize.Get(
                                    $"FBXサブアセットからAvatarを検出: {avatar.name} ({dep})",
                                    $"Found Avatar from FBX sub-asset: {avatar.name} ({dep})"));
                                break;
                            }
                        }
                        if (cloneAnimator.avatar != null) break;
                    }
                }
            }

            // 元のPrefabアセットから直接Avatarを探す
            if (cloneAnimator.avatar == null && _selectedObject != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(_selectedObject);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    foreach (var asset in allAssets)
                    {
                        if (asset is Avatar avatar && avatar.isHuman)
                        {
                            cloneAnimator.avatar = avatar;
                            ExportLogger.LogInfo(Localize.Get(
                                $"PrefabアセットからAvatarを検出: {avatar.name}",
                                $"Found Avatar from Prefab asset: {avatar.name}"));
                            break;
                        }
                    }
                }
            }

            // それでもAvatarがない場合は警告
            if (cloneAnimator.avatar == null)
            {
                ExportLogger.LogWarning(Localize.Get(
                    "Avatarアセットが見つかりません。Humanoid設定を確認してください。",
                    "Avatar asset not found. Please check Humanoid settings."));
            }
            else
            {
                ExportLogger.LogInfo(Localize.Get(
                    $"Avatar確認: {cloneAnimator.avatar.name} (isHuman: {cloneAnimator.avatar.isHuman})",
                    $"Avatar confirmed: {cloneAnimator.avatar.name} (isHuman: {cloneAnimator.avatar.isHuman})"));
            }
        }
    }
}
