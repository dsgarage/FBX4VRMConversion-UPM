using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using DSGarage.FBX4VRM.Editor.Settings;

namespace DSGarage.FBX4VRM.Editor.Reports
{
    /// <summary>
    /// サーバーから取得したアバター情報
    /// </summary>
    [Serializable]
    public class AvatarInfo
    {
        public string id;
        public string name;              // 内部名（FBX/VRMファイル名に対応）
        public string display_name;      // 表示名（日本語名など）
        public string package_version;   // アバターパッケージのバージョン
        public string booth_url;         // BOOTH販売ページURL
        public int avatar_number;        // アバター番号
        public string issue_number;      // GitHub Issue番号
        public string github_issue_url;  // GitHub IssueのURL
        public int report_count;
        public string last_reported;
        public List<string> platforms;

        // result_successはJSONでnull/true/falseを取りうる
        // JsonUtilityはnullable boolをサポートしないため、パース後に手動設定
        [NonSerialized]
        public bool? ResultSuccess;

        /// <summary>
        /// 表示用の名前（display_nameがあればそれを使用、なければname）
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(display_name) ? display_name : name;
    }

    /// <summary>
    /// アバター名一覧（ユニークな名前のみ）
    /// </summary>
    [Serializable]
    public class AvatarNameInfo
    {
        public string name;
        public string display_name;
        public List<string> versions;  // このアバターで登録されているバージョン一覧

        public string DisplayName => !string.IsNullOrEmpty(display_name) ? display_name : name;
    }

    /// <summary>
    /// アバター名一覧APIのレスポンス
    /// </summary>
    [Serializable]
    public class AvatarNamesResponse
    {
        public bool success;
        public List<AvatarNameInfo> avatar_names;
        public string error;
    }

    /// <summary>
    /// アバターリストAPIのレスポンス
    /// </summary>
    [Serializable]
    public class AvatarListResponse
    {
        public bool success;
        public List<AvatarInfo> avatars;
        public string error;
    }

    /// <summary>
    /// アバターバージョン情報
    /// </summary>
    [Serializable]
    public class AvatarVersionInfo
    {
        public int id;
        public string package_version;
        public int version_index;
        public string child_issue_number;
        public string child_issue_url;
        // result_successはnullable bool - JsonUtilityでは直接サポートされない
        [NonSerialized]
        public bool? ResultSuccess;
    }

    /// <summary>
    /// アバター作成APIのレスポンス
    /// </summary>
    [Serializable]
    public class CreateAvatarResponse
    {
        public bool success;
        public string message;
        public AvatarInfo avatar;
    }

    /// <summary>
    /// バージョン作成APIのレスポンス
    /// </summary>
    [Serializable]
    public class CreateVersionResponse
    {
        public bool success;
        public string message;
        public AvatarVersionInfo version;
    }

    /// <summary>
    /// アバター作成リクエスト
    /// </summary>
    [Serializable]
    public class CreateAvatarRequest
    {
        public string name;
        public string display_name;
        public string booth_url;
        public bool create_parent_issue;
    }

    /// <summary>
    /// バージョン作成リクエスト
    /// </summary>
    [Serializable]
    public class CreateVersionRequest
    {
        public string package_version;
        public bool create_child_issue;
        // result_successはnullable - 別途処理
    }
    /// <summary>
    /// 不具合報告サービス
    /// メモリ上のキャッシュ管理とサーバーへの送信
    /// </summary>
    public static class BugReportService
    {
        // デフォルトサーバーURL - HTTPS ポート8443 + キューシステム
        // URL: https://153.126.176.139:8443/bug-reports/queue/submit
        // 更新日: 2024-12-03 - Unity 2022+でHTTPがブロックされるためHTTPSに変更
        // キューシステム: JSONが保存された時点で成功レスポンスを返す（高信頼性）
        private static readonly string DefaultServerUrlEncoded = "aHR0cHM6Ly8xNTMuMTI2LjE3Ni4xMzk6ODQ0My9idWctcmVwb3J0cy9xdWV1ZS9zdWJtaXQ=";

