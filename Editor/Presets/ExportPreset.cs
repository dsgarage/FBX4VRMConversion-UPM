using System;
using System.Collections.Generic;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Presets
{
    /// <summary>
    /// VRMエクスポート設定プリセット
    /// 用途別・作品別に設定を保存・再利用可能
    /// </summary>
    [CreateAssetMenu(fileName = "NewExportPreset", menuName = "FBX4VRM/Export Preset")]
    public class ExportPreset : ScriptableObject
    {
        [Header("Preset Info")]
        [Tooltip("プリセット名")]
        public string presetName = "New Preset";

        [TextArea(2, 4)]
        [Tooltip("プリセットの説明")]
        public string description = "";

        [Tooltip("用途タグ（VRChat, Cluster, etc.）")]
        public string[] tags = new string[0];

        [Header("Export Settings")]
        [Tooltip("VRMバージョン: 0 = VRM 0.x, 1 = VRM 1.0 (Avatar構築にバグあり)")]
        public int vrmVersion = 0;

        [Tooltip("出力フォルダパス（相対または絶対）")]
        public string outputFolder = "VRM_Export";

        [Header("Processor Settings")]
        [Tooltip("無効化するProcessorのID一覧")]
        public string[] disabledProcessors = new string[0];

        [Header("Material Settings")]
        [Tooltip("lilToon変換を有効にする")]
        public bool enableLilToonConversion = true;

        [Tooltip("HDR値の自動クランプを有効にする")]
        public bool enableHdrClamp = true;

        [Header("Expression Settings")]
        [Tooltip("Expression自動マッピングを有効にする")]
        public bool enableExpressionMapping = true;

        [Tooltip("カスタムExpressionマッピング")]
        public ExpressionMapping[] customExpressionMappings = new ExpressionMapping[0];

        [Header("SpringBone Settings")]
        [Tooltip("PhysBone/DynamicBone変換を有効にする")]
        public bool enableSpringBoneConversion = true;

        [Header("VRM Metadata")]
        [Tooltip("デフォルトのVRMメタデータ")]
        public VrmMetadataPreset metadata = new VrmMetadataPreset();

        /// <summary>
        /// プリセットの複製を作成
        /// </summary>
        public ExportPreset Clone()
        {
            var clone = CreateInstance<ExportPreset>();
            clone.presetName = presetName + " (Copy)";
            clone.description = description;
            clone.tags = (string[])tags.Clone();
            clone.vrmVersion = vrmVersion;
            clone.outputFolder = outputFolder;
            clone.disabledProcessors = (string[])disabledProcessors.Clone();
            clone.enableLilToonConversion = enableLilToonConversion;
            clone.enableHdrClamp = enableHdrClamp;
            clone.enableExpressionMapping = enableExpressionMapping;
            clone.customExpressionMappings = (ExpressionMapping[])customExpressionMappings.Clone();
            clone.enableSpringBoneConversion = enableSpringBoneConversion;
            clone.metadata = metadata.Clone();
            return clone;
        }
    }

    /// <summary>
    /// カスタムExpressionマッピング
    /// </summary>
    [Serializable]
    public class ExpressionMapping
    {
        [Tooltip("BlendShape名（部分一致）")]
        public string blendShapeName;

        [Tooltip("マッピング先のVRM Expression名")]
        public string vrmExpressionName;
    }

    /// <summary>
    /// VRMメタデータプリセット
    /// </summary>
    [Serializable]
    public class VrmMetadataPreset
    {
        [Header("Basic Info")]
        public string title = "";
        public string version = "1.0";
        public string author = "";
        public string contactInformation = "";
        public string reference = "";

        [Header("License (VRM 1.0)")]
        [Tooltip("アバターの利用許諾")]
        public AvatarPermission avatarPermission = AvatarPermission.OnlyAuthor;

        [Tooltip("商用利用")]
        public CommercialUsage commercialUsage = CommercialUsage.PersonalNonProfit;

        [Tooltip("政治・宗教利用")]
        public bool allowPoliticalOrReligiousUsage = false;

        [Tooltip("暴力表現")]
        public bool allowViolentUsage = false;

        [Tooltip("性的表現")]
        public bool allowSexualUsage = false;

        [Tooltip("アンチソーシャル利用")]
        public bool allowAntisocialUsage = false;

        [Header("Credit")]
        [Tooltip("クレジット表記")]
        public CreditNotation creditNotation = CreditNotation.Required;

        [Tooltip("再配布")]
        public bool allowRedistribution = false;

        [Tooltip("改変")]
        public ModificationPermission modification = ModificationPermission.AllowModification;

        [TextArea(2, 4)]
        [Tooltip("その他のライセンス条項")]
        public string otherLicenseUrl = "";

        public VrmMetadataPreset Clone()
        {
            return new VrmMetadataPreset
            {
                title = title,
                version = version,
                author = author,
                contactInformation = contactInformation,
                reference = reference,
                avatarPermission = avatarPermission,
                commercialUsage = commercialUsage,
                allowPoliticalOrReligiousUsage = allowPoliticalOrReligiousUsage,
                allowViolentUsage = allowViolentUsage,
                allowSexualUsage = allowSexualUsage,
                allowAntisocialUsage = allowAntisocialUsage,
                creditNotation = creditNotation,
                allowRedistribution = allowRedistribution,
                modification = modification,
                otherLicenseUrl = otherLicenseUrl
            };
        }
    }

    /// <summary>
    /// アバター利用許諾
    /// </summary>
    public enum AvatarPermission
    {
        OnlyAuthor,
        ExplicitlyLicensedPerson,
        Everyone
    }

    /// <summary>
    /// 商用利用
    /// </summary>
    public enum CommercialUsage
    {
        PersonalNonProfit,
        PersonalProfit,
        Corporation
    }

    /// <summary>
    /// クレジット表記
    /// </summary>
    public enum CreditNotation
    {
        Required,
        Unnecessary
    }

    /// <summary>
    /// 改変許可
    /// </summary>
    public enum ModificationPermission
    {
        Prohibited,
        AllowModification,
        AllowModificationRedistribution
    }
}
