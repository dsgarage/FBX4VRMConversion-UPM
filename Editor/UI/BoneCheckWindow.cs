using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using DSGarage.FBX4VRM.Editor.Localization;
using DSGarage.FBX4VRM.Editor.Reports;
using DSGarage.FBX4VRM.Runtime;

namespace DSGarage.FBX4VRM.Editor.UI
{
    /// <summary>
    /// VRMモデルのボーンチェックウィンドウ
    /// VRMをロードし、AnimatorControllerを適用してボーンの動作確認・スクリーンショット撮影を行う
    /// </summary>
    public class BoneCheckWindow : EditorWindow
    {
        // Playモード自動実行用の静的設定
        private static string _pendingVrmPath;
        private static Action<byte[]> _pendingScreenshotCallback;
        private static bool _autoRunPending;

        [MenuItem("Tools/FBX4VRM/Bone Check Window", false, 60)]
        public static void ShowWindow()
        {
            var window = GetWindow<BoneCheckWindow>();
            window.titleContent = new GUIContent("Bone Check");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }

        /// <summary>
        /// 指定したモデルでウィンドウを開く
        /// </summary>
        public static void Show(GameObject model)
        {
            var window = GetWindow<BoneCheckWindow>();
            window.titleContent = new GUIContent("Bone Check");
            window.minSize = new Vector2(450, 600);
            window._targetModel = model;
            window.RefreshBoneMap();
            window.Show();
        }

        /// <summary>
        /// VRMファイルパスを指定してウィンドウを開く
        /// ExportReportWindowから呼ばれた場合、VRMパスは読み取り専用
        /// </summary>
        public static void ShowWithVrmPath(string vrmPath)
        {
            var window = GetWindow<BoneCheckWindow>();
            window.titleContent = new GUIContent("Bone Check");
            window.minSize = new Vector2(450, 600);
            window._vrmFilePath = vrmPath;
            window._vrmPathReadOnly = true; // ExportReportから開いた場合は読み取り専用
            window.Show();
        }

        /// <summary>
        /// VRMファイルパスとA-poseスクリーンショットを指定してウィンドウを開く
        /// </summary>
        public static void ShowWithVrmPathAndScreenshot(string vrmPath, byte[] screenshotBytes)
        {
            var window = GetWindow<BoneCheckWindow>();
            window.titleContent = new GUIContent("Bone Check");
            window.minSize = new Vector2(450, 600);
            window._vrmFilePath = vrmPath;
            window._vrmPathReadOnly = true;
            window._aposeScreenshotBytes = screenshotBytes;
            window.UpdateAposeScreenshotPreview();
            window.Show();
        }

