using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using DSGarage.FBX4VRM.Editor.Localization;
using DSGarage.FBX4VRM.Editor.Reports;
using DSGarage.FBX4VRM.Editor.Processors;

namespace DSGarage.FBX4VRM.Editor.UI
{
    /// <summary>
    /// シェーダー/マテリアルチェックウィンドウ
    /// VRM変換後のマテリアルとシェーダーの状態を確認する
    /// </summary>
    public class ShaderCheckWindow : EditorWindow
    {
        [MenuItem("Tools/FBX4VRM/Shader Check Window", false, 61)]
        public static void ShowWindow()
        {
            var window = GetWindow<ShaderCheckWindow>();
            window.titleContent = new GUIContent("Shader Check");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        /// <summary>
        /// 指定したモデルでウィンドウを開く
        /// </summary>
        public static void Show(GameObject model)
        {
            var window = GetWindow<ShaderCheckWindow>();
            window.titleContent = new GUIContent("Shader Check");
            window.minSize = new Vector2(500, 600);
            window._targetModel = model;
            window.RefreshMaterialList();
            window.Show();
        }

        /// <summary>
        /// VRMファイルパスを指定してウィンドウを開く
        /// </summary>
        public static void ShowWithVrmPath(string vrmPath)
        {
            var window = GetWindow<ShaderCheckWindow>();
            window.titleContent = new GUIContent("Shader Check");
            window.minSize = new Vector2(500, 600);
            window._vrmFilePath = vrmPath;
            window._vrmPathReadOnly = true;
            window.Show();
        }

        /// <summary>
        /// A-poseスクリーンショットを設定してウィンドウを開く
        /// </summary>
        public static void ShowWithScreenshot(GameObject model, byte[] screenshotBytes)
        {
            var window = GetWindow<ShaderCheckWindow>();
            window.titleContent = new GUIContent("Shader Check");
            window.minSize = new Vector2(500, 600);
            window._targetModel = model;
            window._screenshotBytes = screenshotBytes;
            window.RefreshMaterialList();
            window.UpdateScreenshotPreview();
            window.Show();
        }

        // VRMファイル
        private string _vrmFilePath;
        private bool _vrmPathReadOnly;
        private GameObject _targetModel;
        private GameObject _loadedVrmInstance;

        // スクリーンショット（A-pose）
        private byte[] _screenshotBytes;
        private Texture2D _screenshotPreview;

        // 撮影したスクリーンショット
        private byte[] _capturedScreenshotBytes;
        private Texture2D _capturedScreenshotPreview;

        // マテリアル情報
        private List<MaterialInfo> _materialList = new List<MaterialInfo>();
        private Vector2 _scrollPosition;
        private bool _showMaterialList = true;
        private bool _showShaderDetails = true;

        // フィルター
        private bool _showLilToon = true;
        private bool _showMToon = true;
        private bool _showStandard = true;
        private bool _showOther = true;

        /// <summary>
        /// マテリアル情報
        /// </summary>
        private class MaterialInfo
        {
            public Material Material;
            public string ShaderName;
            public ShaderCategory Category;
            public List<string> Warnings = new List<string>();
            public Renderer[] UsedByRenderers;

            public enum ShaderCategory
            {
                LilToon,
                MToon,
                Standard,
                Other
            }
        }

        private void OnEnable()
        {
            // 選択中のオブジェクトを自動設定
            if (_targetModel == null && Selection.activeGameObject != null)
            {
                var renderers = Selection.activeGameObject.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    _targetModel = Selection.activeGameObject;
                    RefreshMaterialList();
                }
            }
        }

        private void OnDisable()
        {
            CleanupScreenshotPreview();
            CleanupCapturedScreenshotPreview();
        }

        private void OnDestroy()
        {
            CleanupLoadedVrm();
            CleanupScreenshotPreview();
            CleanupCapturedScreenshotPreview();
        }

        private void CleanupScreenshotPreview()
        {
            if (_screenshotPreview != null)
            {
                DestroyImmediate(_screenshotPreview);
                _screenshotPreview = null;
            }
        }

