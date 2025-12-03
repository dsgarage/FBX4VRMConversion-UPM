using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using DSGarage.FBX4VRM.Editor.Reports;
using DSGarage.FBX4VRM.Editor.Localization;

namespace DSGarage.FBX4VRM.Editor.UI
{
    /// <summary>
    /// アバター登録ウィンドウ
    /// 未登録アバターの登録と、パッケージ情報・スクリーンショットのアップロード
    /// </summary>
    public class AvatarRegistrationWindow : EditorWindow
    {
        [MenuItem("Tools/FBX4VRM/Avatar Registration", false, 51)]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarRegistrationWindow>();
            window.titleContent = new GUIContent("Avatar Registration");
            window.minSize = new Vector2(500, 700);
            window.Show();
        }

        public static void Show(BugReportData report)
        {
            var window = GetWindow<AvatarRegistrationWindow>();
            window.titleContent = new GUIContent("Avatar Registration");
            window.minSize = new Vector2(500, 700);
            window._report = report;
            window.InitializeFromReport();
            window.Show();
        }

        private BugReportData _report;
        private Vector2 _scrollPosition;

        // 登録情報
        private string _avatarName = "";
        private string _displayName = "";
        private string _version = "1.0.0";
        private string _packagePath = "";

        // パッケージ解析結果
        private PackageStructure _packageStructure;
        private List<PrefabInfo> _humanoidPrefabs;
        private bool _isAnalyzing;

        // スクリーンショット
        private Dictionary<string, Texture2D> _prefabScreenshots = new Dictionary<string, Texture2D>();
        private Dictionary<string, byte[]> _prefabScreenshotBytes = new Dictionary<string, byte[]>();
        private bool _isCapturing;
        private string _captureStatus = "";

        // 送信状態
        private bool _isSending;
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.None;

        // 進捗
        private float _progress;
        private string _progressMessage = "";

        private void InitializeFromReport()
        {
            if (_report == null) return;

            _avatarName = _report.ModelName ?? "";
            _displayName = _avatarName;
            _version = "1.0.0";

            // パッケージパスを推測（Assetsフォルダ内のモデル名フォルダを検索）
            TryFindPackagePath();
        }

        private void TryFindPackagePath()
        {
            if (string.IsNullOrEmpty(_avatarName)) return;

            // Assets直下でモデル名を含むフォルダを検索
            var assetsPath = Application.dataPath;
            var directories = Directory.GetDirectories(assetsPath, "*", SearchOption.TopDirectoryOnly);

            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.IndexOf(_avatarName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _packagePath = "Assets/" + dirName;
                    Debug.Log($"[AvatarRegistration] Found package path: {_packagePath}");
                    AnalyzePackage();
                    break;
                }
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawBasicInfo();
            EditorGUILayout.Space(10);

            DrawPackageInfo();
            EditorGUILayout.Space(10);

            DrawPrefabList();
            EditorGUILayout.Space(10);

            DrawScreenshots();
            EditorGUILayout.Space(10);

            DrawProgress();
            EditorGUILayout.Space(10);

            DrawActions();

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(
                Localize.Get("アバター登録", "Avatar Registration"),
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                Localize.Get(
                    "アバターを登録すると、FBX4VRMの品質向上に貢献できます。\n" +
                    "パッケージ情報とスクリーンショットがサーバーにアップロードされます。",
                    "Registering an avatar helps improve FBX4VRM quality.\n" +
                    "Package information and screenshots will be uploaded to the server."),
                MessageType.Info);
        }

        private void DrawBasicInfo()
        {
            EditorGUILayout.LabelField(
                Localize.Get("基本情報", "Basic Information"),
                EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // アバター名（内部名）
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        Localize.Get("アバター名", "Avatar Name"),
                        GUILayout.Width(100));
                    _avatarName = EditorGUILayout.TextField(_avatarName);
                }

