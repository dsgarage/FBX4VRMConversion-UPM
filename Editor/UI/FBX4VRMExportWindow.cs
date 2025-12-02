using System.IO;
using UnityEditor;
using UnityEngine;
using DSGarage.FBX4VRM.Editor.Localization;
using DSGarage.FBX4VRM.Editor.Logging;
using DSGarage.FBX4VRM.Editor.Processors;
using DSGarage.FBX4VRM.Editor.Reports;
using DSGarage.FBX4VRM.Editor.Settings;

namespace DSGarage.FBX4VRM.Editor.UI
{
    /// <summary>
    /// FBX4VRM Export ウィンドウ
    /// 詳細な変換オプションを提供
    /// </summary>
    public class FBX4VRMExportWindow : EditorWindow
    {
        private GameObject _selectedRoot;
        private string _outputPath = "";
        private int _vrmVersion = 0; // 0 = VRM 0.x, 1 = VRM 1.0
        private Vector2 _scrollPosition;

        // 詳細設定のFoldout状態
        private bool _showMaterialSettings = true;
        private bool _showSkeletonSettings = false;
        private bool _showExpressionSettings = false;
        private bool _showSpringBoneSettings = false;
        private bool _showOutputSettings = false;
        private bool _showLanguageSettings = false;

        // 変換設定
        private ConversionSettings _settings = new ConversionSettings();

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
                    // Prefab Assetの場合は追加情報を表示
                    if (IsPrefabAsset(_selectedRoot))
                    {
                        EditorGUILayout.HelpBox(
                            Localize.Get(
                                "✓ Prefabアセット - エクスポート時に一時的にシーンへロードされます",
                                "✓ Prefab Asset - Will be temporarily loaded to scene for export"),
                            MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(Localize.ReadyToExport, MessageType.None);
                    }
                }
            }
        }

        private void DrawExportSettings()
        {
            EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);

            // VRMバージョン選択
            _vrmVersion = EditorGUILayout.Popup("VRM Version",
                _vrmVersion,
                new string[] { "VRM 0.x", "VRM 1.0 (Avatar構築にバグあり)" });
            _settings.vrmVersion = _vrmVersion;

            if (_vrmVersion == 1)
            {
                EditorGUILayout.HelpBox(
                    Localize.Get(
                        "⚠️ VRM 1.0 はAvatar構築にバグがあります。問題が発生する場合は VRM 0.x をお使いください。",
                        "⚠️ VRM 1.0 has a bug in Avatar construction. Use VRM 0.x if you encounter issues."),
                    MessageType.Warning);
            }

            EditorGUILayout.Space(5);

            // ========== マテリアル変換設定 ==========
            _showMaterialSettings = EditorGUILayout.Foldout(_showMaterialSettings,
                Localize.Get("マテリアル変換", "Material Conversion"));
            if (_showMaterialSettings)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    _settings.enableLilToonConversion = EditorGUILayout.Toggle(
                        Localize.Get("lilToon → MToon 変換", "lilToon → MToon Conversion"),
                        _settings.enableLilToonConversion);

                    _settings.enableHdrClamp = EditorGUILayout.Toggle(
                        Localize.Get("HDR値クランプ (0-1)", "HDR Value Clamp (0-1)"),
                        _settings.enableHdrClamp);

                    _settings.enableOutlineConversion = EditorGUILayout.Toggle(
                        Localize.Get("Outline変換", "Outline Conversion"),
                        _settings.enableOutlineConversion);

                    _settings.transparentMode = (TransparentMode)EditorGUILayout.EnumPopup(
                        Localize.Get("透過処理モード", "Transparent Mode"),
                        _settings.transparentMode);
                }
                EditorGUI.indentLevel--;
            }

            // ========== 骨格・ポーズ設定 ==========
            _showSkeletonSettings = EditorGUILayout.Foldout(_showSkeletonSettings,
                Localize.Get("骨格・ポーズ", "Skeleton / Pose"));
            if (_showSkeletonSettings)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    _settings.enableTPoseNormalization = EditorGUILayout.Toggle(
                        Localize.Get("T-Pose正規化", "T-Pose Normalization"),
                        _settings.enableTPoseNormalization);

                    _settings.enableArmatureRotationBake = EditorGUILayout.Toggle(
                        Localize.Get("Armature回転ベイク", "Armature Rotation Bake"),
                        _settings.enableArmatureRotationBake);

                    using (new EditorGUI.DisabledGroupScope(_vrmVersion != 1))
                    {
                        _settings.enableBoneOrientationNormalization = EditorGUILayout.Toggle(
                            Localize.Get("ボーン向き正規化 (VRM 1.0)", "Bone Orientation Normalization (VRM 1.0)"),
                            _settings.enableBoneOrientationNormalization);
                    }
                }
                EditorGUI.indentLevel--;
            }

            // ========== 表情 (Expression) 設定 ==========
            _showExpressionSettings = EditorGUILayout.Foldout(_showExpressionSettings,
                Localize.Get("表情 (Expression)", "Expression"));
            if (_showExpressionSettings)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    _settings.enableExpressionAutoMapping = EditorGUILayout.Toggle(
                        Localize.Get("BlendShape自動マッピング", "BlendShape Auto Mapping"),
                        _settings.enableExpressionAutoMapping);

                    _settings.expressionNamingConvention = (NamingConvention)EditorGUILayout.EnumPopup(
                        Localize.Get("命名規則", "Naming Convention"),
                        _settings.expressionNamingConvention);
                }
                EditorGUI.indentLevel--;
            }

            // ========== 物理 (SpringBone) 設定 ==========
            _showSpringBoneSettings = EditorGUILayout.Foldout(_showSpringBoneSettings,
                Localize.Get("物理 (SpringBone)", "Physics (SpringBone)"));
            if (_showSpringBoneSettings)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    _settings.enableSpringBoneConversion = EditorGUILayout.Toggle(
                        Localize.Get("PhysBone/DynamicBone変換", "PhysBone/DynamicBone Conversion"),
                        _settings.enableSpringBoneConversion);

                    _settings.enableColliderConversion = EditorGUILayout.Toggle(
                        Localize.Get("Collider変換", "Collider Conversion"),
                        _settings.enableColliderConversion);
                }
                EditorGUI.indentLevel--;
            }

            // ========== 出力設定 ==========
            _showOutputSettings = EditorGUILayout.Foldout(_showOutputSettings,
                Localize.Get("出力設定", "Output Settings"));
            if (_showOutputSettings)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _outputPath = EditorGUILayout.TextField(
                            Localize.Get("出力フォルダ", "Output Folder"),
                            _outputPath);
                        if (GUILayout.Button("...", GUILayout.Width(30)))
                        {
                            var path = EditorUtility.OpenFolderPanel("Select Output Folder", _outputPath, "");
                            if (!string.IsNullOrEmpty(path))
                            {
                                _outputPath = path;
                            }
                        }
                    }
                    _settings.outputFolder = _outputPath;

                    _settings.fileNameMode = (FileNameMode)EditorGUILayout.EnumPopup(
                        Localize.Get("ファイル名", "File Name"),
                        _settings.fileNameMode);

                    if (_settings.fileNameMode == FileNameMode.Custom)
                    {
                        _settings.customFileName = EditorGUILayout.TextField(
                            Localize.Get("カスタムファイル名", "Custom File Name"),
                            _settings.customFileName);
                    }
                }
                EditorGUI.indentLevel--;
            }

            // ========== 言語設定 ==========
            _showLanguageSettings = EditorGUILayout.Foldout(_showLanguageSettings,
                Localize.Get("言語設定", "Language Settings"));
            if (_showLanguageSettings)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    var langIndex = Localize.IsJapanese ? 0 : 1;
                    var newLangIndex = EditorGUILayout.Popup("Language / 言語", langIndex, new[] { "日本語", "English" });
                    if (newLangIndex != langIndex)
                    {
                        Localize.CurrentLanguage = newLangIndex == 0 ? Localize.Language.Japanese : Localize.Language.English;
                        Repaint();
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// 現在の設定を取得（バグレポート用）
        /// </summary>
        public ConversionSettings GetCurrentSettings()
        {
            return _settings.Clone();
        }

        private void DrawExportButton()
        {
            var canExport = _selectedRoot != null &&
                            !string.IsNullOrEmpty(_outputPath) &&
                            string.IsNullOrEmpty(ValidateRoot(_selectedRoot));

            using (new EditorGUI.DisabledGroupScope(!canExport))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // プレビューボタン
                    if (GUILayout.Button("Preview", GUILayout.Height(40), GUILayout.Width(100)))
                    {
                        ShowPreview();
                    }

                    // Exportボタン
                    if (GUILayout.Button("Export VRM", GUILayout.Height(40)))
                    {
                        ExecuteExport();
                    }
                }
            }
        }

        private void ShowPreview()
        {
            if (_selectedRoot == null) return;

            ExportPreviewWindow.Show(_selectedRoot, _vrmVersion, _pipeline, (confirmed) =>
            {
                if (confirmed)
                {
                    ExecuteExport();
                }
            });
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

            // Animatorがあるか確認
            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                return Localize.NoAnimator;
            }

            // Humanoidか確認
            if (animator.avatar == null || !animator.avatar.isHuman)
            {
                return Localize.NotHumanoid;
            }

            return null; // Valid
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
        private void EnsureAvatarReference(GameObject originalAsset, GameObject source, GameObject clone)
        {
            var sourceAnimator = source.GetComponent<Animator>();
            var cloneAnimator = clone.GetComponent<Animator>();

            ExportLogger.LogInfo($"=== Avatar Reference Debug ===");
            ExportLogger.LogInfo($"Original Asset: {originalAsset?.name ?? "null"}");
            ExportLogger.LogInfo($"Source (instance): {source?.name ?? "null"}");
            ExportLogger.LogInfo($"Clone: {clone?.name ?? "null"}");

            if (sourceAnimator == null || cloneAnimator == null)
            {
                ExportLogger.LogWarning($"Animator missing - source: {sourceAnimator != null}, clone: {cloneAnimator != null}");
                return;
            }

            ExportLogger.LogInfo($"Source Animator.avatar: {(sourceAnimator.avatar != null ? sourceAnimator.avatar.name : "null")}");
            ExportLogger.LogInfo($"Clone Animator.avatar (before): {(cloneAnimator.avatar != null ? cloneAnimator.avatar.name : "null")}");

            // 1. ソースから直接コピー
            if (cloneAnimator.avatar == null && sourceAnimator.avatar != null)
            {
                cloneAnimator.avatar = sourceAnimator.avatar;
                ExportLogger.LogInfo($"Copied Avatar from source: {sourceAnimator.avatar.name}");
            }

            // 2. 元のアセットパスから検索
            if (cloneAnimator.avatar == null && originalAsset != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(originalAsset);
                ExportLogger.LogInfo($"Searching in asset path: {assetPath}");

                if (!string.IsNullOrEmpty(assetPath))
                {
                    // 直接サブアセットを検索
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    ExportLogger.LogInfo($"Found {allAssets.Length} sub-assets");

                    foreach (var asset in allAssets)
                    {
                        ExportLogger.LogInfo($"  Sub-asset: {asset?.name ?? "null"} ({asset?.GetType().Name ?? "null"})");
                        if (asset is Avatar avatar)
                        {
                            ExportLogger.LogInfo($"    -> Avatar found! isHuman: {avatar.isHuman}");
                            if (avatar.isHuman)
                            {
                                cloneAnimator.avatar = avatar;
                                ExportLogger.LogInfo($"Assigned Avatar: {avatar.name}");
                                break;
                            }
                        }
                    }
                }
            }

            // 3. 依存アセットから検索
            if (cloneAnimator.avatar == null && originalAsset != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(originalAsset);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var dependencies = AssetDatabase.GetDependencies(assetPath, true);
                    ExportLogger.LogInfo($"Searching {dependencies.Length} dependencies...");

                    foreach (var dep in dependencies)
                    {
                        if (dep.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
                            dep.EndsWith(".FBX", System.StringComparison.OrdinalIgnoreCase))
                        {
                            ExportLogger.LogInfo($"  FBX dependency: {dep}");
                            var fbxAssets = AssetDatabase.LoadAllAssetsAtPath(dep);
                            foreach (var asset in fbxAssets)
                            {
                                if (asset is Avatar avatar && avatar.isHuman)
                                {
                                    cloneAnimator.avatar = avatar;
                                    ExportLogger.LogInfo($"Found Avatar in FBX: {avatar.name}");
                                    break;
                                }
                            }
                        }
                        if (cloneAnimator.avatar != null) break;
                    }
                }
            }

            // 4. 元のAnimatorのruntimeAnimatorControllerから参照を探す
            if (cloneAnimator.avatar == null)
            {
                var originalAnimator = originalAsset?.GetComponent<Animator>();
                if (originalAnimator != null && originalAnimator.avatar != null)
                {
                    cloneAnimator.avatar = originalAnimator.avatar;
                    ExportLogger.LogInfo($"Copied Avatar from original asset Animator: {originalAnimator.avatar.name}");
                }
            }

            ExportLogger.LogInfo($"Clone Animator.avatar (after): {(cloneAnimator.avatar != null ? cloneAnimator.avatar.name : "null")}");

            if (cloneAnimator.avatar == null)
            {
                ExportLogger.LogError(Localize.Get(
                    "Avatarアセットが見つかりません。Humanoid設定を確認してください。",
                    "Avatar asset not found. Please check Humanoid settings."));
            }
            else
            {
                ExportLogger.LogInfo(Localize.Get(
                    $"Avatar確認完了: {cloneAnimator.avatar.name} (isHuman: {cloneAnimator.avatar.isHuman})",
                    $"Avatar confirmed: {cloneAnimator.avatar.name} (isHuman: {cloneAnimator.avatar.isHuman})"));
            }

            ExportLogger.LogInfo($"=== End Avatar Debug ===");
        }

        /// <summary>
        /// エクスポート直前の状態をデバッグ出力
        /// </summary>
        private void DebugLogExportState(GameObject exportRoot)
        {
            ExportLogger.LogInfo($"=== Export State Debug ===");
            ExportLogger.LogInfo($"Export Root: {exportRoot.name}");

            var animator = exportRoot.GetComponent<Animator>();
            if (animator != null)
            {
                ExportLogger.LogInfo($"Animator found");
                ExportLogger.LogInfo($"  avatar: {(animator.avatar != null ? animator.avatar.name : "NULL")}");
                ExportLogger.LogInfo($"  isHuman: {(animator.avatar != null ? animator.avatar.isHuman.ToString() : "N/A")}");
                ExportLogger.LogInfo($"  humanDescription.human.Length: {(animator.avatar != null ? animator.avatar.humanDescription.human.Length.ToString() : "N/A")}");

                if (animator.avatar != null && animator.avatar.isHuman)
                {
                    // Humanoidボーン情報を出力
                    var humanBones = animator.avatar.humanDescription.human;
                    ExportLogger.LogInfo($"  Human bones count: {humanBones.Length}");

                    // 必須ボーンの確認
                    var requiredBones = new[] { "Hips", "Spine", "Head", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot" };
                    foreach (var boneName in requiredBones)
                    {
                        var found = System.Array.Exists(humanBones, h => h.humanName == boneName);
                        if (!found)
                        {
                            ExportLogger.LogWarning($"  Missing bone: {boneName}");
                        }
                    }
                }
            }
            else
            {
                ExportLogger.LogError("No Animator component on export root!");
            }

            // SkinnedMeshRendererの確認
            var smrs = exportRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            ExportLogger.LogInfo($"SkinnedMeshRenderer count: {smrs.Length}");
            foreach (var smr in smrs)
            {
                ExportLogger.LogInfo($"  SMR: {smr.name}, bones: {smr.bones?.Length ?? 0}, rootBone: {smr.rootBone?.name ?? "null"}");
            }

            ExportLogger.LogInfo($"=== End Export State Debug ===");
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

            // Prefab Assetの場合は一時的にシーンへインスタンス化
            GameObject sourceInstance = null;
            bool isPrefabAsset = IsPrefabAsset(_selectedRoot);

            if (isPrefabAsset)
            {
                sourceInstance = (GameObject)PrefabUtility.InstantiatePrefab(_selectedRoot);
                sourceInstance.name = _selectedRoot.name + "_TempInstance";
                ExportLogger.LogInfo(Localize.Get(
                    $"Prefabアセットをシーンにロード: {_selectedRoot.name}",
                    $"Loaded prefab asset to scene: {_selectedRoot.name}"));
            }

            var sourceRoot = isPrefabAsset ? sourceInstance : _selectedRoot;

            // 複製を作成（非破壊処理）
            var cloned = Instantiate(sourceRoot);
            cloned.name = _selectedRoot.name + "_Export";

            // Avatarアセット参照を確実に維持
            EnsureAvatarReference(_selectedRoot, sourceRoot, cloned);

            // デバッグ: エクスポート直前の状態を確認
            DebugLogExportState(cloned);

            try
            {
                // コンテキスト作成
                var context = new ExportContext(sourceRoot)
                {
                    ClonedRoot = cloned,
                    OutputPath = fullPath,
                    VrmVersion = _vrmVersion
                };

                // パイプライン実行
                _lastResult = _pipeline.Execute(context);

                // レポート生成
                var report = ExportReport.FromPipelineResult(_lastResult, context);

                // 不具合報告用にスクリーンショットを撮影してキャッシュ（DestroyImmediate前に行う）
                Debug.Log($"[FBX4VRMExport] Creating bug report for model: {cloned.name}");
                try
                {
                    BugReportService.CreateFromExportResult(cloned, report, _settings, captureScreenshot: true);
                    Debug.Log($"[FBX4VRMExport] Bug report created, cached: {BugReportService.CachedReport?.ReportId ?? "null"}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[FBX4VRMExport] Failed to create bug report: {ex.Message}\n{ex.StackTrace}");
                }

                if (_lastResult.Success)
                {
                    // UniVRMでExport
                    ExportWithUniVRM(cloned, fullPath);

                    // レポート保存
                    ReportManager.LogReport(report);
                    ReportManager.SaveReport(report);

                    // パイプライン完了ログ
                    ExportLogger.LogPipelineComplete(true, fullPath);

                    // レポートウィンドウを表示
                    ExportReportWindow.Show(report);
                }
                else
                {
                    // パイプライン失敗ログ
                    ExportLogger.LogPipelineComplete(false);

                    // レポートウィンドウを表示（失敗時も）
                    ExportReportWindow.Show(report);
                }
            }
            finally
            {
                // 複製を削除
                DestroyImmediate(cloned);

                // Prefab Assetから作成した一時インスタンスを削除
                if (sourceInstance != null)
                {
                    DestroyImmediate(sourceInstance);
                    ExportLogger.LogInfo(Localize.Get(
                        "一時インスタンスを削除しました",
                        "Temporary instance removed"));
                }
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

            // VRM10Objectコンポーネントからメタ取得、なければデフォルト作成
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
                if (File.Exists(path))
                {
                    ExportLogger.LogInfo(Localize.Get(
                        $"既存ファイルを上書き: {path}",
                        $"Overwriting existing file: {path}"));
                }
                File.WriteAllBytes(path, bytes);
            }
            else
            {
                throw new System.Exception("VRM 0.x export returned null");
            }
        }
    }
}