        /// <summary>
        /// Playモードで自動的にVRMをロードし、A-ポーズでスクリーンショットを撮影
        /// </summary>
        /// <param name="vrmPath">VRMファイルパス</param>
        /// <param name="screenshotSavePath">スクリーンショット保存パス（nullの場合は自動生成）</param>
        /// <param name="onComplete">完了コールバック（スクリーンショットパス）</param>
        public static void RunAutoCheck(
            string vrmPath,
            string screenshotSavePath = null,
            Action<string> onComplete = null)
        {
            if (string.IsNullOrEmpty(vrmPath) || !File.Exists(vrmPath))
            {
                Debug.LogError($"[BoneCheckWindow] VRM file not found: {vrmPath}");
                return;
            }

            // スクリーンショット保存パスを生成
            if (string.IsNullOrEmpty(screenshotSavePath))
            {
                var modelName = Path.GetFileNameWithoutExtension(vrmPath);
                var directory = Path.Combine(Application.dataPath, "../FBX4VRM_Reports");
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                screenshotSavePath = Path.Combine(directory, $"{modelName}_apose_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            }

            _pendingVrmPath = vrmPath;
            _pendingScreenshotCallback = (bytes) => onComplete?.Invoke(screenshotSavePath);
            _autoRunPending = true;

            // BoneCheckRunnerを設定（PlayerPrefsで永続化）- A-ポーズで撮影
            BoneCheckRunner.Setup(vrmPath, screenshotSavePath, useAPose: true);

            // 完了フラグをクリア
            PlayerPrefs.DeleteKey("FBX4VRM_BoneCheck_Completed");
            PlayerPrefs.DeleteKey("FBX4VRM_BoneCheck_ResultPath");
            PlayerPrefs.DeleteKey("FBX4VRM_BoneCheck_Error");
            PlayerPrefs.Save();

            // Playモード開始前にBugReportWindowを閉じる（Playモード中にCachedReportがリセットされるため）
            CloseBugReportWindowIfOpen();

            // Playモード開始
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.isPlaying = true;

            Debug.Log($"[BoneCheckWindow] Starting A-Pose screenshot for: {vrmPath}");
        }

        /// <summary>
        /// Playモードで自動的にVRMをロードし、AnimatorControllerでスクリーンショットを撮影
        /// </summary>
        /// <param name="vrmPath">VRMファイルパス</param>
        /// <param name="screenshotSavePath">スクリーンショット保存パス</param>
        /// <param name="animatorResourcePath">Resources内のAnimatorControllerパス（拡張子なし）</param>
        /// <param name="animationWaitTime">アニメーション待機時間（秒）</param>
        /// <param name="onComplete">完了コールバック（スクリーンショットパス）</param>
        public static void RunAutoCheckWithAnimator(
            string vrmPath,
            string screenshotSavePath,
            string animatorResourcePath,
            float animationWaitTime = 1.0f,
            Action<string> onComplete = null)
        {
            if (string.IsNullOrEmpty(vrmPath) || !File.Exists(vrmPath))
            {
                Debug.LogError($"[BoneCheckWindow] VRM file not found: {vrmPath}");
                return;
            }

            if (string.IsNullOrEmpty(animatorResourcePath))
            {
                Debug.LogError("[BoneCheckWindow] AnimatorController resource path is empty");
                return;
            }

            // スクリーンショット保存パスを生成
            if (string.IsNullOrEmpty(screenshotSavePath))
            {
                var modelName = Path.GetFileNameWithoutExtension(vrmPath);
                var directory = Path.Combine(Application.dataPath, "../FBX4VRM_Reports");
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                screenshotSavePath = Path.Combine(directory, $"{modelName}_animation_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            }

            _pendingVrmPath = vrmPath;
            _pendingScreenshotCallback = (bytes) => onComplete?.Invoke(screenshotSavePath);
            _autoRunPending = true;

            // BoneCheckRunnerを設定（PlayerPrefsで永続化）- AnimatorControllerで撮影
            BoneCheckRunner.SetupWithAnimator(vrmPath, screenshotSavePath, animatorResourcePath, animationWaitTime);

            // 完了フラグをクリア
            PlayerPrefs.DeleteKey("FBX4VRM_BoneCheck_Completed");
            PlayerPrefs.DeleteKey("FBX4VRM_BoneCheck_ResultPath");
            PlayerPrefs.DeleteKey("FBX4VRM_BoneCheck_Error");
            PlayerPrefs.Save();

            // Playモード開始前にBugReportWindowを閉じる（Playモード中にCachedReportがリセットされるため）
            CloseBugReportWindowIfOpen();

            // Playモード開始
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.isPlaying = true;

            Debug.Log($"[BoneCheckWindow] Starting AnimatorController screenshot for: {vrmPath}, Controller: {animatorResourcePath}");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && _autoRunPending)
            {
                // BoneCheckRunnerをシーンに追加
                var runnerGo = new GameObject("BoneCheckRunner");
                runnerGo.AddComponent<BoneCheckRunner>();
                _autoRunPending = false;
                Debug.Log("[BoneCheckWindow] BoneCheckRunner added to scene");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

                // 結果を確認
                var completed = PlayerPrefs.GetInt("FBX4VRM_BoneCheck_Completed", 0) == 1;
                var resultPath = PlayerPrefs.GetString("FBX4VRM_BoneCheck_ResultPath", "");
                var error = PlayerPrefs.GetString("FBX4VRM_BoneCheck_Error", "");

                if (completed)
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogError($"[BoneCheckWindow] Bone check failed: {error}");
                        EditorUtility.DisplayDialog(
                            "Bone Check Error",
                            error,
                            "OK");
                    }
                    else if (!string.IsNullOrEmpty(resultPath) && File.Exists(resultPath))
                    {
                        Debug.Log($"[BoneCheckWindow] Bone check completed: {resultPath}");

                        // コールバック実行
                        _pendingScreenshotCallback?.Invoke(null);

                        // ダイアログで通知
                        if (EditorUtility.DisplayDialog(
                            "Bone Check Complete",
                            $"Screenshot saved:\n{resultPath}",
                            "Show in Finder",
                            "OK"))
                        {
                            EditorUtility.RevealInFinder(resultPath);
                        }
                    }

                    // クリーンアップ
                    PlayerPrefs.DeleteKey("FBX4VRM_BoneCheck_Completed");
                    PlayerPrefs.DeleteKey("FBX4VRM_BoneCheck_ResultPath");
                    PlayerPrefs.DeleteKey("FBX4VRM_BoneCheck_Error");
                    PlayerPrefs.Save();
                }

                _pendingVrmPath = null;
                _pendingScreenshotCallback = null;
            }
        }

        /// <summary>
        /// BugReportWindowが開いている場合は閉じる
        /// Playモード中にCachedReportがリセットされるため、事前に閉じておく
        /// </summary>
        private static void CloseBugReportWindowIfOpen()
        {
            var bugReportWindow = Resources.FindObjectsOfTypeAll<BugReportWindow>();
            if (bugReportWindow != null && bugReportWindow.Length > 0)
            {
                foreach (var window in bugReportWindow)
                {
                    Debug.Log("[BoneCheckWindow] Closing BugReportWindow before Play mode");
                    window.Close();
                }
            }
        }

        // VRMファイル
        private string _vrmFilePath;
        private bool _vrmPathReadOnly; // ExportReportから開いた場合は読み取り専用
        private GameObject _targetModel;
        private GameObject _loadedVrmInstance;

        // A-poseスクリーンショット（エクスポート時に撮影されたもの）
        private byte[] _aposeScreenshotBytes;
        private Texture2D _aposeScreenshotPreview;

        // AnimatorController
        private RuntimeAnimatorController _selectedController;
        private RuntimeAnimatorController _locomotionsController;
        private RuntimeAnimatorController _actionCheckController;
        private RuntimeAnimatorController _arPoseController;

        // UI状態
        private Vector2 _scrollPosition;
        private bool _showBoneList = true;
        private float _animationSpeed = 1.0f;

        // ボーン情報キャッシュ
        private Dictionary<HumanBodyBones, Transform> _boneMap;
        private Animator _animator;

        // Playモード前のボーン状態をキャッシュ（ボーン名で保持）
        private Dictionary<HumanBodyBones, string> _cachedBoneNames;
        private int _cachedBoneCount;
        private string _cachedModelName;

        // スクリーンショット
        private Texture2D _screenshotPreview;
        private byte[] _lastScreenshotBytes;
        private bool _userCapturedScreenshot; // ユーザーが手動で撮影したかどうか

        // デフォルトのAnimatorControllerパス
        private const string LocomotionsControllerPath = "Assets/UnityChan/Animators/UnityChanLocomotions.controller";
        private const string ActionCheckControllerPath = "Assets/UnityChan/Animators/UnityChanActionCheck.controller";
        private const string ARPoseControllerPath = "Assets/UnityChan/Animators/UnityChanARPose.controller";

        private void OnEnable()
        {
            LoadAnimatorControllers();

            // Playモード変更イベントを登録
            EditorApplication.playModeStateChanged += OnInstancePlayModeStateChanged;

            // 選択中のオブジェクトを自動設定
            if (_targetModel == null && Selection.activeGameObject != null)
            {
                var animator = Selection.activeGameObject.GetComponent<Animator>();
                if (animator != null && animator.isHuman)
                {
                    _targetModel = Selection.activeGameObject;
                    RefreshBoneMap();
                }
            }
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnInstancePlayModeStateChanged;
            CleanupScreenshotPreview();
        }

        private void OnInstancePlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // Playモード開始前にボーン情報をキャッシュ
                    CacheBoneInfo();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    // Playモード終了後、モデルを再検索してボーン情報を復元
                    RestoreModelAfterPlayMode();
                    break;
            }
        }