        /// <summary>
        /// 報告先サーバーURL（設定可能）
        /// </summary>
        public static string ServerUrl
        {
            get
            {
                var customUrl = EditorPrefs.GetString("FBX4VRM_BugReportServerUrl", "");
                if (!string.IsNullOrEmpty(customUrl))
                    return customUrl;

                // デフォルトURLをデコード
                try
                {
                    return Encoding.UTF8.GetString(Convert.FromBase64String(DefaultServerUrlEncoded));
                }
                catch
                {
                    return "";
                }
            }
            set => EditorPrefs.SetString("FBX4VRM_BugReportServerUrl", value);
        }

        /// <summary>
        /// サーバーURL設定をリセット（デフォルトに戻す）
        /// </summary>
        public static void ResetServerUrl()
        {
            EditorPrefs.DeleteKey("FBX4VRM_BugReportServerUrl");
            ClearAvatarListCache();
            Debug.Log($"[BugReportService] Server URL reset to default: {ServerUrl}");
        }

        /// <summary>
        /// APIキー（設定可能）
        /// </summary>
        public static string ApiKey
        {
            get => EditorPrefs.GetString("FBX4VRM_BugReportApiKey", "");
            set => EditorPrefs.SetString("FBX4VRM_BugReportApiKey", value);
        }

        /// <summary>
        /// 現在キャッシュされている報告データ
        /// </summary>
        public static BugReportData CachedReport { get; private set; }

        /// <summary>
        /// キャッシュされたアバターリスト
        /// </summary>
        public static List<AvatarInfo> CachedAvatarList { get; private set; }

        /// <summary>
        /// アバターリストの最終取得時刻
        /// </summary>
        public static DateTime AvatarListLastFetched { get; private set; }

        /// <summary>
        /// アバターリストのキャッシュ有効時間（分）
        /// </summary>
        private const int AvatarListCacheMinutes = 5;

        /// <summary>
        /// 報告データをキャッシュ
        /// </summary>
        public static void CacheReport(BugReportData report)
        {
            CachedReport = report;
            Debug.Log($"[BugReportService] Report cached: {report.ReportId}");
        }

        /// <summary>
        /// キャッシュをクリア
        /// </summary>
        public static void ClearCache()
        {
            CachedReport = null;
            Debug.Log("[BugReportService] Cache cleared");
        }

        /// <summary>
        /// エクスポート結果から報告データを作成してキャッシュ
        /// </summary>
        public static BugReportData CreateFromExportResult(
            GameObject exportedModel,
            ExportReport report,
            ConversionSettings settings = null,
            bool captureScreenshot = true)
        {
            Debug.Log($"[BugReportService] CreateFromExportResult called - model: {exportedModel?.name}, captureScreenshot: {captureScreenshot}");

            // モデル名を取得
            var modelName = exportedModel?.name ?? "Unknown";
            if (report != null && !string.IsNullOrEmpty(report.SourceAssetPath))
            {
                modelName = System.IO.Path.GetFileNameWithoutExtension(report.SourceAssetPath);
            }

            var bugReport = BugReportData.Create(modelName, report?.VrmVersion ?? 0);

            // 結果情報を設定
            bugReport.result.success = report?.Success ?? false;
            bugReport.result.stopped_at_processor = report?.StoppedAtProcessor;
            bugReport.export_settings.output_path = report?.OutputPath;
            bugReport.export_settings.preset_name = report?.PresetName;

            // ConversionSettings を適用
            if (settings != null)
            {
                bugReport.ApplySettings(settings);
            }

            // モデル情報を収集
            if (exportedModel != null)
            {
                bugReport.CollectModelInfo(exportedModel);
            }

            // 通知を追加
            if (report?.Notifications != null)
            {
                foreach (var notif in report.Notifications)
                {
                    if (notif.Level == "Error")
                    {
                        bugReport.AddError(notif.ProcessorId, notif.Message, notif.Details);
                    }
                    else if (notif.Level == "Warning")
                    {
                        bugReport.AddWarning(notif.ProcessorId, notif.Message, notif.Details);
                    }
                    else
                    {
                        bugReport.notifications.summary.info++;
                    }
                }
            }

            // スクリーンショット撮影
            if (captureScreenshot && exportedModel != null)
            {
                try
                {
                    bugReport.ScreenshotBytes = MultiAngleCapture.CaptureMultiAngle(exportedModel);
                    if (bugReport.ScreenshotBytes != null)
                    {
                        // スクリーンショット情報を設定
                        var tex = new Texture2D(2, 2);
                        tex.LoadImage(bugReport.ScreenshotBytes);
                        bugReport.screenshot.width = tex.width;
                        bugReport.screenshot.height = tex.height;
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                    Debug.Log($"[BugReportService] Screenshot captured: {bugReport.ScreenshotBytes?.Length ?? 0} bytes");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BugReportService] Failed to capture screenshot: {ex.Message}");
                }
            }

            CacheReport(bugReport);
            Debug.Log($"[BugReportService] Report created and cached: {bugReport.ReportId}, screenshot: {bugReport.ScreenshotBytes?.Length ?? 0} bytes");
            return bugReport;
        }