        private void CleanupCapturedScreenshotPreview()
        {
            if (_capturedScreenshotPreview != null)
            {
                DestroyImmediate(_capturedScreenshotPreview);
                _capturedScreenshotPreview = null;
            }
        }

        private void CleanupLoadedVrm()
        {
            if (_loadedVrmInstance != null)
            {
                DestroyImmediate(_loadedVrmInstance);
                _loadedVrmInstance = null;
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawScreenshotSection();
            EditorGUILayout.Space(10);

            DrawModelSection();
            EditorGUILayout.Space(10);

            DrawSummary();
            EditorGUILayout.Space(10);

            DrawFilters();
            EditorGUILayout.Space(5);

            DrawMaterialList();
            EditorGUILayout.Space(10);

            DrawReportSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawScreenshotSection()
        {
            // スクリーンショット表示（A-poseまたは撮影したもの）
            Texture2D displayPreview = null;
            byte[] displayBytes = null;

            // 撮影したスクリーンショットを優先、なければA-poseを表示
            if (_capturedScreenshotPreview != null)
            {
                displayPreview = _capturedScreenshotPreview;
                displayBytes = _capturedScreenshotBytes;
            }
            else if (_screenshotPreview != null)
            {
                displayPreview = _screenshotPreview;
                displayBytes = _screenshotBytes;
            }

            // スクリーンショット表示
            if (displayPreview != null)
            {
                var maxWidth = position.width - 30;
                var aspect = (float)displayPreview.height / displayPreview.width;
                var height = Mathf.Min(maxWidth * aspect, 280);

                var rect = GUILayoutUtility.GetRect(maxWidth, height);
                GUI.DrawTexture(rect, displayPreview, ScaleMode.ScaleToFit);

                EditorGUILayout.LabelField(
                    $"{displayPreview.width} x {displayPreview.height} ({displayBytes?.Length / 1024 ?? 0} KB)",
                    EditorStyles.centeredGreyMiniLabel);

                EditorGUILayout.Space(5);
            }

            // 撮影ボタン
            var canCapture = _targetModel != null;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canCapture))
                {
                    if (GUILayout.Button(
                        Localize.Get("マルチアングル撮影", "Multi-Angle Capture"),
                        GUILayout.Height(28)))
                    {
                        CaptureMultiAngleScreenshot();
                    }

                    if (GUILayout.Button(
                        Localize.Get("Sceneビュー撮影", "Scene View Capture"),
                        GUILayout.Height(28)))
                    {
                        CaptureSceneViewScreenshot();
                    }
                }

                // 保存ボタン
                using (new EditorGUI.DisabledScope(displayPreview == null))
                {
                    if (GUILayout.Button(
                        Localize.Get("保存", "Save"),
                        GUILayout.Width(60),
                        GUILayout.Height(28)))
                    {
                        SaveCurrentScreenshot();
                    }
                }
            }
        }

