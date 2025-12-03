using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DSGarage.FBX4VRM.Runtime
{
    /// <summary>
    /// Playモードで VRM をロードし、A-ポーズまたはAnimatorControllerでスクリーンショットを撮影
    /// </summary>
    public class BoneCheckRunner : MonoBehaviour
    {
        // PlayerPrefsキー（Playモード遷移時にも保持される）
        private const string PREFS_VRM_PATH = "FBX4VRM_BoneCheck_VrmPath";
        private const string PREFS_SCREENSHOT_PATH = "FBX4VRM_BoneCheck_ScreenshotPath";
        private const string PREFS_ENABLED = "FBX4VRM_BoneCheck_Enabled";
        private const string PREFS_USE_APOSE = "FBX4VRM_BoneCheck_UseAPose";
        private const string PREFS_ANIMATOR_PATH = "FBX4VRM_BoneCheck_AnimatorPath";
        private const string PREFS_ANIMATION_WAIT = "FBX4VRM_BoneCheck_AnimationWait";

        // 状態
        private GameObject _loadedVrm;
        private Animator _animator;
        private bool _isProcessing;
        private float _waitTime = 0.5f;

        // A-Pose用のボーン回転データ
        private Dictionary<HumanBodyBones, Quaternion> _originalRotations;

        /// <summary>
        /// ボーンチェック実行設定（PlayerPrefsに保存）- A-ポーズモード
        /// </summary>
        public static void Setup(string vrmPath, string screenshotSavePath, bool useAPose = true)
        {
            PlayerPrefs.SetString(PREFS_VRM_PATH, vrmPath ?? "");
            PlayerPrefs.SetString(PREFS_SCREENSHOT_PATH, screenshotSavePath ?? "");
            PlayerPrefs.SetInt(PREFS_USE_APOSE, useAPose ? 1 : 0);
            PlayerPrefs.SetString(PREFS_ANIMATOR_PATH, ""); // AnimatorControllerなし
            PlayerPrefs.SetFloat(PREFS_ANIMATION_WAIT, 0.5f);
            PlayerPrefs.SetInt(PREFS_ENABLED, 1);
            PlayerPrefs.Save();

            Debug.Log($"[BoneCheckRunner] Setup saved - VRM: {vrmPath}, UseAPose: {useAPose}");
        }

        /// <summary>
        /// ボーンチェック実行設定（PlayerPrefsに保存）- AnimatorControllerモード
        /// </summary>
        /// <param name="vrmPath">VRMファイルのパス</param>
        /// <param name="screenshotSavePath">スクリーンショット保存パス</param>
        /// <param name="animatorControllerPath">Resources内のAnimatorControllerパス（拡張子なし）</param>
        /// <param name="animationWaitTime">アニメーション待機時間（秒）</param>
        public static void SetupWithAnimator(string vrmPath, string screenshotSavePath, string animatorControllerPath, float animationWaitTime = 1.0f)
        {
            PlayerPrefs.SetString(PREFS_VRM_PATH, vrmPath ?? "");
            PlayerPrefs.SetString(PREFS_SCREENSHOT_PATH, screenshotSavePath ?? "");
            PlayerPrefs.SetInt(PREFS_USE_APOSE, 0); // A-ポーズを使わない
            PlayerPrefs.SetString(PREFS_ANIMATOR_PATH, animatorControllerPath ?? "");
            PlayerPrefs.SetFloat(PREFS_ANIMATION_WAIT, animationWaitTime);
            PlayerPrefs.SetInt(PREFS_ENABLED, 1);
            PlayerPrefs.Save();

            Debug.Log($"[BoneCheckRunner] Setup saved - VRM: {vrmPath}, AnimatorController: {animatorControllerPath}, WaitTime: {animationWaitTime}s");
        }

        /// <summary>
        /// 設定をクリア
        /// </summary>
        public static void ClearSetup()
        {
            PlayerPrefs.DeleteKey(PREFS_VRM_PATH);
            PlayerPrefs.DeleteKey(PREFS_SCREENSHOT_PATH);
            PlayerPrefs.DeleteKey(PREFS_USE_APOSE);
            PlayerPrefs.DeleteKey(PREFS_ANIMATOR_PATH);
            PlayerPrefs.DeleteKey(PREFS_ANIMATION_WAIT);
            PlayerPrefs.SetInt(PREFS_ENABLED, 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 設定済みかどうか
        /// </summary>
        public static bool IsSetup => PlayerPrefs.GetInt(PREFS_ENABLED, 0) == 1;

        /// <summary>
        /// 設定を取得
        /// </summary>
        public static (string vrmPath, string screenshotPath, bool useAPose, string animatorPath, float animationWait) GetSetup()
        {
            return (
                PlayerPrefs.GetString(PREFS_VRM_PATH, ""),
                PlayerPrefs.GetString(PREFS_SCREENSHOT_PATH, ""),
                PlayerPrefs.GetInt(PREFS_USE_APOSE, 1) == 1,
                PlayerPrefs.GetString(PREFS_ANIMATOR_PATH, ""),
                PlayerPrefs.GetFloat(PREFS_ANIMATION_WAIT, 0.5f)
            );
        }

        private void Awake()
        {
            Debug.Log($"[BoneCheckRunner] Awake - IsSetup: {IsSetup}");
        }

        private void Start()
        {
            Debug.Log($"[BoneCheckRunner] Start - IsSetup: {IsSetup}");

            if (!IsSetup)
            {
                Debug.Log("[BoneCheckRunner] No setup. Skipping.");
                return;
            }

            StartCoroutine(RunBoneCheck());
        }

        private IEnumerator RunBoneCheck()
        {
            _isProcessing = true;

            var (vrmPath, screenshotPath, useAPose, animatorPath, animationWait) = GetSetup();

            Debug.Log($"[BoneCheckRunner] Starting - VRM: {vrmPath}");
            Debug.Log($"[BoneCheckRunner] Screenshot path: {screenshotPath}");
            Debug.Log($"[BoneCheckRunner] Use A-Pose: {useAPose}");
            Debug.Log($"[BoneCheckRunner] Animator path: {animatorPath}");
            Debug.Log($"[BoneCheckRunner] Animation wait: {animationWait}s");

            // VRMファイルの存在確認
            if (!File.Exists(vrmPath))
            {
                ReportError($"VRM file not found: {vrmPath}");
                yield break;
            }

            // VRMをロード
            yield return StartCoroutine(LoadVrm(vrmPath));

            if (_loadedVrm == null)
            {
                ReportError("Failed to load VRM");
                yield break;
            }

            // AnimatorControllerが指定されている場合
            if (!string.IsNullOrEmpty(animatorPath))
            {
                yield return StartCoroutine(ApplyAnimatorController(animatorPath, animationWait));
            }
            // A-ポーズを適用
            else if (useAPose)
            {
                ApplyAPose();
            }

            // 数フレーム待機してポーズを安定させる
            yield return null;
            yield return null;
            yield return null;

            // カメラをセットアップ
            SetupCamera();

            // さらに待機（AnimatorControllerの場合はより長く待つ）
            var waitTime = !string.IsNullOrEmpty(animatorPath) ? animationWait : _waitTime;
            yield return new WaitForSeconds(waitTime);

            // スクリーンショット撮影
            yield return StartCoroutine(CaptureGameViewScreenshot(screenshotPath));

            _isProcessing = false;
            Debug.Log("[BoneCheckRunner] Completed");

            // 設定をクリア
            ClearSetup();

            // 完了フラグ
            PlayerPrefs.SetInt("FBX4VRM_BoneCheck_Completed", 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// AnimatorControllerを適用してアニメーションを再生
        /// </summary>
        private IEnumerator ApplyAnimatorController(string resourcePath, float waitTime)
        {
            if (_animator == null)
            {
                Debug.LogWarning("[BoneCheckRunner] No Animator component found");
                yield break;
            }

            // Resourcesから AnimatorController をロード
            var controller = Resources.Load<RuntimeAnimatorController>(resourcePath);
            if (controller == null)
            {
                Debug.LogError($"[BoneCheckRunner] AnimatorController not found in Resources: {resourcePath}");
                Debug.Log("[BoneCheckRunner] Falling back to A-Pose");
                ApplyAPose();
                yield break;
            }

            Debug.Log($"[BoneCheckRunner] Applying AnimatorController: {controller.name}");

            // AnimatorControllerを設定
            _animator.runtimeAnimatorController = controller;

            // アニメーションの最初のフレームを待つ
            yield return null;

            // アニメーションを再生開始
            _animator.Play(0, 0, 0f); // 最初のステートを再生

            Debug.Log($"[BoneCheckRunner] Animation started, waiting {waitTime}s...");
        }

        private IEnumerator LoadVrm(string path)
        {
            Debug.Log($"[BoneCheckRunner] Loading VRM: {path}");

            // UniVRM の VrmUtility を使用してロード
            var task = VRM.VrmUtility.LoadAsync(path);

            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Debug.LogError($"[BoneCheckRunner] VRM load failed: {task.Exception}");
                _loadedVrm = null;
                yield break;
            }

            var instance = task.Result;
            _loadedVrm = instance.Root;
            _loadedVrm.name = Path.GetFileNameWithoutExtension(path);

            // 位置・回転をリセット
            _loadedVrm.transform.position = Vector3.zero;
            _loadedVrm.transform.rotation = Quaternion.identity;

            // メッシュを表示
            instance.ShowMeshes();

            // Animatorを取得
            _animator = _loadedVrm.GetComponent<Animator>();

            Debug.Log($"[BoneCheckRunner] VRM loaded: {_loadedVrm.name}");
        }

        /// <summary>
        /// A-ポーズを適用（両腕を水平に広げた姿勢）
        /// </summary>
        private void ApplyAPose()
        {
            if (_animator == null || !_animator.isHuman)
            {
                Debug.LogWarning("[BoneCheckRunner] Cannot apply A-Pose: No humanoid animator");
                return;
            }

            Debug.Log("[BoneCheckRunner] Applying A-Pose...");

            // AnimatorControllerを無効化してポーズを直接制御
            _animator.runtimeAnimatorController = null;

            // 元の回転を保存
            _originalRotations = new Dictionary<HumanBodyBones, Quaternion>();

            // A-ポーズ: 腕を水平に広げる
            // 左腕
            SetBoneRotation(HumanBodyBones.LeftUpperArm, Quaternion.Euler(0, 0, 75));  // 腕を下げる（T-Poseから）
            SetBoneRotation(HumanBodyBones.LeftLowerArm, Quaternion.identity);
            SetBoneRotation(HumanBodyBones.LeftHand, Quaternion.identity);

            // 右腕
            SetBoneRotation(HumanBodyBones.RightUpperArm, Quaternion.Euler(0, 0, -75)); // 腕を下げる（T-Poseから）
            SetBoneRotation(HumanBodyBones.RightLowerArm, Quaternion.identity);
            SetBoneRotation(HumanBodyBones.RightHand, Quaternion.identity);

            // 脚はまっすぐ
            SetBoneRotation(HumanBodyBones.LeftUpperLeg, Quaternion.identity);
            SetBoneRotation(HumanBodyBones.LeftLowerLeg, Quaternion.identity);
            SetBoneRotation(HumanBodyBones.RightUpperLeg, Quaternion.identity);
            SetBoneRotation(HumanBodyBones.RightLowerLeg, Quaternion.identity);

            // 体幹
            SetBoneRotation(HumanBodyBones.Spine, Quaternion.identity);
            SetBoneRotation(HumanBodyBones.Chest, Quaternion.identity);
            SetBoneRotation(HumanBodyBones.Head, Quaternion.identity);

            Debug.Log("[BoneCheckRunner] A-Pose applied");
        }

        private void SetBoneRotation(HumanBodyBones bone, Quaternion localRotation)
        {
            var boneTransform = _animator.GetBoneTransform(bone);
            if (boneTransform != null)
            {
                if (!_originalRotations.ContainsKey(bone))
                {
                    _originalRotations[bone] = boneTransform.localRotation;
                }
                boneTransform.localRotation = localRotation;
            }
        }

        private void SetupCamera()
        {
            // メインカメラを取得または作成
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraGo = new GameObject("PreviewCamera");
                camera = cameraGo.AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            // 背景色を設定
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

            if (_loadedVrm != null)
            {
                // モデルの全身が映るようにカメラを配置
                var bounds = CalculateBounds(_loadedVrm);
                var center = bounds.center;
                var size = bounds.size;

                // 正面から撮影
                var distance = Mathf.Max(size.y * 1.2f, size.x * 1.5f);
                camera.transform.position = new Vector3(0, center.y, distance);
                camera.transform.LookAt(center);

                // FOVを調整
                camera.fieldOfView = 35f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;

                Debug.Log($"[BoneCheckRunner] Camera: pos={camera.transform.position}, target={center}, distance={distance}");
            }
        }

        private IEnumerator CaptureGameViewScreenshot(string savePath)
        {
            // フレーム終了まで待機
            yield return new WaitForEndOfFrame();

            // Game Viewのスクリーンショットを撮影
            var width = Screen.width;
            var height = Screen.height;

            // より高解像度で撮影
            if (width < 1920)
            {
                width = 1920;
                height = 1080;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                ReportError("No camera for screenshot");
                yield break;
            }

            // RenderTextureを使用して撮影
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prevTarget = camera.targetTexture;

            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();

            camera.targetTexture = prevTarget;
            RenderTexture.active = null;

            var bytes = screenshot.EncodeToPNG();

            Destroy(rt);
            Destroy(screenshot);

            Debug.Log($"[BoneCheckRunner] Screenshot: {width}x{height}, {bytes.Length} bytes");

            // ファイルに保存
            if (!string.IsNullOrEmpty(savePath))
            {
                var directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(savePath, bytes);
                Debug.Log($"[BoneCheckRunner] Saved to: {savePath}");

                PlayerPrefs.SetString("FBX4VRM_BoneCheck_ResultPath", savePath);
                PlayerPrefs.Save();
            }
        }

        private Bounds CalculateBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(obj.transform.position, Vector3.one * 2f);
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private void ReportError(string message)
        {
            Debug.LogError($"[BoneCheckRunner] Error: {message}");
            PlayerPrefs.SetString("FBX4VRM_BoneCheck_Error", message);
            PlayerPrefs.SetInt("FBX4VRM_BoneCheck_Completed", 1);
            PlayerPrefs.Save();
            _isProcessing = false;
            ClearSetup();
        }

        private void OnDestroy()
        {
            if (_loadedVrm != null)
            {
                Destroy(_loadedVrm);
            }
        }
    }
}