        /// <summary>
        /// GameObjectから直接報告データを作成（Quick Export用）
        /// </summary>
        public static BugReportData CreateFromModel(
            GameObject model,
            int vrmVersion,
            bool success,
            ConversionSettings settings = null,
            bool captureScreenshot = true)
        {
            var bugReport = BugReportData.Create(model?.name ?? "Unknown", vrmVersion);

            bugReport.result.success = success;

            // ConversionSettings を適用
            if (settings != null)
            {
                bugReport.ApplySettings(settings);
            }

            // モデル情報を収集
            if (model != null)
            {
                bugReport.CollectModelInfo(model);
            }

            // スクリーンショット撮影
            if (captureScreenshot && model != null)
            {
                try
                {
                    bugReport.ScreenshotBytes = MultiAngleCapture.CaptureMultiAngle(model);
                    if (bugReport.ScreenshotBytes != null)
                    {
                        var tex = new Texture2D(2, 2);
                        tex.LoadImage(bugReport.ScreenshotBytes);
                        bugReport.screenshot.width = tex.width;
                        bugReport.screenshot.height = tex.height;
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                    Debug.Log($"[BugReportService] Screenshot captured: {bugReport.ScreenshotBytes?.Length ?? 0} bytes");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BugReportService] Failed to capture screenshot: {ex.Message}");
                }
            }

            CacheReport(bugReport);
            return bugReport;
        }

        /// <summary>
        /// サーバーに報告を送信
        /// </summary>
        public static void SendReport(BugReportData report, Action<bool, string> onComplete = null)
        {
            var serverUrl = ServerUrl;
            if (string.IsNullOrEmpty(serverUrl))
            {
                var error = "Server URL is not configured.";
                Debug.LogError($"[BugReportService] {error}");
                onComplete?.Invoke(false, error);
                return;
            }

            EditorCoroutineRunner.StartCoroutine(SendReportCoroutine(report, serverUrl, onComplete));
        }

        /// <summary>
        /// キャッシュされた報告を送信
        /// </summary>
        public static void SendCachedReport(Action<bool, string> onComplete = null)
        {
            if (CachedReport == null)
            {
                var error = "No cached report available";
                Debug.LogError($"[BugReportService] {error}");
                onComplete?.Invoke(false, error);
                return;
            }

            SendReport(CachedReport, onComplete);
        }