        private void CacheBoneInfo()
        {
            if (_boneMap == null || _boneMap.Count == 0) return;

            _cachedBoneNames = new Dictionary<HumanBodyBones, string>();
            foreach (var kvp in _boneMap)
            {
                if (kvp.Value != null)
                {
                    _cachedBoneNames[kvp.Key] = kvp.Value.name;
                }
            }
            _cachedBoneCount = _cachedBoneNames.Count;
            _cachedModelName = _targetModel != null ? _targetModel.name : null;

            Debug.Log($"[BoneCheck] Cached {_cachedBoneCount} bones for model: {_cachedModelName}");
        }

        private void RestoreModelAfterPlayMode()
        {
            // モデル名でシーン内を検索
            if (!string.IsNullOrEmpty(_cachedModelName))
            {
                var foundObject = GameObject.Find(_cachedModelName);
                if (foundObject != null)
                {
                    var animator = foundObject.GetComponent<Animator>();
                    if (animator != null && animator.isHuman)
                    {
                        _targetModel = foundObject;
                        _loadedVrmInstance = foundObject;
                        RefreshBoneMap();
                        Debug.Log($"[BoneCheck] Restored model after Play mode: {_cachedModelName}");
                        return;
                    }
                }
            }

            // 見つからない場合はキャッシュされたボーン名情報を保持
            // （DrawBoneStatusでキャッシュを使用して表示）
            Debug.Log($"[BoneCheck] Model not found after Play mode, using cached bone info");
        }