        private void SaveCurrentScreenshot()
        {
            byte[] bytesToSave = null;

            if (_capturedScreenshotBytes != null && _capturedScreenshotBytes.Length > 0)
            {
                bytesToSave = _capturedScreenshotBytes;
            }
            else if (_screenshotBytes != null && _screenshotBytes.Length > 0)
            {
                bytesToSave = _screenshotBytes;
            }

            if (bytesToSave == null || bytesToSave.Length == 0)
            {
                Debug.LogWarning("[ShaderCheck] No screenshot to save");
                return;
            }

            var defaultName = _targetModel != null
                ? $"{_targetModel.name}_shadercheck_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                : $"shadercheck_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            var path = EditorUtility.SaveFilePanel(
                "Save Screenshot",
                Application.dataPath,
                defaultName,
                "png");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, bytesToSave);
                Debug.Log($"[ShaderCheck] Screenshot saved: {path}");
                EditorUtility.RevealInFinder(path);
            }
        }

        private void UpdateScreenshotPreview()
        {
            if (_screenshotBytes == null || _screenshotBytes.Length == 0)
            {
                return;
            }

            CleanupScreenshotPreview();
            _screenshotPreview = new Texture2D(2, 2);
            _screenshotPreview.LoadImage(_screenshotBytes);
        }

        private void DrawModelSection()
        {
            // モデルが既に設定されている場合は表示しない
            if (_targetModel != null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // VRMロードボタン（VRMパスが設定されている場合）
                if (!string.IsNullOrEmpty(_vrmFilePath))
                {
                    EditorGUILayout.LabelField(
                        Localize.Get("VRMファイル", "VRM File"),
                        EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField(_vrmFilePath);
                    }
                    EditorGUILayout.Space(5);

                    if (GUILayout.Button(
                        Localize.Get("VRMをシーンにロード", "Load VRM to Scene"),
                        GUILayout.Height(28)))
                    {
                        LoadVrmToScene(_vrmFilePath);
                    }
                }
                else
                {
                    // モデル選択
                    EditorGUI.BeginChangeCheck();
                    _targetModel = EditorGUILayout.ObjectField(
                        Localize.Get("モデル", "Model"),
                        _targetModel,
                        typeof(GameObject),
                        true) as GameObject;

                    if (EditorGUI.EndChangeCheck())
                    {
                        RefreshMaterialList();
                    }

                    EditorGUILayout.HelpBox(
                        Localize.Get(
                            "Hierarchyからモデルを選択してください。",
                            "Select a model from Hierarchy."),
                        MessageType.Info);
                }
            }
        }

        private void CaptureMultiAngleScreenshot()
        {
            if (_targetModel == null) return;

            _capturedScreenshotBytes = MultiAngleCapture.CaptureMultiAngle(_targetModel);

            if (_capturedScreenshotBytes != null && _capturedScreenshotBytes.Length > 0)
            {
                CleanupCapturedScreenshotPreview();
                _capturedScreenshotPreview = new Texture2D(2, 2);
                _capturedScreenshotPreview.LoadImage(_capturedScreenshotBytes);
                Debug.Log($"[ShaderCheck] Multi-angle screenshot captured: {_capturedScreenshotBytes.Length} bytes");
            }
        }

        private void CaptureSceneViewScreenshot()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                Debug.LogWarning("[ShaderCheck] No active Scene View");
                return;
            }

            // SceneViewをレンダリング
            sceneView.Repaint();

            var width = (int)sceneView.position.width;
            var height = (int)sceneView.position.height;

            var rt = new RenderTexture(width, height, 24);
            var camera = sceneView.camera;
            var prevRT = camera.targetTexture;

            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();

            camera.targetTexture = prevRT;
            RenderTexture.active = null;
            DestroyImmediate(rt);

            _capturedScreenshotBytes = screenshot.EncodeToPNG();

            CleanupCapturedScreenshotPreview();
            _capturedScreenshotPreview = screenshot;

            Debug.Log($"[ShaderCheck] Scene view screenshot captured: {_capturedScreenshotBytes.Length} bytes");
        }

        private void LoadVrmToScene(string vrmPath)
        {
            if (string.IsNullOrEmpty(vrmPath) || !File.Exists(vrmPath))
            {
                Debug.LogError($"[ShaderCheck] VRM file not found: {vrmPath}");
                return;
            }

            CleanupLoadedVrm();

            // VRMをAssetsにコピーしてインポート
            var fileName = Path.GetFileName(vrmPath);
            var destFolder = "Assets/_ShaderCheck_Temp";
            if (!AssetDatabase.IsValidFolder(destFolder))
            {
                AssetDatabase.CreateFolder("Assets", "_ShaderCheck_Temp");
            }

            var destPath = $"{destFolder}/{fileName}";

            if (File.Exists(destPath))
            {
                AssetDatabase.DeleteAsset(destPath);
            }

            File.Copy(vrmPath, destPath, true);
            AssetDatabase.Refresh();

            var vrmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
            if (vrmPrefab == null)
            {
                Debug.LogError($"[ShaderCheck] Failed to load VRM as prefab: {destPath}");
                return;
            }

            _loadedVrmInstance = Instantiate(vrmPrefab);
            _loadedVrmInstance.name = Path.GetFileNameWithoutExtension(fileName);
            _loadedVrmInstance.transform.position = Vector3.zero;
            _loadedVrmInstance.transform.rotation = Quaternion.identity;

            _targetModel = _loadedVrmInstance;
            RefreshMaterialList();

            Selection.activeGameObject = _loadedVrmInstance;

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log($"[ShaderCheck] VRM loaded to scene: {_loadedVrmInstance.name}");
        }

        private void DrawSummary()
        {
            if (_targetModel == null || _materialList.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(
                Localize.Get("サマリー", "Summary"),
                EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var lilToonCount = _materialList.Count(m => m.Category == MaterialInfo.ShaderCategory.LilToon);
                var mtoonCount = _materialList.Count(m => m.Category == MaterialInfo.ShaderCategory.MToon);
                var standardCount = _materialList.Count(m => m.Category == MaterialInfo.ShaderCategory.Standard);
                var otherCount = _materialList.Count(m => m.Category == MaterialInfo.ShaderCategory.Other);
                var warningCount = _materialList.Sum(m => m.Warnings.Count);

                EditorGUILayout.LabelField($"{Localize.Get("総マテリアル数", "Total Materials")}: {_materialList.Count}");

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"lilToon: {lilToonCount}", GUILayout.Width(100));
                    EditorGUILayout.LabelField($"MToon: {mtoonCount}", GUILayout.Width(100));
                    EditorGUILayout.LabelField($"Standard: {standardCount}", GUILayout.Width(100));
                    EditorGUILayout.LabelField($"{Localize.Get("その他", "Other")}: {otherCount}", GUILayout.Width(100));
                }

                if (warningCount > 0)
                {
                    var warningStyle = new GUIStyle(EditorStyles.label);
                    warningStyle.normal.textColor = new Color(0.9f, 0.6f, 0.1f);
                    EditorGUILayout.LabelField(
                        $"{Localize.Get("警告", "Warnings")}: {warningCount}",
                        warningStyle);
                }

                // VRM互換性チェック
                if (lilToonCount > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        Localize.Get(
                            $"lilToonマテリアルが{lilToonCount}個検出されました。\nVRM出力時にMToonへの変換が必要です。",
                            $"{lilToonCount} lilToon material(s) detected.\nConversion to MToon required for VRM export."),
                        MessageType.Warning);
                }

                if (otherCount > 0)
                {
                    EditorGUILayout.HelpBox(
                        Localize.Get(
                            $"非対応シェーダーが{otherCount}個検出されました。\nVRM出力時にUnlit/Standard系に変換される可能性があります。",
                            $"{otherCount} unsupported shader(s) detected.\nMay be converted to Unlit/Standard on VRM export."),
                        MessageType.Warning);
                }
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    Localize.Get("フィルター:", "Filter:"),
                    GUILayout.Width(60));

                _showLilToon = GUILayout.Toggle(_showLilToon, "lilToon", "Button", GUILayout.Width(70));
                _showMToon = GUILayout.Toggle(_showMToon, "MToon", "Button", GUILayout.Width(60));
                _showStandard = GUILayout.Toggle(_showStandard, "Standard", "Button", GUILayout.Width(70));
                _showOther = GUILayout.Toggle(_showOther, Localize.Get("その他", "Other"), "Button", GUILayout.Width(60));
            }
        }

        private void DrawMaterialList()
        {
            _showMaterialList = EditorGUILayout.Foldout(_showMaterialList,
                Localize.Get("マテリアル一覧", "Material List"), true);

            if (!_showMaterialList || _targetModel == null) return;

            if (_materialList.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    Localize.Get(
                        "マテリアルが見つかりません。",
                        "No materials found."),
                    MessageType.Warning);
                return;
            }

            var filteredList = _materialList.Where(m =>
                (m.Category == MaterialInfo.ShaderCategory.LilToon && _showLilToon) ||
                (m.Category == MaterialInfo.ShaderCategory.MToon && _showMToon) ||
                (m.Category == MaterialInfo.ShaderCategory.Standard && _showStandard) ||
                (m.Category == MaterialInfo.ShaderCategory.Other && _showOther)
            ).ToList();

            foreach (var matInfo in filteredList)
            {
                DrawMaterialEntry(matInfo);
            }
        }

        private void DrawMaterialEntry(MaterialInfo matInfo)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // カテゴリアイコン
                    var icon = GetCategoryIcon(matInfo.Category);
                    var iconContent = EditorGUIUtility.IconContent(icon);
                    GUILayout.Label(iconContent, GUILayout.Width(20), GUILayout.Height(20));

                    // マテリアル名
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField(matInfo.Material, typeof(Material), false);
                    }
                }

                EditorGUI.indentLevel++;

                // シェーダー名
                EditorGUILayout.LabelField(
                    Localize.Get("シェーダー", "Shader"),
                    matInfo.ShaderName,
                    EditorStyles.miniLabel);

                // カテゴリ
                EditorGUILayout.LabelField(
                    Localize.Get("カテゴリ", "Category"),
                    GetCategoryDisplayName(matInfo.Category),
                    EditorStyles.miniLabel);

                // lilToonの場合、バリアント情報を表示
                if (matInfo.Category == MaterialInfo.ShaderCategory.LilToon && matInfo.Material != null)
                {
                    var variant = LilToonDetectProcessor.GetMaterialVariant(matInfo.Material);
                    EditorGUILayout.LabelField(
                        Localize.Get("バリアント", "Variant"),
                        variant,
                        EditorStyles.miniLabel);
                }

                // 警告
                foreach (var warning in matInfo.Warnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }
        }

        private string GetCategoryIcon(MaterialInfo.ShaderCategory category)
        {
            return category switch
            {
                MaterialInfo.ShaderCategory.LilToon => "console.warnicon",
                MaterialInfo.ShaderCategory.MToon => "TestPassed",
                MaterialInfo.ShaderCategory.Standard => "TestNormal",
                MaterialInfo.ShaderCategory.Other => "console.erroricon.sml",
                _ => "TestNormal"
            };
        }

        private string GetCategoryDisplayName(MaterialInfo.ShaderCategory category)
        {
            return category switch
            {
                MaterialInfo.ShaderCategory.LilToon => "lilToon (要変換)",
                MaterialInfo.ShaderCategory.MToon => "MToon (VRM対応)",
                MaterialInfo.ShaderCategory.Standard => "Standard/Unlit",
                MaterialInfo.ShaderCategory.Other => Localize.Get("その他 (非対応の可能性)", "Other (May be unsupported)"),
                _ => "Unknown"
            };
        }

        private void DrawReportSection()
        {
            EditorGUILayout.LabelField(
                Localize.Get("不具合報告", "Report Issue"),
                EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    Localize.Get(
                        "シェーダー/マテリアルに問題がある場合、ここから不具合を報告できます。\nスクリーンショットとマテリアル情報が自動的に添付されます。",
                        "If there are shader/material issues, you can report them here.\nScreenshot and material information will be attached automatically."),
                    MessageType.Info);

                var canReport = _targetModel != null;

                using (new EditorGUI.DisabledScope(!canReport))
                {
                    var reportButtonStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontStyle = FontStyle.Bold,
                        fontSize = 13
                    };
                    reportButtonStyle.normal.textColor = new Color(0.9f, 0.4f, 0.4f);

                    if (GUILayout.Button(
                        Localize.Get("シェーダー不具合を報告", "Report Shader Issue"),
                        reportButtonStyle,
                        GUILayout.Height(35)))
                    {
                        OpenBugReportWithShaderInfo();
                    }
                }

                if (!canReport)
                {
                    EditorGUILayout.HelpBox(
                        Localize.Get(
                            "報告するにはモデルを選択してください。",
                            "Please select a model to report."),
                        MessageType.Warning);
                }
            }
        }

        private void RefreshMaterialList()
        {
            _materialList.Clear();

            if (_targetModel == null) return;

            var renderers = _targetModel.GetComponentsInChildren<Renderer>(true);
            var processedMaterials = new HashSet<Material>();

            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null || processedMaterials.Contains(mat)) continue;
                    processedMaterials.Add(mat);

                    var info = new MaterialInfo
                    {
                        Material = mat,
                        ShaderName = mat.shader?.name ?? "Unknown",
                        Category = CategorizeShader(mat),
                        UsedByRenderers = renderers.Where(r => r.sharedMaterials.Contains(mat)).ToArray()
                    };

                    // 警告をチェック
                    CheckMaterialWarnings(info);

                    _materialList.Add(info);
                }
            }

            Debug.Log($"[ShaderCheck] Found {_materialList.Count} materials");
        }

        private MaterialInfo.ShaderCategory CategorizeShader(Material material)
        {
            if (material == null || material.shader == null)
            {
                return MaterialInfo.ShaderCategory.Other;
            }

            var shaderName = material.shader.name;

            // lilToon
            if (LilToonDetectProcessor.IsLilToonMaterial(material))
            {
                return MaterialInfo.ShaderCategory.LilToon;
            }

            // MToon
            if (shaderName.Contains("MToon") || shaderName.Contains("VRM"))
            {
                return MaterialInfo.ShaderCategory.MToon;
            }

            // Standard/Unlit
            if (shaderName.StartsWith("Standard") ||
                shaderName.StartsWith("Unlit") ||
                shaderName.StartsWith("Universal Render Pipeline") ||
                shaderName.StartsWith("Lit") ||
                shaderName.Contains("glTF"))
            {
                return MaterialInfo.ShaderCategory.Standard;
            }

            return MaterialInfo.ShaderCategory.Other;
        }

        private void CheckMaterialWarnings(MaterialInfo info)
        {
            if (info.Material == null) return;

            var mat = info.Material;

            // HDRエミッションチェック
            if (mat.HasProperty("_EmissionColor"))
            {
                var emission = mat.GetColor("_EmissionColor");
                if (emission.r > 1 || emission.g > 1 || emission.b > 1)
                {
                    info.Warnings.Add(
                        Localize.Get(
                            $"HDRエミッションカラーが検出されました ({emission.r:F2}, {emission.g:F2}, {emission.b:F2})。VRM出力時にクランプされます。",
                            $"HDR emission color detected ({emission.r:F2}, {emission.g:F2}, {emission.b:F2}). Will be clamped on VRM export."));
                }
            }

            // lilToon固有のチェック
            if (info.Category == MaterialInfo.ShaderCategory.LilToon)
            {
                // アウトライン
                if (mat.shader.name.Contains("Outline"))
                {
                    info.Warnings.Add(
                        Localize.Get(
                            "アウトラインシェーダーです。MToon変換時にアウトライン設定が必要です。",
                            "Outline shader detected. Outline settings required for MToon conversion."));
                }

                // 透過
                if (mat.shader.name.Contains("Transparent"))
                {
                    info.Warnings.Add(
                        Localize.Get(
                            "透過シェーダーです。MToon変換時にアルファモードの確認が必要です。",
                            "Transparent shader detected. Alpha mode verification required for MToon conversion."));
                }
            }

            // その他のシェーダー
            if (info.Category == MaterialInfo.ShaderCategory.Other)
            {
                info.Warnings.Add(
                    Localize.Get(
                        "VRM非対応シェーダーの可能性があります。出力時にUnlit/Standardに変換されることがあります。",
                        "May be unsupported for VRM. Could be converted to Unlit/Standard on export."));
            }
        }

        private void OpenBugReportWithShaderInfo()
        {
            if (_targetModel == null)
            {
                Debug.LogWarning("[ShaderCheck] No model selected for report");
                return;
            }

            // スクリーンショットがない場合は自動撮影
            if ((_screenshotBytes == null || _screenshotBytes.Length == 0) &&
                (_capturedScreenshotBytes == null || _capturedScreenshotBytes.Length == 0))
            {
                CaptureMultiAngleScreenshot();
            }

            // シェーダー情報を収集
            var shaderInfo = CollectShaderInfo();

            // BugReportDataを作成
            var report = BugReportData.Create(_targetModel.name, 0);

            // スクリーンショットを設定（撮影したものを優先、なければA-poseを使用）
            if (_capturedScreenshotBytes != null && _capturedScreenshotBytes.Length > 0)
            {
                report.ScreenshotBytes = _capturedScreenshotBytes;
                if (_capturedScreenshotPreview != null)
                {
                    report.screenshot.width = _capturedScreenshotPreview.width;
                    report.screenshot.height = _capturedScreenshotPreview.height;
                }
            }
            else if (_screenshotBytes != null && _screenshotBytes.Length > 0)
            {
                report.ScreenshotBytes = _screenshotBytes;
                if (_screenshotPreview != null)
                {
                    report.screenshot.width = _screenshotPreview.width;
                    report.screenshot.height = _screenshotPreview.height;
                }
            }

            report.user_comment = "";

            // モデル情報を収集
            report.CollectModelInfo(_targetModel);

            // シェーダー不具合カテゴリとして追加情報を設定
            report.AddWarning("ShaderCheck", "シェーダー不具合報告", shaderInfo);

            // BugReportServiceにキャッシュ
            BugReportService.CacheReport(report);

            // BugReportWindowを開く
            BugReportWindow.Show(report);

            Debug.Log($"[ShaderCheck] Opened BugReportWindow with shader info for: {_targetModel.name}");
        }

        private string CollectShaderInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Shader Check Report ===");
            sb.AppendLine();

            // サマリー
            var lilToonCount = _materialList.Count(m => m.Category == MaterialInfo.ShaderCategory.LilToon);
            var mtoonCount = _materialList.Count(m => m.Category == MaterialInfo.ShaderCategory.MToon);
            var standardCount = _materialList.Count(m => m.Category == MaterialInfo.ShaderCategory.Standard);
            var otherCount = _materialList.Count(m => m.Category == MaterialInfo.ShaderCategory.Other);

            sb.AppendLine("[Summary]");
            sb.AppendLine($"  Total Materials: {_materialList.Count}");
            sb.AppendLine($"  lilToon: {lilToonCount}");
            sb.AppendLine($"  MToon: {mtoonCount}");
            sb.AppendLine($"  Standard/Unlit: {standardCount}");
            sb.AppendLine($"  Other: {otherCount}");
            sb.AppendLine();

            // マテリアル詳細
            sb.AppendLine("[Materials]");
            foreach (var matInfo in _materialList)
            {
                sb.AppendLine($"  - {matInfo.Material?.name ?? "Unknown"}");
                sb.AppendLine($"    Shader: {matInfo.ShaderName}");
                sb.AppendLine($"    Category: {matInfo.Category}");

                if (matInfo.Category == MaterialInfo.ShaderCategory.LilToon && matInfo.Material != null)
                {
                    var variant = LilToonDetectProcessor.GetMaterialVariant(matInfo.Material);
                    sb.AppendLine($"    Variant: {variant}");
                }

                foreach (var warning in matInfo.Warnings)
                {
                    sb.AppendLine($"    WARNING: {warning}");
                }
            }

            // VRMファイル情報
            if (!string.IsNullOrEmpty(_vrmFilePath))
            {
                sb.AppendLine();
                sb.AppendLine("[VRM File]");
                sb.AppendLine($"  Path: {_vrmFilePath}");
                if (File.Exists(_vrmFilePath))
                {
                    var fileInfo = new FileInfo(_vrmFilePath);
                    sb.AppendLine($"  Size: {fileInfo.Length / 1024} KB");
                }
            }

            return sb.ToString();
        }
    }
}