        /// <summary>
        /// 報告送信コルーチン
        /// </summary>
        private static IEnumerator SendReportCoroutine(BugReportData report, string serverUrl, Action<bool, string> onComplete)
        {
            var json = report.ToJson();
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest(serverUrl, "POST"))
            {
                // 自己署名証明書を許可（エディタ専用）
#if UNITY_EDITOR
                request.certificateHandler = new AcceptAllCertificatesHandler();
#endif
                request.uploadHandler = new UploadHandlerRaw(jsonBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(ApiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {ApiKey}");
                }

                request.SetRequestHeader("X-Report-Id", report.ReportId);
                request.SetRequestHeader("X-Client-Version", report.PackageVersion);

                Debug.Log($"[BugReportService] Sending report {report.ReportId}...");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var responseText = request.downloadHandler.text;
                    Debug.Log($"[BugReportService] Report queued successfully: {report.ReportId}");
                    Debug.Log($"[BugReportService] Server response: {responseText}");

                    // キューシステムのレスポンス: {"success": true, "queue_id": "...", "status": "queued"}
                    // JSONが保存された時点で成功とみなす
                    onComplete?.Invoke(true, responseText);
                }
                else
                {
                    var error = $"Failed to send report: {request.error}";
                    Debug.LogError($"[BugReportService] {error}");

                    // エラーレスポンスのボディを出力（デバッグ用）
                    var responseBody = request.downloadHandler?.text ?? "(no response body)";
                    Debug.LogError($"[BugReportService] Response code: {request.responseCode}");
                    Debug.LogError($"[BugReportService] Response body: {responseBody}");

                    onComplete?.Invoke(false, $"{error}\nServer response: {responseBody}");
                }
            }
        }

        /// <summary>
        /// スクリーンショットをローカルに保存（デバッグ用）
        /// </summary>
        public static string SaveScreenshotLocally(BugReportData report, string folder = null)
        {
            if (report?.ScreenshotBytes == null || report.ScreenshotBytes.Length == 0)
            {
                Debug.LogWarning("[BugReportService] No screenshot to save");
                return null;
            }

            folder ??= System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath),
                "FBX4VRM_Reports");

            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }

            var filename = $"bugreport_{report.ReportId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var path = System.IO.Path.Combine(folder, filename);

            System.IO.File.WriteAllBytes(path, report.ScreenshotBytes);
            Debug.Log($"[BugReportService] Screenshot saved: {path}");

            return path;
        }

        /// <summary>
        /// デフォルトサーバーURLを使用しているか確認
        /// </summary>
        public static bool IsUsingDefaultServer()
        {
            var customUrl = EditorPrefs.GetString("FBX4VRM_BugReportServerUrl", "");
            return string.IsNullOrEmpty(customUrl);
        }

        /// <summary>
        /// カスタムサーバーURLをクリアしてデフォルトに戻す
        /// </summary>
        public static void ResetToDefaultServer()
        {
            EditorPrefs.DeleteKey("FBX4VRM_BugReportServerUrl");
            Debug.Log("[BugReportService] Reset to default server");
        }

        #region Avatar List API

        /// <summary>
        /// サーバーからアバターリストを取得
        /// </summary>
        /// <param name="forceRefresh">キャッシュを無視して再取得</param>
        /// <param name="onComplete">完了コールバック (success, avatarList, errorMessage)</param>
        public static void FetchAvatarList(bool forceRefresh, Action<bool, List<AvatarInfo>, string> onComplete)
        {
            // キャッシュが有効ならそれを返す
            if (!forceRefresh && CachedAvatarList != null &&
                (DateTime.Now - AvatarListLastFetched).TotalMinutes < AvatarListCacheMinutes)
            {
                Debug.Log($"[BugReportService] Using cached avatar list ({CachedAvatarList.Count} avatars)");
                onComplete?.Invoke(true, CachedAvatarList, null);
                return;
            }

            var serverUrl = ServerUrl;
            if (string.IsNullOrEmpty(serverUrl))
            {
                var error = "Server URL is not configured.";
                Debug.LogError($"[BugReportService] {error}");
                onComplete?.Invoke(false, null, error);
                return;
            }

            // サーバーURLからアバターリストAPIエンドポイントを構築
            Debug.Log($"[BugReportService] Server URL: {serverUrl}");
            var uri = new Uri(serverUrl);
            var avatarListUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}/api/v1/fbx4vrm/avatars";
            Debug.Log($"[BugReportService] Avatar list URL: {avatarListUrl}");

            EditorCoroutineRunner.StartCoroutine(FetchAvatarListCoroutine(avatarListUrl, onComplete));
        }

        /// <summary>
        /// アバターリスト取得コルーチン
        /// </summary>
        private static IEnumerator FetchAvatarListCoroutine(string url, Action<bool, List<AvatarInfo>, string> onComplete)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                // 自己署名証明書を許可（エディタ専用）
