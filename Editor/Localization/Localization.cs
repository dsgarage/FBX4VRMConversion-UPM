using UnityEditor;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Localization
{
    /// <summary>
    /// ローカライゼーション管理
    /// 日本語/英語の切り替えをサポート
    /// </summary>
    public static class Localize
    {
        private const string LanguageKey = "FBX4VRM_Language";

        public enum Language
        {
            Japanese,
            English
        }

        private static Language _currentLanguage = Language.Japanese;

        static Localize()
        {
            // EditorPrefsから言語設定を読み込み
            _currentLanguage = (Language)EditorPrefs.GetInt(LanguageKey, (int)Language.Japanese);
        }

        public static Language CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                _currentLanguage = value;
                EditorPrefs.SetInt(LanguageKey, (int)value);
            }
        }

        public static bool IsJapanese => _currentLanguage == Language.Japanese;

        /// <summary>
        /// 言語に応じた文字列を返す
        /// </summary>
        public static string Get(string ja, string en) => IsJapanese ? ja : en;

        // ========== 共通 ==========
        public static string Export => Get("エクスポート", "Export");
        public static string Cancel => Get("キャンセル", "Cancel");
        public static string Close => Get("閉じる", "Close");
        public static string Success => Get("成功", "Success");
        public static string Failed => Get("失敗", "Failed");
        public static string Warning => Get("警告", "Warning");
        public static string Error => Get("エラー", "Error");
        public static string Info => Get("情報", "Info");

        // ========== Export Window ==========
        public static string ExportWindowTitle => Get("FBX4VRM エクスポート", "FBX4VRM Export");
        public static string QuickExportTitle => Get("クイック VRM エクスポート", "Quick VRM Export");
        public static string SelectRootObject => Get("ルートオブジェクトを選択", "Select Root Object");
        public static string OutputFolder => Get("出力フォルダ", "Output Folder");
        public static string VrmVersion => Get("VRMバージョン", "VRM Version");
        public static string Preview => Get("プレビュー", "Preview");
        public static string ExportVrm => Get("VRMをエクスポート", "Export VRM");
        public static string ReadyToExport => Get("✓ エクスポート準備完了", "✓ Ready to export");

        // ========== Validation ==========
        public static string NoAnimator => Get("Animatorコンポーネントがありません", "No Animator component found");
        public static string NotHumanoid => Get("Humanoidアバターではありません", "Avatar is not Humanoid");
        public static string SelectSceneObject => Get("シーン上のオブジェクトを選択してください", "Please select a scene object");

        // ========== Processors ==========
        public static string ProcessorRootValidation => Get("ルート検証", "Root Validation");
        public static string ProcessorHumanoidValidation => Get("Humanoid検証", "Humanoid Validation");
        public static string ProcessorLilToonDetect => Get("lilToon検出", "lilToon Detect");
        public static string ProcessorLilToonConvert => Get("lilToon→MToon変換", "lilToon to MToon Convert");
        public static string ProcessorGltfClamp => Get("glTF値クランプ", "glTF Value Clamp");
        public static string ProcessorExpressions => Get("Expression設定", "Expressions Setup");
        public static string ProcessorSpringBone => Get("SpringBone変換", "SpringBone Convert");

        // ========== Results ==========
        public static string ExportCompleted => Get("エクスポート完了", "Export Completed");
        public static string ExportFailed => Get("エクスポート失敗", "Export Failed");
        public static string ShowInFinder => Get("Finderで表示", "Show in Finder");
        public static string ViewReport => Get("レポートを表示", "View Report");
        public static string CopyToClipboard => Get("クリップボードにコピー", "Copy to Clipboard");
        public static string SaveReport => Get("レポートを保存", "Save Report");

        // ========== Pipeline Log ==========
        public static string PipelineStarted => Get("パイプライン開始", "Pipeline Started");
        public static string PipelineCompleted => Get("パイプライン完了", "Pipeline Completed");
        public static string ExecutingProcessor => Get("実行中", "Executing");
        public static string ProcessorCompleted => Get("完了", "Completed");
        public static string ProcessorSkipped => Get("スキップ", "Skipped");
        public static string ProcessorFailed => Get("失敗", "Failed");

        // ========== Humanoid ==========
        public static string RequiredBonesFound => Get("必須ボーン: すべて検出", "Required bones: All found");
        public static string RequiredBonesMissing => Get("必須ボーン不足", "Missing required bones");
        public static string RecommendedBonesMissing => Get("推奨ボーン不足", "Missing recommended bones");
        public static string TPoseOk => Get("T-Pose: OK", "T-Pose: OK");
        public static string TPoseWarning => Get("T-Pose: 要確認", "T-Pose: Check required");
        public static string HierarchyOk => Get("階層構造: OK", "Hierarchy: OK");
        public static string HierarchyWarning => Get("階層構造: 要確認", "Hierarchy: Check required");

        // ========== Materials ==========
        public static string MaterialsDetected => Get("マテリアル検出", "Materials Detected");
        public static string MaterialsConverted => Get("マテリアル変換", "Materials Converted");
        public static string ValuesClampedCount => Get("値クランプ", "Values Clamped");
        public static string NoClampRequired => Get("クランプ不要", "No clamp required");

        // ========== Expressions ==========
        public static string BlendShapesFound => Get("BlendShape検出", "BlendShapes Found");
        public static string ExpressionsMappped => Get("Expression マッピング", "Expressions Mapped");
        public static string RecommendedExpressionsMissing => Get("推奨Expression不足", "Missing recommended expressions");

        // ========== SpringBone ==========
        public static string DynamicsBonesDetected => Get("ダイナミクスボーン検出", "Dynamics Bones Detected");
        public static string NoDynamicsBones => Get("ダイナミクスボーンなし", "No dynamics bones");
        public static string ConversionApproximate => Get("変換は近似値です", "Conversion is approximate");
    }
}
