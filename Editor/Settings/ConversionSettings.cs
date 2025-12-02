using System;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Settings
{
    /// <summary>
    /// FBX→VRM変換設定
    /// Export Window で設定し、バグレポートにも含める
    /// </summary>
    [Serializable]
    public class ConversionSettings
    {
        // ========== 基本設定 ==========
        [Header("Basic Settings")]
        [Tooltip("VRMバージョン: 0 = VRM 0.x, 1 = VRM 1.0")]
        public int vrmVersion = 0;

        [Tooltip("プリセット名")]
        public string presetName = "VRChat";

        // ========== マテリアル変換 ==========
        [Header("Material Conversion")]
        [Tooltip("lilToon → MToon 変換を有効にする")]
        public bool enableLilToonConversion = true;

        [Tooltip("HDR値を glTF仕様 (0-1) にクランプする")]
        public bool enableHdrClamp = true;

        [Tooltip("Outline変換を有効にする")]
        public bool enableOutlineConversion = true;

        [Tooltip("透過処理モード")]
        public TransparentMode transparentMode = TransparentMode.Auto;

        // ========== 骨格・ポーズ ==========
        [Header("Skeleton / Pose")]
        [Tooltip("T-Pose正規化を有効にする")]
        public bool enableTPoseNormalization = true;

        [Tooltip("Armature回転をベイクする")]
        public bool enableArmatureRotationBake = true;

        [Tooltip("ボーン向き正規化を有効にする (VRM 1.0)")]
        public bool enableBoneOrientationNormalization = true;

        // ========== 表情 (Expression) ==========
        [Header("Expression")]
        [Tooltip("BlendShape自動マッピングを有効にする")]
        public bool enableExpressionAutoMapping = true;

        [Tooltip("BlendShape命名規則")]
        public NamingConvention expressionNamingConvention = NamingConvention.Auto;

        // ========== 物理 (SpringBone) ==========
        [Header("SpringBone / Physics")]
        [Tooltip("PhysBone/DynamicBone → VRM SpringBone 変換を有効にする")]
        public bool enableSpringBoneConversion = true;

        [Tooltip("Collider変換を有効にする")]
        public bool enableColliderConversion = true;

        // ========== 出力 ==========
        [Header("Output")]
        [Tooltip("出力フォルダパス")]
        public string outputFolder = "VRM_Export";

        [Tooltip("ファイル名モード")]
        public FileNameMode fileNameMode = FileNameMode.Auto;

        [Tooltip("カスタムファイル名 (FileNameMode.Custom時に使用)")]
        public string customFileName = "";

        /// <summary>
        /// デフォルト設定を作成
        /// </summary>
        public static ConversionSettings Default => new ConversionSettings();

        /// <summary>
        /// VRChat向け設定
        /// </summary>
        public static ConversionSettings ForVRChat => new ConversionSettings
        {
            vrmVersion = 0,
            presetName = "VRChat",
            enableLilToonConversion = true,
            enableSpringBoneConversion = true
        };

        /// <summary>
        /// Cluster向け設定
        /// </summary>
        public static ConversionSettings ForCluster => new ConversionSettings
        {
            vrmVersion = 1,
            presetName = "Cluster",
            enableLilToonConversion = true,
            enableSpringBoneConversion = true
        };

        /// <summary>
        /// 設定をコピー
        /// </summary>
        public ConversionSettings Clone()
        {
            return new ConversionSettings
            {
                vrmVersion = vrmVersion,
                presetName = presetName,
                enableLilToonConversion = enableLilToonConversion,
                enableHdrClamp = enableHdrClamp,
                enableOutlineConversion = enableOutlineConversion,
                transparentMode = transparentMode,
                enableTPoseNormalization = enableTPoseNormalization,
                enableArmatureRotationBake = enableArmatureRotationBake,
                enableBoneOrientationNormalization = enableBoneOrientationNormalization,
                enableExpressionAutoMapping = enableExpressionAutoMapping,
                expressionNamingConvention = expressionNamingConvention,
                enableSpringBoneConversion = enableSpringBoneConversion,
                enableColliderConversion = enableColliderConversion,
                outputFolder = outputFolder,
                fileNameMode = fileNameMode,
                customFileName = customFileName
            };
        }
    }

    /// <summary>
    /// 透過処理モード
    /// </summary>
    public enum TransparentMode
    {
        [Tooltip("元のシェーダー設定に従う")]
        Auto,

        [Tooltip("Cutout (アルファ閾値で切り抜き)")]
        Cutout,

        [Tooltip("Transparent (半透明)")]
        Transparent,

        [Tooltip("Opaque (不透明に強制)")]
        Opaque
    }

    /// <summary>
    /// BlendShape命名規則
    /// </summary>
    public enum NamingConvention
    {
        [Tooltip("自動検出")]
        Auto,

        [Tooltip("VRChat形式 (vrc.v_aa, vrc.blink 等)")]
        VRChat,

        [Tooltip("日本語 (あ, い, う, まばたき 等)")]
        Japanese,

        [Tooltip("英語 (aa, ih, happy, blink 等)")]
        English,

        [Tooltip("ARKit形式 (jawOpen, eyeBlinkLeft 等)")]
        ARKit
    }

    /// <summary>
    /// ファイル名モード
    /// </summary>
    public enum FileNameMode
    {
        [Tooltip("モデル名から自動生成")]
        Auto,

        [Tooltip("カスタムファイル名")]
        Custom,

        [Tooltip("タイムスタンプ付き")]
        WithTimestamp
    }
}