#if UNITY_EDITOR
                request.certificateHandler = new AcceptAllCertificatesHandler();
#endif
                request.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(ApiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {ApiKey}");
                }

                Debug.Log($"[BugReportService] Fetching avatar list from {url}...");

                yield return request.SendWebRequest();

                // デバッグ: レスポンス情報をログ出力
                Debug.Log($"[BugReportService] Response code: {request.responseCode}");
                Debug.Log($"[BugReportService] Result: {request.result}");
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[BugReportService] Error: {request.error}");
                }
                else
                {
                    Debug.Log($"[BugReportService] Response text: {request.downloadHandler.text}");
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var responseText = request.downloadHandler.text;
                        var response = JsonUtility.FromJson<AvatarListResponse>(responseText);
                        Debug.Log($"[BugReportService] Parsed response - success: {response?.success}, avatars count: {response?.avatars?.Count ?? -1}");

                        if (response != null && response.success)
                        {
                            CachedAvatarList = response.avatars ?? new List<AvatarInfo>();

                            // result_successを手動でパース（JsonUtilityはnullable boolをサポートしない）
                            ParseResultSuccess(responseText, CachedAvatarList);

                            AvatarListLastFetched = DateTime.Now;
                            Debug.Log($"[BugReportService] Avatar list fetched: {CachedAvatarList.Count} avatars");
                            onComplete?.Invoke(true, CachedAvatarList, null);
                        }
                        else
                        {
                            Debug.LogWarning($"[BugReportService] Avatar list fetch failed: {response.error}");
                            onComplete?.Invoke(false, null, response.error);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[BugReportService] Failed to parse avatar list: {ex.Message}");
                        onComplete?.Invoke(false, null, ex.Message);
                    }
                }
                else
                {
                    // サーバー接続エラーはWarningにする（サーバーが起動していない場合など）
                    var error = request.error ?? "Unknown error";
                    Debug.LogWarning($"[BugReportService] Failed to fetch avatar list: {error}");
                    onComplete?.Invoke(false, null, error);
                }
            }
        }

        /// <summary>
        /// result_successフィールドを手動でパース
        /// JsonUtilityはnullable boolをサポートしないため
        /// </summary>
        private static void ParseResultSuccess(string json, List<AvatarInfo> avatars)
        {
            if (avatars == null || avatars.Count == 0) return;

            try
            {
                // 簡易的な正規表現でresult_successを抽出
                // "result_success": null / true / false
                var pattern = new System.Text.RegularExpressions.Regex(
                    @"""id""\s*:\s*""([^""]+)""[^}]*""result_success""\s*:\s*(null|true|false)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                var matches = pattern.Matches(json);
                var resultMap = new Dictionary<string, bool?>();

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        var id = match.Groups[1].Value;
                        var resultStr = match.Groups[2].Value.ToLower();
                        bool? result = resultStr == "null" ? null : (bool?)(resultStr == "true");
                        resultMap[id] = result;
                    }
                }

                // アバターリストに反映
                foreach (var avatar in avatars)
                {
                    if (resultMap.TryGetValue(avatar.id, out var result))
                    {
                        avatar.ResultSuccess = result;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BugReportService] Failed to parse result_success: {ex.Message}");
            }
        }

        /// <summary>
        /// アバターリストキャッシュをクリア
        /// </summary>
        public static void ClearAvatarListCache()
        {
            CachedAvatarList = null;
            AvatarListLastFetched = DateTime.MinValue;
            Debug.Log("[BugReportService] Avatar list cache cleared");
        }

        /// <summary>
        /// 新規アバターを作成
        /// </summary>
        /// <param name="name">アバター名（必須）</param>
        /// <param name="displayName">表示名（オプション）</param>
        /// <param name="boothUrl">Booth URL（オプション）</param>
        /// <param name="createParentIssue">親GitHub Issueを作成するか</param>
        /// <param name="onComplete">完了コールバック (success, avatar, errorMessage)</param>
        public static void CreateAvatar(
            string name,
            string displayName = null,
            string boothUrl = null,
            bool createParentIssue = true,
            Action<bool, AvatarInfo, string> onComplete = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                onComplete?.Invoke(false, null, "Avatar name is required");
                return;
            }

            var serverUrl = ServerUrl;
            if (string.IsNullOrEmpty(serverUrl))
            {
                onComplete?.Invoke(false, null, "Server URL is not configured");
                return;
            }

            var uri = new Uri(serverUrl);
            var apiUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}/api/v1/fbx4vrm/avatars/db";

            var request = new CreateAvatarRequest
            {
                name = name,
                display_name = displayName,
                booth_url = boothUrl,
                create_parent_issue = createParentIssue
            };

            EditorCoroutineRunner.StartCoroutine(CreateAvatarCoroutine(apiUrl, request, onComplete));
        }

        /// <summary>
        /// アバター作成コルーチン
        /// </summary>
        private static IEnumerator CreateAvatarCoroutine(
            string url,
            CreateAvatarRequest request,
            Action<bool, AvatarInfo, string> onComplete)
        {
            var json = JsonUtility.ToJson(request);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            using (var webRequest = new UnityWebRequest(url, "POST"))
            {
#if UNITY_EDITOR
                webRequest.certificateHandler = new AcceptAllCertificatesHandler();
#endif
                webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");

                Debug.Log($"[BugReportService] Creating avatar: {request.name}");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var responseText = webRequest.downloadHandler.text;
                        var response = JsonUtility.FromJson<CreateAvatarResponse>(responseText);

                        if (response != null && response.success)
                        {
                            Debug.Log($"[BugReportService] Avatar created: {response.avatar?.name}");
                            // キャッシュをクリアして次回再取得させる
                            ClearAvatarListCache();
                            onComplete?.Invoke(true, response.avatar, null);
                        }
                        else
                        {
                            onComplete?.Invoke(false, null, response?.message ?? "Unknown error");
                        }
                    }
                    catch (Exception ex)
                    {
                        onComplete?.Invoke(false, null, ex.Message);
                    }
                }
                else
                {
                    var error = webRequest.error ?? "Unknown error";
                    var responseBody = webRequest.downloadHandler?.text ?? "";
                    Debug.LogWarning($"[BugReportService] Failed to create avatar: {error}");
                    onComplete?.Invoke(false, null, $"{error}\n{responseBody}");
                }
            }
        }

        /// <summary>
        /// 新規バージョンを作成
        /// </summary>
        /// <param name="avatarName">アバター名（必須）</param>
        /// <param name="packageVersion">パッケージバージョン（必須）</param>
        /// <param name="resultSuccess">変換成功フラグ（オプション）</param>
        /// <param name="createChildIssue">子GitHub Issueを作成するか</param>
        /// <param name="onComplete">完了コールバック (success, version, errorMessage)</param>
        public static void CreateVersion(
            string avatarName,
            string packageVersion,
            bool? resultSuccess = null,
            bool createChildIssue = true,
            Action<bool, AvatarVersionInfo, string> onComplete = null)
        {
            if (string.IsNullOrEmpty(avatarName))
            {
                onComplete?.Invoke(false, null, "Avatar name is required");
                return;
            }

            if (string.IsNullOrEmpty(packageVersion))
            {
                onComplete?.Invoke(false, null, "Package version is required");
                return;
            }

            var serverUrl = ServerUrl;
            if (string.IsNullOrEmpty(serverUrl))
            {
                onComplete?.Invoke(false, null, "Server URL is not configured");
                return;
            }

            var uri = new Uri(serverUrl);
            var encodedAvatarName = Uri.EscapeDataString(avatarName);
            var apiUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}/api/v1/fbx4vrm/avatars/db/{encodedAvatarName}/versions";

            EditorCoroutineRunner.StartCoroutine(
                CreateVersionCoroutine(apiUrl, packageVersion, resultSuccess, createChildIssue, onComplete));
        }

        /// <summary>
        /// バージョン作成コルーチン
        /// </summary>
        private static IEnumerator CreateVersionCoroutine(
            string url,
            string packageVersion,
            bool? resultSuccess,
            bool createChildIssue,
            Action<bool, AvatarVersionInfo, string> onComplete)
        {
            // JSONを手動で構築（result_successがnullableのため）
            var jsonBuilder = new StringBuilder();
            jsonBuilder.Append("{");
            jsonBuilder.Append($"\"package_version\":\"{packageVersion}\"");
            jsonBuilder.Append($",\"create_child_issue\":{(createChildIssue ? "true" : "false")}");
            if (resultSuccess.HasValue)
            {
                jsonBuilder.Append($",\"result_success\":{(resultSuccess.Value ? "true" : "false")}");
            }
            jsonBuilder.Append("}");

            var json = jsonBuilder.ToString();
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            using (var webRequest = new UnityWebRequest(url, "POST"))
            {
#if UNITY_EDITOR
                webRequest.certificateHandler = new AcceptAllCertificatesHandler();
#endif
                webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");

                Debug.Log($"[BugReportService] Creating version: {packageVersion}");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var responseText = webRequest.downloadHandler.text;
                        var response = JsonUtility.FromJson<CreateVersionResponse>(responseText);

                        if (response != null && response.success)
                        {
                            Debug.Log($"[BugReportService] Version created: {response.version?.package_version}");
                            // キャッシュをクリアして次回再取得させる
                            ClearAvatarListCache();
                            onComplete?.Invoke(true, response.version, null);
                        }
                        else
                        {
                            onComplete?.Invoke(false, null, response?.message ?? "Unknown error");
                        }
                    }
                    catch (Exception ex)
                    {
                        onComplete?.Invoke(false, null, ex.Message);
                    }
                }
                else
                {
                    var error = webRequest.error ?? "Unknown error";
                    var responseBody = webRequest.downloadHandler?.text ?? "";
                    Debug.LogWarning($"[BugReportService] Failed to create version: {error}");
                    onComplete?.Invoke(false, null, $"{error}\n{responseBody}");
                }
            }
        }

        /// <summary>
        /// ユニークなアバター名の一覧を取得
        /// </summary>
        public static List<AvatarNameInfo> GetUniqueAvatarNames()
        {
            if (CachedAvatarList == null || CachedAvatarList.Count == 0)
                return new List<AvatarNameInfo>();

            var nameDict = new Dictionary<string, AvatarNameInfo>();

            foreach (var avatar in CachedAvatarList)
            {
                if (string.IsNullOrEmpty(avatar.name)) continue;

                if (!nameDict.TryGetValue(avatar.name, out var info))
                {
                    info = new AvatarNameInfo
                    {
                        name = avatar.name,
                        display_name = avatar.display_name,
                        versions = new List<string>()
                    };
                    nameDict[avatar.name] = info;
                }

                // バージョンがあれば追加
                if (!string.IsNullOrEmpty(avatar.package_version) &&
                    !info.versions.Contains(avatar.package_version))
                {
                    info.versions.Add(avatar.package_version);
                }
            }

            // 名前でソートして返す
            var result = new List<AvatarNameInfo>(nameDict.Values);
            result.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        /// <summary>
        /// 指定アバター名のバージョン一覧を取得
        /// </summary>
        public static List<string> GetAvatarVersions(string avatarName)
        {
            if (CachedAvatarList == null || string.IsNullOrEmpty(avatarName))
                return new List<string>();

            var versions = new HashSet<string>();
            foreach (var avatar in CachedAvatarList)
            {
                if (string.Equals(avatar.name, avatarName, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(avatar.package_version))
                {
                    versions.Add(avatar.package_version);
                }
            }

            var result = new List<string>(versions);
            result.Sort();
            return result;
        }

        /// <summary>
        /// アバター名とバージョンで既存のアバターを検索
        /// </summary>
        public static AvatarInfo FindAvatarByNameAndVersion(string name, string version)
        {
            if (CachedAvatarList == null || string.IsNullOrEmpty(name))
                return null;

            foreach (var avatar in CachedAvatarList)
            {
                if (string.Equals(avatar.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    // バージョン指定がない場合は名前のみでマッチ
                    if (string.IsNullOrEmpty(version))
                    {
                        if (string.IsNullOrEmpty(avatar.package_version))
                            return avatar;
                    }
                    else if (string.Equals(avatar.package_version, version, StringComparison.OrdinalIgnoreCase))
                    {
                        return avatar;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// アバター名で既存のアバターを検索
        /// </summary>
        public static AvatarInfo FindAvatarByName(string name)
        {
            if (CachedAvatarList == null || string.IsNullOrEmpty(name))
                return null;

            // 完全一致
            foreach (var avatar in CachedAvatarList)
            {
                if (string.Equals(avatar.name, name, StringComparison.OrdinalIgnoreCase))
                    return avatar;
            }

            // 部分一致（アバター名がモデル名を含む、またはその逆）
            foreach (var avatar in CachedAvatarList)
            {
                if (avatar.name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(avatar.name, StringComparison.OrdinalIgnoreCase))
                    return avatar;
            }

            return null;
        }

        /// <summary>
        /// プラットフォームのラベル名を取得
        /// </summary>
        public static string GetPlatformLabel(ReportPlatform platform)
        {
            return platform switch
            {
                ReportPlatform.FBX4VRM => "fbx4vrm",
                ReportPlatform.VRMLoader => "vrmloader",
                ReportPlatform.ARApp => "arapp",
                _ => "unknown"
            };
        }

        /// <summary>
        /// 報告にプラットフォームを設定
        /// </summary>
        public static void SetReportPlatform(BugReportData report, ReportPlatform platform)
        {
            if (report == null) return;
            report.platform = GetPlatformLabel(platform);
        }

        /// <summary>
        /// 報告にアバターIDを設定
        /// </summary>
        public static void SetReportAvatar(BugReportData report, AvatarInfo avatar)
        {
            if (report == null) return;

            if (avatar != null)
            {
                report.source_model.avatar_id = avatar.id;
                report.source_model.is_new_avatar = false;
            }
            else
            {
                report.source_model.avatar_id = null;
                report.source_model.is_new_avatar = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Editor用コルーチンランナー
    /// </summary>
    public static class EditorCoroutineRunner
    {
        public static void StartCoroutine(IEnumerator coroutine)
        {
            EditorApplication.CallbackFunction callback = null;
            object currentYield = null;

            callback = () =>
            {
                try
                {
                    // AsyncOperationの場合は完了を待つ
                    if (currentYield is AsyncOperation asyncOp)
                    {
                        if (!asyncOp.isDone)
                        {
                            return; // まだ完了していない
                        }
                    }

                    // 次のステップに進む
                    if (coroutine.MoveNext())
                    {
                        currentYield = coroutine.Current;
                    }
                    else
                    {
                        EditorApplication.update -= callback;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EditorCoroutineRunner] Exception: {ex.Message}");
                    EditorApplication.update -= callback;
                }
            };
            EditorApplication.update += callback;
        }
    }
}