                // 表示名
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        Localize.Get("表示名", "Display Name"),
                        GUILayout.Width(100));
                    _displayName = EditorGUILayout.TextField(_displayName);
                }

                // バージョン
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        Localize.Get("バージョン", "Version"),
                        GUILayout.Width(100));
                    _version = EditorGUILayout.TextField(_version);
                }
            }
        }

        private void DrawPackageInfo()
        {
            EditorGUILayout.LabelField(
                Localize.Get("パッケージ情報", "Package Information"),
                EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // パッケージパス
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        Localize.Get("パス", "Path"),
                        GUILayout.Width(100));

                    _packagePath = EditorGUILayout.TextField(_packagePath);

                    if (GUILayout.Button(
                        Localize.Get("選択", "Select"),
                        GUILayout.Width(60)))
                    {
                        SelectPackageFolder();
                    }

                    if (GUILayout.Button(
                        Localize.Get("解析", "Analyze"),
                        GUILayout.Width(60)))
                    {
                        AnalyzePackage();
                    }
                }

                // 解析結果
                if (_isAnalyzing)
                {
                    EditorGUILayout.LabelField(
                        Localize.Get("解析中...", "Analyzing..."));
                }
                else if (_packageStructure != null)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField(
                        Localize.Get("解析結果", "Analysis Result"),
                        EditorStyles.boldLabel);

                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.LabelField(
                            Localize.Get("ルートフォルダ", "Root Folder"),
                            _packageStructure.root_folder);
                        EditorGUILayout.LabelField(
                            Localize.Get("フォルダ数", "Folder Count"),
                            _packageStructure.folders?.Count.ToString() ?? "0");

                        if (_packageStructure.file_counts != null)
                        {
                            foreach (var kvp in _packageStructure.file_counts)
                            {
                                EditorGUILayout.LabelField($"  {kvp.Key}", $"{kvp.Value} files");
                            }
                        }
                    }
                }
            }
        }

        private void DrawPrefabList()
        {
            var prefabCount = _humanoidPrefabs?.Count ?? 0;
            EditorGUILayout.LabelField(
                Localize.Get($"Humanoidプレハブ ({prefabCount})", $"Humanoid Prefabs ({prefabCount})"),
                EditorStyles.boldLabel);

            if (_humanoidPrefabs == null || _humanoidPrefabs.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    Localize.Get(
                        "パッケージを解析するとHumanoidプレハブが表示されます。",
                        "Analyze the package to show Humanoid prefabs."),
                    MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (var prefab in _humanoidPrefabs)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // プレハブ名
                        EditorGUILayout.LabelField(Path.GetFileName(prefab.path), GUILayout.Width(200));

                        // ボーン数
                        EditorGUILayout.LabelField(
                            Localize.Get($"ボーン: {prefab.bone_count}", $"Bones: {prefab.bone_count}"),
                            GUILayout.Width(100));

                        // スクリーンショット状態
                        var hasScreenshot = _prefabScreenshots.ContainsKey(prefab.path);
                        var statusText = hasScreenshot
                            ? Localize.Get("撮影済み", "Captured")
                            : Localize.Get("未撮影", "Not captured");
                        EditorGUILayout.LabelField(statusText, GUILayout.Width(80));
                    }
                }
            }
        }

        private void DrawScreenshots()
        {
            EditorGUILayout.LabelField(
                Localize.Get("マルチアングルスクリーンショット", "Multi-Angle Screenshots"),
                EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_isCapturing)
                {
                    EditorGUILayout.LabelField(_captureStatus);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        Localize.Get(
                            "各プレハブを10方向から撮影します（前後左右+上下+斜め4方向）。\n" +
                            "Aポーズで撮影されます。",
                            "Captures each prefab from 10 angles (front/back/left/right/top/bottom + 4 diagonals).\n" +
                            "Will be captured in A-pose."),
                        MessageType.Info);

                    using (new EditorGUI.DisabledScope(_humanoidPrefabs == null || _humanoidPrefabs.Count == 0))
                    {
                        if (GUILayout.Button(
                            Localize.Get("全プレハブを撮影", "Capture All Prefabs"),
                            GUILayout.Height(30)))
                        {
                            CaptureAllPrefabs();
                        }
                    }
                }

                // 撮影済みスクリーンショットのプレビュー
                if (_prefabScreenshots.Count > 0)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField(
                        Localize.Get("プレビュー", "Preview"),
                        EditorStyles.boldLabel);

                    foreach (var kvp in _prefabScreenshots)
                    {
                        EditorGUILayout.LabelField(Path.GetFileName(kvp.Key));
                        var texture = kvp.Value;
                        if (texture != null)
                        {
                            var maxWidth = position.width - 60;
                            var aspect = (float)texture.height / texture.width;
                            var height = Mathf.Min(maxWidth * aspect, 150);
                            var rect = GUILayoutUtility.GetRect(maxWidth, height);
                            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                        }
                    }
                }
            }
        }

        private void DrawProgress()
        {
            if (_progress > 0 && _progress < 1)
            {
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(GUILayout.Height(20)),
                    _progress,
                    _progressMessage);
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // キャンセル
                if (GUILayout.Button(
                    Localize.Get("キャンセル", "Cancel"),
                    GUILayout.Height(30)))
                {
                    Close();
                }

                // 登録ボタン
                using (new EditorGUI.DisabledScope(_isSending || string.IsNullOrEmpty(_avatarName)))
                {
                    var buttonStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontStyle = FontStyle.Bold
                    };

                    if (GUILayout.Button(
                        _isSending
                            ? Localize.Get("送信中...", "Sending...")
                            : Localize.Get("アバターを登録", "Register Avatar"),
                        buttonStyle,
                        GUILayout.Height(30)))
                    {
                        RegisterAvatar();
                    }
                }
            }
        }

        private void SelectPackageFolder()
        {
            var path = EditorUtility.OpenFolderPanel(
                Localize.Get("パッケージフォルダを選択", "Select Package Folder"),
                Application.dataPath,
                "");

            if (!string.IsNullOrEmpty(path))
            {
                // Assets/からの相対パスに変換
                if (path.StartsWith(Application.dataPath))
                {
                    _packagePath = "Assets" + path.Substring(Application.dataPath.Length);
                }
                else
                {
                    _packagePath = path;
                }
                AnalyzePackage();
            }
        }

        private void AnalyzePackage()
        {
            if (string.IsNullOrEmpty(_packagePath))
            {
                _statusMessage = Localize.Get("パッケージパスを指定してください", "Please specify package path");
                _statusType = MessageType.Warning;
                return;
            }

            _isAnalyzing = true;
            _statusMessage = "";

            try
            {
                // パッケージ構造を解析
                _packageStructure = PackageAnalyzer.AnalyzePackage(_packagePath);

                // Humanoidプレハブを検索
                _humanoidPrefabs = PackageAnalyzer.GetHumanoidPrefabs(_packagePath);

                Debug.Log($"[AvatarRegistration] Found {_humanoidPrefabs.Count} humanoid prefabs");

                _statusMessage = Localize.Get(
                    $"解析完了: {_humanoidPrefabs.Count}個のHumanoidプレハブを発見",
                    $"Analysis complete: Found {_humanoidPrefabs.Count} Humanoid prefabs");
                _statusType = MessageType.Info;
            }
            catch (Exception e)
            {
                _statusMessage = $"Error: {e.Message}";
                _statusType = MessageType.Error;
                Debug.LogError($"[AvatarRegistration] Error analyzing package: {e}");
            }
            finally
            {
                _isAnalyzing = false;
                Repaint();
            }
        }

        private void CaptureAllPrefabs()
        {
            if (_humanoidPrefabs == null || _humanoidPrefabs.Count == 0)
            {
                _statusMessage = Localize.Get("撮影するプレハブがありません", "No prefabs to capture");
                _statusType = MessageType.Warning;
                return;
            }

            _isCapturing = true;
            _prefabScreenshots.Clear();
            _prefabScreenshotBytes.Clear();

            try
            {
                var settings = new MultiAngleCapture.CaptureSettings
                {
                    SingleImageSize = 512,
                    Columns = 5,
                    IncludeDiagonals = true
                };

                for (int i = 0; i < _humanoidPrefabs.Count; i++)
                {
                    var prefab = _humanoidPrefabs[i];
                    _captureStatus = Localize.Get(
                        $"撮影中: {i + 1}/{_humanoidPrefabs.Count} - {Path.GetFileName(prefab.path)}",
                        $"Capturing: {i + 1}/{_humanoidPrefabs.Count} - {Path.GetFileName(prefab.path)}");
                    _progress = (float)i / _humanoidPrefabs.Count;
                    Repaint();

                    // プレハブを撮影（Aポーズ）
                    var imageData = MultiAngleCapture.CapturePrefabMultiAngle(prefab.path, PoseType.APose);

                    if (imageData != null && imageData.Length > 0)
                    {
                        // テクスチャに変換
                        var texture = new Texture2D(2, 2);
                        texture.LoadImage(imageData);

                        _prefabScreenshots[prefab.path] = texture;
                        _prefabScreenshotBytes[prefab.path] = imageData;

                        Debug.Log($"[AvatarRegistration] Captured: {prefab.path} ({imageData.Length} bytes)");
                    }
                    else
                    {
                        Debug.LogWarning($"[AvatarRegistration] Failed to capture: {prefab.path}");
                    }
                }

                _statusMessage = Localize.Get(
                    $"撮影完了: {_prefabScreenshots.Count}個のスクリーンショット",
                    $"Capture complete: {_prefabScreenshots.Count} screenshots");
                _statusType = MessageType.Info;
            }
            catch (Exception e)
            {
                _statusMessage = $"Error: {e.Message}";
                _statusType = MessageType.Error;
                Debug.LogError($"[AvatarRegistration] Error capturing: {e}");
            }
            finally
            {
                _isCapturing = false;
                _progress = 0;
                _captureStatus = "";
                Repaint();
            }
        }

        private void RegisterAvatar()
        {
            if (string.IsNullOrEmpty(_avatarName))
            {
                _statusMessage = Localize.Get("アバター名を入力してください", "Please enter avatar name");
                _statusType = MessageType.Warning;
                return;
            }

            _isSending = true;
            _progress = 0;
            _progressMessage = Localize.Get("登録中...", "Registering...");

            // 非同期で登録処理を実行
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // Step 1: パッケージ情報をアップロード
                    _progress = 0.2f;
                    _progressMessage = Localize.Get("パッケージ情報をアップロード中...", "Uploading package info...");
                    Repaint();

                    if (_packageStructure != null)
                    {
                        // プレハブパスのリストを作成
                        var prefabPaths = new List<string>();
                        if (_humanoidPrefabs != null)
                        {
                            foreach (var prefab in _humanoidPrefabs)
                            {
                                prefabPaths.Add(prefab.path);
                            }
                        }

                        BugReportService.UploadPackageInfo(
                            _avatarName,
                            _version,
                            _packageStructure,
                            prefabPaths,
                            Application.unityVersion,
                            (success, error) =>
                            {
                                if (!success)
                                {
                                    Debug.LogWarning($"[AvatarRegistration] Package info upload failed: {error}");
                                }
                            });
                    }

                    // Step 2: スクリーンショットをアップロード
                    _progress = 0.5f;
                    _progressMessage = Localize.Get("スクリーンショットをアップロード中...", "Uploading screenshots...");
                    Repaint();

                    int uploadedCount = 0;
                    foreach (var kvp in _prefabScreenshotBytes)
                    {
                        var prefabPath = kvp.Key;
                        var imageData = kvp.Value;

                        BugReportService.UploadPrefabScreenshot(
                            _avatarName,
                            _version,
                            prefabPath,
                            imageData,
                            PoseType.APose,
                            (success, githubUrl, error) =>
                            {
                                if (success)
                                {
                                    uploadedCount++;
                                    Debug.Log($"[AvatarRegistration] Screenshot uploaded: {prefabPath} -> {githubUrl}");
                                }
                                else
                                {
                                    Debug.LogWarning($"[AvatarRegistration] Screenshot upload failed: {error}");
                                }
                            });

                        _progress = 0.5f + 0.4f * ((float)uploadedCount / _prefabScreenshotBytes.Count);
                        Repaint();
                    }

                    // 完了
                    _progress = 1f;
                    _statusMessage = Localize.Get(
                        "登録完了！ご協力ありがとうございます。",
                        "Registration complete! Thank you for your contribution.");
                    _statusType = MessageType.Info;
                }
                catch (Exception e)
                {
                    _statusMessage = $"Error: {e.Message}";
                    _statusType = MessageType.Error;
                    Debug.LogError($"[AvatarRegistration] Error: {e}");
                }
                finally
                {
                    _isSending = false;
                    _progress = 0;
                    _progressMessage = "";
                    Repaint();
                }
            };
        }

        private void OnDisable()
        {
            // テクスチャをクリーンアップ
            foreach (var texture in _prefabScreenshots.Values)
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }
            _prefabScreenshots.Clear();
            _prefabScreenshotBytes.Clear();
        }
    }
}
