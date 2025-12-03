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
    /// 不具合報告ウィンドウ
    /// スクリーンショットプレビューと送信機能
    /// </summary>
    public class BugReportWindow : EditorWindow
    {
        [MenuItem("Tools/FBX4VRM/Bug Report", false, 50)]
        public static void ShowWindow()
        {
            var window = GetWindow<BugReportWindow>();
            window.titleContent = new GUIContent("Bug Report");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private BugReportData _report;
        private Texture2D _screenshotPreview;
        private Vector2 _scrollPosition;
        private string _userComment = "";
        private bool _isSending;
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.None;

        // アバター選択
        private bool _isFetchingAvatars;
        private List<AvatarInfo> _avatarList;
        private List<AvatarNameInfo> _uniqueAvatarNames;  // ユニークなアバター名一覧
        private int _selectedAvatarNameIndex;  // 0 = 新規アバター, 1~ = 既存アバター名
        private int _selectedVersionIndex;     // 0 = 新規バージョン, 1~ = 既存バージョン
        private string _customAvatarName = "";  // 新規アバター名（手動入力）
        private string _customVersion = "";     // 新規バージョン（手動入力）
        private AvatarInfo _matchedAvatar;  // モデル名から自動マッチしたアバター
        private bool _showAvatarSelection = true;

        // 通知の折りたたみ
        private bool _showNotifications = false;

        // スクリーンショット追加
        private bool _showScreenshotSection = true;
        private bool _userSelectedScreenshot = false; // ユーザーが手動で選択したかどうか

        // プラットフォーム選択（FBX4VRMでは固定だが、将来の拡張用）
        private ReportPlatform _selectedPlatform = ReportPlatform.FBX4VRM;

        public static void Show(BugReportData report)
        {
            var window = GetWindow<BugReportWindow>();
            window.titleContent = new GUIContent("Bug Report");
            window.minSize = new Vector2(500, 600);
            window._report = report;
            // レポートにスクリーンショットがあれば表示フラグを立てる
            if (report?.ScreenshotBytes != null && report.ScreenshotBytes.Length > 0)
            {
                window._userSelectedScreenshot = true;
            }
            window.LoadScreenshotPreview();
            window.Show();
        }

        private void OnEnable()
        {
            if (_report == null)
            {
                _report = BugReportService.CachedReport;
                Debug.Log($"[BugReportWindow] OnEnable - CachedReport: {(_report != null ? _report.ReportId : "null")}");
                // キャッシュからロードした場合もスクリーンショットがあれば表示フラグを立てる
                if (_report?.ScreenshotBytes != null && _report.ScreenshotBytes.Length > 0)
                {
                    _userSelectedScreenshot = true;
                }
                LoadScreenshotPreview();
            }

            // プラットフォームを設定（FBX4VRM固定）
            if (_report != null)
            {
                BugReportService.SetReportPlatform(_report, _selectedPlatform);
            }

            // サーバーURL設定をリセット（古い設定が残っている場合に対応）
            BugReportService.ResetServerUrl();

            // アバターリストを取得
            FetchAvatarList(true);  // 強制リフレッシュ
        }

        private void FetchAvatarList(bool forceRefresh)
        {
            _isFetchingAvatars = true;
            BugReportService.FetchAvatarList(forceRefresh, (success, avatars, error) =>
            {
                _isFetchingAvatars = false;
                if (success)
                {
                    _avatarList = avatars;
                    _uniqueAvatarNames = BugReportService.GetUniqueAvatarNames();

                    // モデル名からアバターを自動マッチ
                    if (_report != null && !string.IsNullOrEmpty(_report.ModelName))
                    {
                        _matchedAvatar = BugReportService.FindAvatarByName(_report.ModelName);
                        if (_matchedAvatar != null)
                        {
                            // アバター名のインデックスを検索
                            for (int i = 0; i < _uniqueAvatarNames.Count; i++)
                            {
                                if (string.Equals(_uniqueAvatarNames[i].name, _matchedAvatar.name, StringComparison.OrdinalIgnoreCase))
                                {
                                    _selectedAvatarNameIndex = i + 1;  // +1 は「新規」オプションの分
                                    // バージョンも設定
                                    var versions = BugReportService.GetAvatarVersions(_matchedAvatar.name);
                                    if (!string.IsNullOrEmpty(_matchedAvatar.package_version))
                                    {
                                        var versionIdx = versions.IndexOf(_matchedAvatar.package_version);
                                        if (versionIdx >= 0)
                                        {
                                            _selectedVersionIndex = versionIdx + 1;
                                        }
                                    }
                                    break;
                                }
                            }
                            Debug.Log($"[BugReportWindow] Auto-matched avatar: {_matchedAvatar.name}");
                        }
                        else
                        {
                            // マッチしなかった場合は新規アバターとしてモデル名を設定
                            _selectedAvatarNameIndex = 0;
                            _customAvatarName = _report.ModelName;
                        }
                    }
                    // アバターリスト取得後、選択状態をレポートに反映
                    UpdateSelectedAvatar();
                }
                else
                {
                    Debug.LogWarning($"[BugReportWindow] Failed to fetch avatar list: {error}");
                }
                Repaint();
            });
        }

        private void OnDisable()
        {
            if (_screenshotPreview != null)
            {
                DestroyImmediate(_screenshotPreview);
            }
        }

        private void LoadScreenshotPreview()
        {
            if (_screenshotPreview != null)
            {
                DestroyImmediate(_screenshotPreview);
            }

            if (_report?.ScreenshotBytes != null && _report.ScreenshotBytes.Length > 0)
            {
                _screenshotPreview = new Texture2D(2, 2);
                _screenshotPreview.LoadImage(_report.ScreenshotBytes);
            }
        }

        private void OnGUI()
        {
            if (_report == null)
            {
                EditorGUILayout.HelpBox(
                    Localize.Get(
                        "報告データがありません。VRMエクスポート後に「不具合を報告」ボタンから開いてください。",
                        "No report data available. Please open from 'Report Issue' button after VRM export."),
                    MessageType.Warning);

                EditorGUILayout.Space(10);

                if (GUILayout.Button(Localize.Get("ウィンドウを閉じる", "Close Window"), GUILayout.Height(30)))
                {
                    Close();
                }
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawReportInfo();
            EditorGUILayout.Space(10);

            DrawAvatarSelection();
            EditorGUILayout.Space(10);

            DrawScreenshotSection();
            EditorGUILayout.Space(10);

            DrawNotifications();
            EditorGUILayout.Space(10);

            DrawUserComment();
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
                Localize.Get("不具合報告", "Bug Report"),
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                Localize.Get(
                    "エクスポート結果とスクリーンショットを送信して不具合を報告できます。",
                    "You can report issues by sending export results and screenshots."),
                MessageType.Info);
        }

        private void DrawReportInfo()
        {
            EditorGUILayout.LabelField(
                Localize.Get("報告情報", "Report Info"),
                EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("Report ID", _report.ReportId);
                EditorGUILayout.LabelField("Model", _report.ModelName);
                EditorGUILayout.LabelField("VRM Version", _report.VrmVersion == 1 ? "1.0" : "0.x");
                EditorGUILayout.LabelField("Export Result", _report.ExportSuccess ? "Success" : "Failed");
                EditorGUILayout.LabelField("Package Version", _report.PackageVersion);
                EditorGUILayout.LabelField("Unity Version", _report.UnityVersion);
                EditorGUILayout.LabelField("Timestamp", _report.Timestamp);
            }
        }

        private void DrawAvatarSelection()
        {
            _showAvatarSelection = EditorGUILayout.Foldout(_showAvatarSelection,
                Localize.Get("アバター選択", "Avatar Selection"), true);

            if (!_showAvatarSelection) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 読み込み中
                if (_isFetchingAvatars)
                {
                    EditorGUILayout.LabelField(
                        Localize.Get("アバターリストを取得中...", "Fetching avatar list..."));
                    return;
                }

                // === アバター名選択 ===
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        Localize.Get("アバター名", "Avatar Name"),
                        GUILayout.Width(100));

                    // 選択肢を構築
                    var nameOptions = new List<string>
                    {
                        Localize.Get("-- 新規登録 --", "-- New Avatar --")
                    };

                    if (_uniqueAvatarNames != null)
                    {
                        foreach (var avatarName in _uniqueAvatarNames)
                        {
                            var displayText = !string.IsNullOrEmpty(avatarName.display_name)
                                ? $"{avatarName.display_name} ({avatarName.name})"
                                : avatarName.name;
                            nameOptions.Add(displayText);
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    _selectedAvatarNameIndex = EditorGUILayout.Popup(_selectedAvatarNameIndex, nameOptions.ToArray());

                    if (EditorGUI.EndChangeCheck())
                    {
                        // アバター名が変更されたらバージョン選択をリセット
                        _selectedVersionIndex = 0;
                        _customVersion = "";
                        UpdateSelectedAvatar();
                    }

                    // 更新ボタン
                    if (GUILayout.Button(
                        EditorGUIUtility.IconContent("Refresh"),
                        GUILayout.Width(30), GUILayout.Height(18)))
                    {
                        BugReportService.ResetServerUrl();
                        FetchAvatarList(true);
                    }
                }

                // 新規アバターの場合 - アバター名を手動入力
                if (_selectedAvatarNameIndex == 0)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("", GUILayout.Width(100));
                        _customAvatarName = EditorGUILayout.TextField(_customAvatarName);
                    }
                }

                EditorGUILayout.Space(5);

                // === バージョン選択 ===
                string selectedAvatarName = GetSelectedAvatarName();
                var versions = !string.IsNullOrEmpty(selectedAvatarName)
                    ? BugReportService.GetAvatarVersions(selectedAvatarName)
                    : new List<string>();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        Localize.Get("バージョン", "Version"),
                        GUILayout.Width(100));

                    // 選択肢を構築
                    var versionOptions = new List<string>
                    {
                        Localize.Get("-- 手動入力 --", "-- Manual Input --")
                    };

                    // 既存アバターを選択している場合、「バージョンなし」を追加
                    if (_selectedAvatarNameIndex > 0)
                    {
                        versionOptions.Add(Localize.Get("(バージョンなし)", "(No Version)"));
                    }

                    versionOptions.AddRange(versions);

                    EditorGUI.BeginChangeCheck();
                    _selectedVersionIndex = EditorGUILayout.Popup(_selectedVersionIndex, versionOptions.ToArray());

                    if (EditorGUI.EndChangeCheck())
                    {
                        UpdateSelectedAvatar();
                    }
                }

                // 手動入力の場合 - バージョンを手動入力
                if (_selectedVersionIndex == 0)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("", GUILayout.Width(100));
                        _customVersion = EditorGUILayout.TextField(_customVersion);
                    }
                }

                EditorGUILayout.Space(5);

                // === 選択中のアバター情報表示 ===
                var selectedAvatar = GetCurrentSelectedAvatar();
                if (selectedAvatar != null)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(
                            Localize.Get("選択中のアバター", "Selected Avatar"),
                            EditorStyles.boldLabel);

                        using (new EditorGUI.IndentLevelScope())
                        {
                            if (!string.IsNullOrEmpty(selectedAvatar.display_name))
                            {
                                EditorGUILayout.LabelField(
                                    Localize.Get("表示名", "Display Name"),
                                    selectedAvatar.display_name);
                            }
                            EditorGUILayout.LabelField(
                                Localize.Get("内部名", "Internal Name"),
                                selectedAvatar.name);
                            if (!string.IsNullOrEmpty(selectedAvatar.issue_number))
                            {
                                EditorGUILayout.LabelField(
                                    Localize.Get("Issue番号", "Issue Number"),
                                    $"#{selectedAvatar.issue_number}");
                            }
                            if (!string.IsNullOrEmpty(selectedAvatar.package_version))
                            {
                                EditorGUILayout.LabelField(
                                    Localize.Get("登録バージョン", "Registered Version"),
                                    selectedAvatar.package_version);
                            }
                            if (selectedAvatar.ResultSuccess.HasValue)
                            {
                                EditorGUILayout.LabelField(
                                    Localize.Get("テスト結果", "Test Result"),
                                    selectedAvatar.ResultSuccess.Value
                                        ? Localize.Get("成功", "Success")
                                        : Localize.Get("失敗", "Failed"));
                            }
                        }
                    }
                }
                else if (_selectedAvatarNameIndex == 0 && !string.IsNullOrEmpty(_customAvatarName))
                {
                    EditorGUILayout.HelpBox(
                        Localize.Get(
                            "新しいアバターとして登録されます",
                            "Will be registered as a new avatar"),
                        MessageType.Info);
                }

                // 自動マッチの通知
                if (_matchedAvatar != null && _selectedAvatarNameIndex > 0)
                {
                    EditorGUILayout.HelpBox(
                        Localize.Get(
                            $"モデル名から自動選択されました",
                            $"Auto-selected based on model name"),
                        MessageType.None);
                }

                // FBX4VRMパッケージバージョン表示
                EditorGUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        Localize.Get("FBX4VRM Ver", "FBX4VRM Ver"),
                        GUILayout.Width(100));
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField(_report?.PackageVersion ?? "");
                    }
                }
            }
        }

        /// <summary>
        /// 選択中のアバター名を取得
        /// </summary>
        private string GetSelectedAvatarName()
        {
            if (_selectedAvatarNameIndex == 0)
            {
                return _customAvatarName;
            }
            else if (_uniqueAvatarNames != null && _selectedAvatarNameIndex <= _uniqueAvatarNames.Count)
            {
                return _uniqueAvatarNames[_selectedAvatarNameIndex - 1].name;
            }
            return "";
        }

        /// <summary>
        /// 選択中のバージョンを取得
        /// </summary>
        private string GetSelectedVersion()
        {
            // 0 = 手動入力
            if (_selectedVersionIndex == 0)
            {
                return _customVersion;
            }
            // 既存アバター選択時: 1 = バージョンなし, 2~ = 既存バージョン
            else if (_selectedAvatarNameIndex > 0)
            {
                if (_selectedVersionIndex == 1)
                {
                    return ""; // バージョンなし
                }
                else
                {
                    var avatarName = GetSelectedAvatarName();
                    var versions = BugReportService.GetAvatarVersions(avatarName);
                    var versionIdx = _selectedVersionIndex - 2; // 手動入力とバージョンなしの分を引く
                    if (versionIdx >= 0 && versionIdx < versions.Count)
                    {
                        return versions[versionIdx];
                    }
                }
            }
            // 新規アバター選択時: 1~ = 既存バージョン（バージョンなしオプションなし）
            else
            {
                var avatarName = GetSelectedAvatarName();
                var versions = BugReportService.GetAvatarVersions(avatarName);
                var versionIdx = _selectedVersionIndex - 1;
                if (versionIdx >= 0 && versionIdx < versions.Count)
                {
                    return versions[versionIdx];
                }
            }
            return "";
        }

        /// <summary>
        /// 現在選択中のAvatarInfoを取得
        /// </summary>
        private AvatarInfo GetCurrentSelectedAvatar()
        {
            var avatarName = GetSelectedAvatarName();
            var version = GetSelectedVersion();

            if (string.IsNullOrEmpty(avatarName))
                return null;

            return BugReportService.FindAvatarByNameAndVersion(avatarName, version);
        }

        /// <summary>
        /// 選択が変更された時にレポートに反映
        /// </summary>
        private void UpdateSelectedAvatar()
        {
            var selectedAvatar = GetCurrentSelectedAvatar();
            BugReportService.SetReportAvatar(_report, selectedAvatar);

            // 新規アバターの場合はモデル名を更新
            if (_selectedAvatarNameIndex == 0 && !string.IsNullOrEmpty(_customAvatarName))
            {
                _report.source_model.name = _customAvatarName;
            }

            // アバターのバージョンを設定（FBX4VRMツールのバージョンではなく、アバター自体のバージョン）
            var selectedVersion = GetSelectedVersion();
            _report.source_model.package_version = selectedVersion;
            Debug.Log($"[BugReportWindow] Updated avatar version: {selectedVersion}");
        }

        private void DrawScreenshotSection()
        {
            _showScreenshotSection = EditorGUILayout.Foldout(_showScreenshotSection,
                Localize.Get("スクリーンショット", "Screenshot"), true);

            if (!_showScreenshotSection) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // ユーザーが手動で選択した画像のみ表示（自動撮影されたものは表示しない）
                if (_screenshotPreview != null && _userSelectedScreenshot)
                {
                    var maxWidth = position.width - 50;
                    var aspect = (float)_screenshotPreview.height / _screenshotPreview.width;
                    var height = Mathf.Min(maxWidth * aspect, 300);

                    var rect = GUILayoutUtility.GetRect(maxWidth, height);
                    GUI.DrawTexture(rect, _screenshotPreview, ScaleMode.ScaleToFit);

                    EditorGUILayout.LabelField(
                        $"{_screenshotPreview.width} x {_screenshotPreview.height} ({_report.ScreenshotBytes?.Length / 1024 ?? 0} KB)",
                        EditorStyles.centeredGreyMiniLabel);
                }

                EditorGUILayout.Space(5);

                // スクリーンショット追加・変更ボタン
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                        Localize.Get("画像を選択...", "Select Image..."),
                        GUILayout.Height(25)))
                    {
                        SelectScreenshotFromFile();
                    }

                    if (_screenshotPreview != null && _userSelectedScreenshot)
                    {
                        if (GUILayout.Button(
                            Localize.Get("クリア", "Clear"),
                            GUILayout.Width(60),
                            GUILayout.Height(25)))
                        {
                            ClearScreenshot();
                        }
                    }
                }

                EditorGUILayout.HelpBox(
                    Localize.Get(
                        "PNG/JPG画像を選択して送信できます。問題のあるモデルのスクリーンショットを添付してください。",
                        "Select a PNG/JPG image to send. Please attach a screenshot of the problematic model."),
                    MessageType.Info);
            }
        }

        private void SelectScreenshotFromFile()
        {
            var path = EditorUtility.OpenFilePanel(
                Localize.Get("スクリーンショットを選択", "Select Screenshot"),
                "",
                "png,jpg,jpeg");

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes != null && bytes.Length > 0)
                {
                    _report.ScreenshotBytes = bytes;
                    _userSelectedScreenshot = true; // ユーザーが手動で選択した
                    LoadScreenshotPreview();
                    Debug.Log($"[BugReportWindow] Screenshot loaded: {path} ({bytes.Length} bytes)");
                }
            }
        }

        private void ClearScreenshot()
        {
            _report.ScreenshotBytes = null;
            _userSelectedScreenshot = false; // フラグをリセット
            if (_screenshotPreview != null)
            {
                DestroyImmediate(_screenshotPreview);
                _screenshotPreview = null;
            }
        }

        private void DrawNotifications()
        {
            if (_report.Notifications == null || _report.Notifications.Count == 0)
            {
                return;
            }

            var notificationCount = _report.Notifications.Count;
            var errorCount = 0;
            var warningCount = 0;
            foreach (var n in _report.Notifications)
            {
                if (n.Level == "Error") errorCount++;
                else if (n.Level == "Warning") warningCount++;
            }

            var summary = $" ({notificationCount}";
            if (errorCount > 0) summary += $", {errorCount} errors";
            if (warningCount > 0) summary += $", {warningCount} warnings";
            summary += ")";

            _showNotifications = EditorGUILayout.Foldout(_showNotifications,
                Localize.Get("通知", "Notifications") + summary, true);

            if (!_showNotifications) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (var notif in _report.Notifications)
                {
                    var icon = notif.Level switch
                    {
                        "Error" => MessageType.Error,
                        "Warning" => MessageType.Warning,
                        _ => MessageType.Info
                    };
                    EditorGUILayout.HelpBox($"[{notif.ProcessorId}] {notif.Message}", icon);
                }
            }
        }

        private void DrawUserComment()
        {
            EditorGUILayout.LabelField(
                Localize.Get("コメント", "Comment"),
                EditorStyles.boldLabel);

            _userComment = EditorGUILayout.TextArea(_userComment, GUILayout.Height(60));
            _report.UserComment = _userComment;
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // ローカル保存
                if (GUILayout.Button(
                    Localize.Get("ローカルに保存", "Save Locally"),
                    GUILayout.Height(30)))
                {
                    SaveLocally();
                }

                // サーバー送信
                using (new EditorGUI.DisabledGroupScope(_isSending))
                {
                    var buttonStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontStyle = FontStyle.Bold
                    };

                    if (GUILayout.Button(
                        _isSending
                            ? Localize.Get("送信中...", "Sending...")
                            : Localize.Get("送信", "Send"),
                        buttonStyle,
                        GUILayout.Height(30)))
                    {
                        SendToServer();
                    }
                }
            }
        }

        private void SaveLocally()
        {
            var path = BugReportService.SaveScreenshotLocally(_report);
            if (!string.IsNullOrEmpty(path))
            {
                // JSONも保存
                var jsonPath = path.Replace(".png", ".json");
                File.WriteAllText(jsonPath, _report.ToJson());

                _statusMessage = Localize.Get(
                    $"保存しました: {Path.GetDirectoryName(path)}",
                    $"Saved to: {Path.GetDirectoryName(path)}");
                _statusType = MessageType.Info;

                EditorUtility.RevealInFinder(path);
            }
            else
            {
                _statusMessage = Localize.Get("保存に失敗しました", "Failed to save");
                _statusType = MessageType.Error;
            }
        }

        private void SendToServer()
        {
            _isSending = true;
            _statusMessage = Localize.Get("送信中...", "Sending...");
            _statusType = MessageType.Info;

            // 送信前に最新の選択状態をレポートに反映
            UpdateSelectedAvatar();
            Debug.Log($"[BugReportWindow] Sending report - source_model.package_version: {_report.source_model.package_version}");

            BugReportService.SendReport(_report, (success, message) =>
            {
                _isSending = false;
                if (success)
                {
                    _statusMessage = Localize.Get(
                        "送信完了！ありがとうございます。",
                        "Sent successfully! Thank you.");
                    _statusType = MessageType.Info;
                }
                else
                {
                    _statusMessage = Localize.Get(
                        $"送信失敗: {message}",
                        $"Failed to send: {message}");
                    _statusType = MessageType.Error;
                }
                Repaint();
            });
        }
    }
}