        private void OnDestroy()
        {
            CleanupLoadedVrm();
            CleanupScreenshotPreview();
            CleanupAposeScreenshotPreview();
        }

        private void CleanupScreenshotPreview()
        {
            if (_screenshotPreview != null)
            {
                DestroyImmediate(_screenshotPreview);
                _screenshotPreview = null;
            }
        }

        private void CleanupAposeScreenshotPreview()
        {
            if (_aposeScreenshotPreview != null)
            {
                DestroyImmediate(_aposeScreenshotPreview);
                _aposeScreenshotPreview = null;
            }
        }

        private void UpdateAposeScreenshotPreview()
        {
            if (_aposeScreenshotBytes == null || _aposeScreenshotBytes.Length == 0)
            {
                return;
            }

            CleanupAposeScreenshotPreview();
            _aposeScreenshotPreview = new Texture2D(2, 2);
            _aposeScreenshotPreview.LoadImage(_aposeScreenshotBytes);
        }

        private void CleanupLoadedVrm()
        {
            if (_loadedVrmInstance != null)
            {
                DestroyImmediate(_loadedVrmInstance);
                _loadedVrmInstance = null;
            }
        }

        private void LoadAnimatorControllers()
        {
            _locomotionsController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LocomotionsControllerPath);
            _actionCheckController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ActionCheckControllerPath);
            _arPoseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ARPoseControllerPath);

            // デフォルトでLocomotionsを選択（ボーンチェックに最適）
            if (_selectedController == null)
            {
                _selectedController = _locomotionsController;
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // スクリーンショット表示（撮影ボタンの上）
            DrawScreenshotDisplay();
            EditorGUILayout.Space(10);

            // モデルが未設定の場合のみVRMローダーを表示
            DrawModelSection();

            DrawAnimatorSelection();
            EditorGUILayout.Space(10);

            DrawPlaybackControls();
            EditorGUILayout.Space(10);

            DrawBoneStatus();
            EditorGUILayout.Space(10);

            DrawReportSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawScreenshotDisplay()
        {
            // ユーザーが手動で撮影した画像のみ表示（自動撮影されたものは表示しない）
            if (_screenshotPreview != null && _userCapturedScreenshot)
            {
                var maxWidth = position.width - 30;
                var aspect = (float)_screenshotPreview.height / _screenshotPreview.width;
                var height = Mathf.Min(maxWidth * aspect, 280);

                var rect = GUILayoutUtility.GetRect(maxWidth, height);
                GUI.DrawTexture(rect, _screenshotPreview, ScaleMode.ScaleToFit);

                EditorGUILayout.LabelField(
                    $"{_screenshotPreview.width} x {_screenshotPreview.height} ({_lastScreenshotBytes?.Length / 1024 ?? 0} KB)",
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
                using (new EditorGUI.DisabledScope(!_userCapturedScreenshot || _screenshotPreview == null))
                {
                    if (GUILayout.Button(
                        Localize.Get("保存", "Save"),
                        GUILayout.Width(60),
                        GUILayout.Height(28)))
                    {
                        SaveScreenshot();
                    }
                }
            }
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
                        RefreshBoneMap();
                    }

                    EditorGUILayout.HelpBox(
                        Localize.Get(
                            "Hierarchyからモデルを選択してください。",
                            "Select a model from Hierarchy."),
                        MessageType.Info);
                }
            }

            EditorGUILayout.Space(10);
        }

        private void LoadVrmToScene(string vrmPath)
        {
            if (string.IsNullOrEmpty(vrmPath) || !File.Exists(vrmPath))
            {
                Debug.LogError($"[BoneCheck] VRM file not found: {vrmPath}");
                return;
            }

            // 既存のインスタンスをクリーンアップ
            CleanupLoadedVrm();

            // VRMをAssetsにコピーしてインポート
            var fileName = Path.GetFileName(vrmPath);
            var destFolder = "Assets/_BoneCheck_Temp";
            if (!AssetDatabase.IsValidFolder(destFolder))
            {
                AssetDatabase.CreateFolder("Assets", "_BoneCheck_Temp");
            }

            var destPath = $"{destFolder}/{fileName}";

            // 既存ファイルを削除
            if (File.Exists(destPath))
            {
                AssetDatabase.DeleteAsset(destPath);
            }

            File.Copy(vrmPath, destPath, true);
            AssetDatabase.Refresh();

            // VRMプレファブをロード
            var vrmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
            if (vrmPrefab == null)
            {
                Debug.LogError($"[BoneCheck] Failed to load VRM as prefab: {destPath}");
                return;
            }

            // シーンにインスタンス化
            _loadedVrmInstance = Instantiate(vrmPrefab);
            _loadedVrmInstance.name = Path.GetFileNameWithoutExtension(fileName);
            _loadedVrmInstance.transform.position = Vector3.zero;
            _loadedVrmInstance.transform.rotation = Quaternion.identity;

            // ターゲットモデルとして設定
            _targetModel = _loadedVrmInstance;
            RefreshBoneMap();

            // 選択状態にする
            Selection.activeGameObject = _loadedVrmInstance;

            // シーンビューにフォーカス
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log($"[BoneCheck] VRM loaded to scene: {_loadedVrmInstance.name}");

            // UnityChanLocomotionsを自動適用（ボーンチェック用）
            if (_locomotionsController != null)
            {
                _selectedController = _locomotionsController;
                ApplyAnimatorController();
                Debug.Log($"[BoneCheck] Applied UnityChanLocomotions for bone verification");
            }
        }

        private void DrawAnimatorSelection()
        {
            EditorGUILayout.LabelField(
                Localize.Get("アニメーション選択", "Animation Selection"),
                EditorStyles.boldLabel);

            // プリセットボタン
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAnimatorButton("Locomotions", _locomotionsController);
                DrawAnimatorButton("ActionCheck", _actionCheckController);
                DrawAnimatorButton("ARPose", _arPoseController);
            }

            // カスタムコントローラー
            EditorGUILayout.Space(5);
            EditorGUI.BeginChangeCheck();
            _selectedController = EditorGUILayout.ObjectField(
                Localize.Get("カスタム", "Custom"),
                _selectedController,
                typeof(RuntimeAnimatorController),
                false) as RuntimeAnimatorController;

            if (EditorGUI.EndChangeCheck())
            {
                ApplyAnimatorController();
            }

            // コントローラーが見つからない場合の警告
            if (_locomotionsController == null && _actionCheckController == null && _arPoseController == null)
            {
                EditorGUILayout.HelpBox(
                    Localize.Get(
                        "UnityChanのAnimatorControllerが見つかりません。\n" +
                        "Assets/UnityChan/Animators/ にインポートしてください。",
                        "UnityChan AnimatorControllers not found.\n" +
                        "Import to Assets/UnityChan/Animators/."),
                    MessageType.Warning);
            }
        }

        private void DrawAnimatorButton(string label, RuntimeAnimatorController controller)
        {
            var available = controller != null;
            using (new EditorGUI.DisabledScope(!available))
            {
                var isSelected = _selectedController == controller;
                var style = isSelected
                    ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold }
                    : GUI.skin.button;

                if (GUILayout.Button(label, style, GUILayout.Height(28)))
                {
                    _selectedController = controller;
                    ApplyAnimatorController();
                }
            }
        }

        private void DrawPlaybackControls()
        {
            EditorGUILayout.LabelField(
                Localize.Get("再生コントロール", "Playback Controls"),
                EditorStyles.boldLabel);

            var canPlay = _targetModel != null && _selectedController != null;

            // Play/Stopボタン（エディタのPlayモード）
            using (new EditorGUILayout.HorizontalScope())
            {
                var isPlaying = EditorApplication.isPlaying;
                var playButtonLabel = isPlaying
                    ? Localize.Get("停止", "Stop")
                    : Localize.Get("▶ 再生してボーンチェック", "▶ Play & Check Bones");

                var playButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 14
                };

                using (new EditorGUI.DisabledScope(!canPlay && !isPlaying))
                {
                    if (GUILayout.Button(playButtonLabel, playButtonStyle, GUILayout.Height(35)))
                    {
                        if (isPlaying)
                        {
                            EditorApplication.isPlaying = false;
                        }
                        else
                        {
                            // AnimatorControllerを適用してからPlayモード開始
                            ApplyAnimatorController();
                            EditorApplication.isPlaying = true;
                        }
                    }
                }
            }

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    Localize.Get(
                        "「再生してボーンチェック」を押すとPlayモードでアニメーションが確認できます。\n" +
                        "Humanoid Avatarの変換ミスがあると、動きがおかしくなります。",
                        "Press 'Play & Check Bones' to verify animation in Play mode.\n" +
                        "If Humanoid Avatar conversion failed, the animation will look wrong."),
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    Localize.Get(
                        "Playモード中です。アニメーションを確認してください。\n" +
                        "問題がある場合はHumanoid Avatarの設定を確認してください。",
                        "Play mode active. Check the animation.\n" +
                        "If there are issues, verify Humanoid Avatar settings."),
                    MessageType.Warning);
            }

            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canPlay))
                {
                    if (GUILayout.Button(
                        Localize.Get("適用", "Apply"),
                        GUILayout.Height(28)))
                    {
                        ApplyAnimatorController();
                    }

                    if (GUILayout.Button(
                        Localize.Get("リセット", "Reset"),
                        GUILayout.Height(28)))
                    {
                        ResetAnimator();
                    }
                }
            }

            // アニメーション速度
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    Localize.Get("速度", "Speed"),
                    GUILayout.Width(40));
                _animationSpeed = EditorGUILayout.Slider(_animationSpeed, 0f, 2f);

                if (_animator != null)
                {
                    _animator.speed = _animationSpeed;
                }
            }
        }

        private void CaptureMultiAngleScreenshot(bool isUserAction = true)
        {
            if (_targetModel == null) return;

            _lastScreenshotBytes = MultiAngleCapture.CaptureMultiAngle(_targetModel);

            if (_lastScreenshotBytes != null && _lastScreenshotBytes.Length > 0)
            {
                CleanupScreenshotPreview();
                _screenshotPreview = new Texture2D(2, 2);
                _screenshotPreview.LoadImage(_lastScreenshotBytes);
                _userCapturedScreenshot = isUserAction; // ユーザーアクションの場合のみtrueに設定
                Debug.Log($"[BoneCheck] Multi-angle screenshot captured: {_lastScreenshotBytes.Length} bytes");
            }
        }

        private void CaptureSceneViewScreenshot()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                Debug.LogWarning("[BoneCheck] No active Scene View");
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

            _lastScreenshotBytes = screenshot.EncodeToPNG();

            CleanupScreenshotPreview();
            _screenshotPreview = screenshot;
            _userCapturedScreenshot = true; // ユーザーが手動で撮影した

            Debug.Log($"[BoneCheck] Scene view screenshot captured: {_lastScreenshotBytes.Length} bytes");
        }

        private void SaveScreenshot()
        {
            if (_lastScreenshotBytes == null || _lastScreenshotBytes.Length == 0)
            {
                Debug.LogWarning("[BoneCheck] No screenshot to save");
                return;
            }

            var defaultName = _targetModel != null
                ? $"{_targetModel.name}_bonecheck_{System.DateTime.Now:yyyyMMdd_HHmmss}.png"
                : $"bonecheck_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";

            var path = EditorUtility.SaveFilePanel(
                "Save Screenshot",
                Application.dataPath,
                defaultName,
                "png");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, _lastScreenshotBytes);
                Debug.Log($"[BoneCheck] Screenshot saved: {path}");
                EditorUtility.RevealInFinder(path);
            }
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
                        "ボーンの動作に問題がある場合、ここから不具合を報告できます。\n" +
                        "スクリーンショットとボーン情報が自動的に添付されます。",
                        "If there are issues with bone behavior, you can report them here.\n" +
                        "Screenshot and bone information will be attached automatically."),
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
                        Localize.Get("ボーン不具合を報告", "Report Bone Issue"),
                        reportButtonStyle,
                        GUILayout.Height(35)))
                    {
                        OpenBugReportWithBoneInfo();
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

        /// <summary>
        /// ボーン情報を収集してBugReportWindowを開く
        /// </summary>
        private void OpenBugReportWithBoneInfo()
        {
            if (_targetModel == null)
            {
                Debug.LogWarning("[BoneCheck] No model selected for report");
                return;
            }

            // スクリーンショットがない場合は自動撮影（表示はしない）
            if (_lastScreenshotBytes == null || _lastScreenshotBytes.Length == 0)
            {
                CaptureMultiAngleScreenshot(isUserAction: false);
            }

            // ボーン情報を収集
            var boneInfo = CollectBoneInfo();

            // BugReportDataを作成
            var report = BugReportData.Create(_targetModel.name, 0);
            report.ScreenshotBytes = _lastScreenshotBytes;
            report.user_comment = ""; // ユーザーが入力

            // モデル情報を収集
            report.CollectModelInfo(_targetModel);

            // ボーン不具合カテゴリとして追加情報を設定
            report.AddWarning("BoneCheck", "ボーン不具合報告", boneInfo);

            // スクリーンショット情報を設定
            if (_lastScreenshotBytes != null)
            {
                report.screenshot.width = _screenshotPreview?.width ?? 0;
                report.screenshot.height = _screenshotPreview?.height ?? 0;
            }

            // BugReportServiceにキャッシュ
            BugReportService.CacheReport(report);

            // BugReportWindowを開く
            BugReportWindow.Show(report);

            Debug.Log($"[BoneCheck] Opened BugReportWindow with bone info for: {_targetModel.name}");
        }

        /// <summary>
        /// ボーン情報を文字列として収集
        /// </summary>
        private string CollectBoneInfo()
        {
            // キャッシュされたボーン情報を使用（Playモード後など）
            var useCachedInfo = (_boneMap == null || _boneMap.Count == 0) &&
                                (_cachedBoneNames != null && _cachedBoneNames.Count > 0);

            if (!useCachedInfo && (_boneMap == null || _boneMap.Count == 0))
            {
                RefreshBoneMap();
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Bone Check Report ===");
            sb.AppendLine();

            if (useCachedInfo)
            {
                sb.AppendLine($"[Model: {_cachedModelName ?? "Unknown"} (cached)]");
                sb.AppendLine();
            }

            // 必須ボーン
            var requiredBones = new[]
            {
                HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot
            };

            sb.AppendLine("[Required Bones]");
            int requiredFound = 0;
            foreach (var bone in requiredBones)
            {
                var found = GetBoneFound(bone, useCachedInfo);
                if (found) requiredFound++;
                var status = found ? "OK" : "MISSING";
                var boneName = GetBoneName(bone, useCachedInfo);
                sb.AppendLine($"  {bone}: {status} ({boneName})");
            }
            sb.AppendLine($"  Total: {requiredFound}/{requiredBones.Length}");
            sb.AppendLine();

            // 推奨ボーン
            var recommendedBones = new[]
            {
                HumanBodyBones.Neck, HumanBodyBones.UpperChest,
                HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
                HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
                HumanBodyBones.LeftEye, HumanBodyBones.RightEye, HumanBodyBones.Jaw
            };

            sb.AppendLine("[Recommended Bones]");
            int recommendedFound = 0;
            foreach (var bone in recommendedBones)
            {
                var found = GetBoneFound(bone, useCachedInfo);
                if (found) recommendedFound++;
                var status = found ? "OK" : "MISSING";
                var boneName = GetBoneName(bone, useCachedInfo);
                sb.AppendLine($"  {bone}: {status} ({boneName})");
            }
            sb.AppendLine($"  Total: {recommendedFound}/{recommendedBones.Length}");
            sb.AppendLine();

            // Animator情報
            if (_animator != null)
            {
                sb.AppendLine("[Animator Info]");
                sb.AppendLine($"  IsHuman: {_animator.isHuman}");
                sb.AppendLine($"  Avatar: {_animator.avatar?.name ?? "None"}");
                sb.AppendLine($"  Controller: {_selectedController?.name ?? "None"}");
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

        private void DrawBoneStatus()
        {
            _showBoneList = EditorGUILayout.Foldout(_showBoneList,
                Localize.Get("ボーン状態", "Bone Status"), true);

            if (!_showBoneList) return;

            // キャッシュされたボーン情報を使用するかどうか
            var useCachedInfo = (_boneMap == null || _boneMap.Count == 0) && HasCachedBoneInfo();

            if (!useCachedInfo && (_boneMap == null || _boneMap.Count == 0))
            {
                EditorGUILayout.HelpBox(
                    Localize.Get(
                        "ボーン情報を取得できません。モデルをロードしてください。",
                        "Could not retrieve bone information. Please load a model."),
                    MessageType.Warning);
                return;
            }

            // キャッシュ使用時のメッセージ
            if (useCachedInfo)
            {
                EditorGUILayout.HelpBox(
                    Localize.Get(
                        $"キャッシュされたボーン情報を表示中: {_cachedModelName}",
                        $"Showing cached bone info: {_cachedModelName}"),
                    MessageType.Info);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 必須ボーン
                EditorGUILayout.LabelField(
                    Localize.Get("必須ボーン", "Required Bones"),
                    EditorStyles.boldLabel);

                var requiredBones = new[]
                {
                    HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                    HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                    HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                    HumanBodyBones.RightFoot
                };

                DrawBoneList(requiredBones, true);

                EditorGUILayout.Space(10);

                // 推奨ボーン
                EditorGUILayout.LabelField(
                    Localize.Get("推奨ボーン", "Recommended Bones"),
                    EditorStyles.boldLabel);

                var recommendedBones = new[]
                {
                    HumanBodyBones.Neck, HumanBodyBones.UpperChest,
                    HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
                    HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
                    HumanBodyBones.LeftEye, HumanBodyBones.RightEye, HumanBodyBones.Jaw
                };

                DrawBoneList(recommendedBones, false);
            }
        }

        private void DrawBoneList(HumanBodyBones[] bones, bool required)
        {
            var foundCount = 0;
            var useCachedInfo = (_boneMap == null || _boneMap.Count == 0) && HasCachedBoneInfo();

            foreach (var bone in bones)
            {
                var found = GetBoneFound(bone, useCachedInfo);
                if (found) foundCount++;

                using (new EditorGUILayout.HorizontalScope())
                {
                    // ステータスアイコン
                    var icon = found ? "TestPassed" : (required ? "TestFailed" : "TestNormal");
                    var iconContent = EditorGUIUtility.IconContent(icon);
                    GUILayout.Label(iconContent, GUILayout.Width(20), GUILayout.Height(18));

                    // ボーン名
                    EditorGUILayout.LabelField(bone.ToString(), GUILayout.Width(140));

                    // Transform参照またはボーン名
                    if (found)
                    {
                        if (useCachedInfo)
                        {
                            // キャッシュからボーン名を表示
                            EditorGUILayout.LabelField(GetBoneName(bone, true), EditorStyles.miniLabel);
                        }
                        else
                        {
                            using (new EditorGUI.DisabledScope(true))
                            {
                                EditorGUILayout.ObjectField(_boneMap[bone], typeof(Transform), true);
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("-", EditorStyles.miniLabel);
                    }
                }
            }

            // サマリー
            var summaryStyle = new GUIStyle(EditorStyles.miniLabel);
            if (required && foundCount < bones.Length)
            {
                summaryStyle.normal.textColor = Color.red;
            }

            EditorGUILayout.LabelField(
                $"{foundCount}/{bones.Length} " + Localize.Get("検出", "found"),
                summaryStyle);
        }

        private void RefreshBoneMap()
        {
            if (_targetModel == null)
            {
                _boneMap = null;
                _animator = null;
                return;
            }

            _animator = _targetModel.GetComponent<Animator>();
            if (_animator == null || !_animator.isHuman)
            {
                _boneMap = null;
                return;
            }

            _boneMap = new Dictionary<HumanBodyBones, Transform>();

            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = (HumanBodyBones)i;
                var transform = _animator.GetBoneTransform(bone);
                if (transform != null)
                {
                    _boneMap[bone] = transform;
                }
            }

            // ボーン情報をキャッシュに保存（Playモード対策）
            CacheBoneInfo();
        }

        /// <summary>
        /// ボーンが見つかっているかどうかを取得（キャッシュ対応）
        /// </summary>
        private bool GetBoneFound(HumanBodyBones bone, bool useCachedInfo)
        {
            if (useCachedInfo)
            {
                return _cachedBoneNames != null && _cachedBoneNames.ContainsKey(bone);
            }
            return _boneMap != null && _boneMap.ContainsKey(bone) && _boneMap[bone] != null;
        }

        /// <summary>
        /// ボーン名を取得（キャッシュ対応）
        /// </summary>
        private string GetBoneName(HumanBodyBones bone, bool useCachedInfo)
        {
            if (useCachedInfo)
            {
                return _cachedBoneNames != null && _cachedBoneNames.ContainsKey(bone)
                    ? _cachedBoneNames[bone]
                    : "-";
            }
            return _boneMap != null && _boneMap.ContainsKey(bone) && _boneMap[bone] != null
                ? _boneMap[bone].name
                : "-";
        }

        /// <summary>
        /// キャッシュされたボーン情報があるかどうか
        /// </summary>
        private bool HasCachedBoneInfo()
        {
            return _cachedBoneNames != null && _cachedBoneNames.Count > 0;
        }

        private void ApplyAnimatorController()
        {
            if (_targetModel == null || _selectedController == null) return;

            _animator = _targetModel.GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = _targetModel.AddComponent<Animator>();
            }

            _animator.runtimeAnimatorController = _selectedController;
            _animator.speed = _animationSpeed;

            // アニメーションを強制的に更新
            _animator.Update(0);

            Debug.Log($"[BoneCheck] Applied AnimatorController: {_selectedController.name} to {_targetModel.name}");

            SceneView.RepaintAll();
        }

        private void ResetAnimator()
        {
            if (_animator != null)
            {
                _animator.runtimeAnimatorController = null;
                _animator.Rebind();
                _animator.Update(0);
            }

            SceneView.RepaintAll();
        }
    }
}
