using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Presets
{
    /// <summary>
    /// プリセット管理クラス
    /// プリセットの検索、保存、読み込みを行う
    /// </summary>
    public static class PresetManager
    {
        /// <summary>
        /// デフォルトのプリセット保存先
        /// </summary>
        public const string DefaultPresetFolder = "Assets/FBX4VRM/Presets";

        /// <summary>
        /// ビルトインプリセットのリソースパス
        /// </summary>
        private const string BuiltinPresetPath = "Packages/com.dsgarage.fbx4vrmconversion/Editor/Presets/Builtin";

        /// <summary>
        /// 全プリセットを取得
        /// </summary>
        public static List<ExportPreset> GetAllPresets()
        {
            var presets = new List<ExportPreset>();

            // ビルトインプリセット
            var builtinGuids = AssetDatabase.FindAssets("t:ExportPreset", new[] { BuiltinPresetPath });
            foreach (var guid in builtinGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<ExportPreset>(path);
                if (preset != null)
                {
                    presets.Add(preset);
                }
            }

            // ユーザープリセット（プロジェクト内）
            var userGuids = AssetDatabase.FindAssets("t:ExportPreset", new[] { "Assets" });
            foreach (var guid in userGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<ExportPreset>(path);
                if (preset != null && !presets.Contains(preset))
                {
                    presets.Add(preset);
                }
            }

            return presets;
        }

        /// <summary>
        /// タグでプリセットをフィルタ
        /// </summary>
        public static List<ExportPreset> GetPresetsByTag(string tag)
        {
            return GetAllPresets()
                .Where(p => p.tags != null && p.tags.Contains(tag))
                .ToList();
        }

        /// <summary>
        /// プリセットを保存
        /// </summary>
        public static ExportPreset SavePreset(ExportPreset preset, string folderPath = null)
        {
            if (preset == null) return null;

            folderPath ??= DefaultPresetFolder;

            // フォルダ作成
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            // ファイル名を生成
            var fileName = SanitizeFileName(preset.presetName);
            var fullPath = Path.Combine(folderPath, $"{fileName}.asset");

            // 既存チェック
            var existingPreset = AssetDatabase.LoadAssetAtPath<ExportPreset>(fullPath);
            if (existingPreset != null)
            {
                // 上書き確認
                if (!EditorUtility.DisplayDialog(
                    "Overwrite Preset",
                    $"Preset '{preset.presetName}' already exists. Overwrite?",
                    "Overwrite",
                    "Cancel"))
                {
                    return null;
                }

                // 既存プリセットを更新
                EditorUtility.CopySerialized(preset, existingPreset);
                EditorUtility.SetDirty(existingPreset);
                AssetDatabase.SaveAssets();
                return existingPreset;
            }

            // 新規作成
            AssetDatabase.CreateAsset(preset, fullPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[FBX4VRM] Preset saved: {fullPath}");
            return AssetDatabase.LoadAssetAtPath<ExportPreset>(fullPath);
        }

        /// <summary>
        /// プリセットを削除
        /// </summary>
        public static bool DeletePreset(ExportPreset preset)
        {
            if (preset == null) return false;

            var path = AssetDatabase.GetAssetPath(preset);
            if (string.IsNullOrEmpty(path)) return false;

            // ビルトインは削除不可
            if (path.StartsWith("Packages/"))
            {
                Debug.LogWarning("[FBX4VRM] Cannot delete built-in preset");
                return false;
            }

            // 確認ダイアログ
            if (!EditorUtility.DisplayDialog(
                "Delete Preset",
                $"Delete preset '{preset.presetName}'?",
                "Delete",
                "Cancel"))
            {
                return false;
            }

            AssetDatabase.DeleteAsset(path);
            Debug.Log($"[FBX4VRM] Preset deleted: {path}");
            return true;
        }

        /// <summary>
        /// 現在の設定からプリセットを作成
        /// </summary>
        public static ExportPreset CreatePresetFromCurrentSettings(
            string name,
            int vrmVersion,
            string outputFolder,
            string[] disabledProcessors = null)
        {
            var preset = ScriptableObject.CreateInstance<ExportPreset>();
            preset.presetName = name;
            preset.vrmVersion = vrmVersion;
            preset.outputFolder = outputFolder;
            preset.disabledProcessors = disabledProcessors ?? new string[0];
            return preset;
        }

        /// <summary>
        /// デフォルトプリセットを取得または作成
        /// VRChatプリセットを優先、なければDefaultを作成
        /// </summary>
        public static ExportPreset GetOrCreateDefaultPreset()
        {
            var presets = GetAllPresets();

            // VRChatプリセットを優先的に探す
            var vrchatPreset = presets.FirstOrDefault(p => p.presetName == "VRChat");
            if (vrchatPreset != null)
            {
                return vrchatPreset;
            }

            // 次にDefaultプリセットを探す
            var defaultPreset = presets.FirstOrDefault(p => p.presetName == "Default");
            if (defaultPreset != null)
            {
                return defaultPreset;
            }

            // VRChat用デフォルトプリセット作成
            var preset = ScriptableObject.CreateInstance<ExportPreset>();
            preset.presetName = "VRChat";
            preset.description = "Optimized settings for VRChat avatars (VRM 0.x)";
            preset.tags = new[] { "VRChat", "Social VR" };
            preset.vrmVersion = 0;
            preset.outputFolder = "VRM_Export";
            preset.enableSpringBoneConversion = true;

            return SavePreset(preset);
        }

        /// <summary>
        /// ファイル名に使用できない文字を除去
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
            return string.IsNullOrEmpty(sanitized) ? "Preset" : sanitized;
        }

        /// <summary>
        /// ビルトインプリセットを作成（開発用）
        /// </summary>
        [MenuItem("Tools/FBX4VRM/Create Builtin Presets", false, 100)]
        private static void CreateBuiltinPresets()
        {
            var folder = "Assets/FBX4VRM/Presets/Builtin";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // VRChat用プリセット
            var vrchatPreset = ScriptableObject.CreateInstance<ExportPreset>();
            vrchatPreset.presetName = "VRChat";
            vrchatPreset.description = "Optimized settings for VRChat avatars";
            vrchatPreset.tags = new[] { "VRChat", "Social VR" };
            vrchatPreset.vrmVersion = 0; // VRChat uses VRM 0.x
            vrchatPreset.enableSpringBoneConversion = true;
            AssetDatabase.CreateAsset(vrchatPreset, $"{folder}/VRChat.asset");

            // Cluster用プリセット
            var clusterPreset = ScriptableObject.CreateInstance<ExportPreset>();
            clusterPreset.presetName = "Cluster";
            clusterPreset.description = "Optimized settings for Cluster avatars (VRM 1.0 - Avatar構築にバグあり)";
            clusterPreset.tags = new[] { "Cluster", "Social VR" };
            clusterPreset.vrmVersion = 1; // Cluster supports VRM 1.0
            AssetDatabase.CreateAsset(clusterPreset, $"{folder}/Cluster.asset");

            // Generic VRM 1.0プリセット
            var vrm10Preset = ScriptableObject.CreateInstance<ExportPreset>();
            vrm10Preset.presetName = "VRM 1.0 Standard (Avatar構築にバグあり)";
            vrm10Preset.description = "Standard VRM 1.0 export settings - Avatar構築にバグがあります";
            vrm10Preset.tags = new[] { "VRM 1.0", "Standard" };
            vrm10Preset.vrmVersion = 1;
            AssetDatabase.CreateAsset(vrm10Preset, $"{folder}/VRM10_Standard.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[FBX4VRM] Built-in presets created");
        }
    }
}
