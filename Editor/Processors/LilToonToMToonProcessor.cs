using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// lilToon→MToon自動変換Processor
    /// 検出されたlilToonマテリアルをMToonに変換し、Rendererに差し替える
    /// 元マテリアルは変更せず、複製を生成して差し替え
    /// </summary>
    public class LilToonToMToonProcessor : ExportProcessorBase
    {
        public override string Id => "liltoon_to_mtoon_convert";
        public override string DisplayName => "lilToon to MToon Convert";
        public override string Description => "Converts lilToon materials to MToon format";
        public override int Order => 20; // LilToonDetect(10)の後

        /// <summary>
        /// SharedDataに保存するキー（変換結果）
        /// </summary>
        public const string SHARED_KEY_CONVERTED_MATERIALS = "ConvertedMToonMaterials";

        public override ProcessorResult Execute(ExportContext context)
        {
            var result = CreateResult();
            var root = context.ClonedRoot;

            if (root == null)
            {
                result.AddWarning(Id, "Cloned root is null, skipping material conversion");
                return result;
            }

            // LilToonDetectProcessorの結果を取得
            var lilToonMaterials = context.GetSharedData<List<Material>>(
                LilToonDetectProcessor.SHARED_KEY_LILTOON_MATERIALS);

            if (lilToonMaterials == null || lilToonMaterials.Count == 0)
            {
                result.AddInfo(Id, "No lilToon materials to convert");
                return result;
            }

            // MToonシェーダーを取得
            Shader mtoonShader;
            if (context.VrmVersion == 1)
            {
                mtoonShader = LilToonToMToonConverter.GetMToon10Shader();
            }
            else
            {
                mtoonShader = LilToonToMToonConverter.GetMToonShader();
            }

            if (mtoonShader == null)
            {
                result.AddError(Id, "MToon shader not found. Please ensure UniVRM is properly installed.");
                return result;
            }

            // 変換マッピング（元マテリアル → 変換後マテリアル）
            var conversionMap = new Dictionary<Material, Material>();
            var allWarnings = new List<string>();
            var allUnsupportedFeatures = new List<string>();
            int convertedCount = 0;
            int failedCount = 0;

            // 各lilToonマテリアルを変換
            foreach (var sourceMat in lilToonMaterials)
            {
                var conversionResult = LilToonToMToonConverter.Convert(sourceMat, mtoonShader);

                if (conversionResult.Success && conversionResult.ConvertedMaterial != null)
                {
                    conversionMap[sourceMat] = conversionResult.ConvertedMaterial;
                    convertedCount++;

                    // 警告を集約
                    foreach (var warning in conversionResult.Warnings)
                    {
                        allWarnings.Add($"[{sourceMat.name}] {warning}");
                    }

                    foreach (var feature in conversionResult.UnsupportedFeatures)
                    {
                        allUnsupportedFeatures.Add($"[{sourceMat.name}] {feature}");
                    }

                    Debug.Log($"[FBX4VRM] Converted: {sourceMat.name} → {conversionResult.ConvertedMaterial.name}");
                }
                else
                {
                    failedCount++;
                    result.AddWarning(Id, $"Failed to convert material: {sourceMat.name}",
                        string.Join("\n", conversionResult.Warnings));
                }
            }

            // 変換結果をSharedDataに保存
            context.SetSharedData(SHARED_KEY_CONVERTED_MATERIALS, conversionMap);

            // Rendererのマテリアルを差し替え
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            int replacedCount = 0;

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                var newMaterials = new Material[materials.Length];
                bool hasReplacement = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && conversionMap.TryGetValue(materials[i], out var convertedMat))
                    {
                        newMaterials[i] = convertedMat;
                        hasReplacement = true;
                        replacedCount++;
                    }
                    else
                    {
                        newMaterials[i] = materials[i];
                    }
                }

                if (hasReplacement)
                {
                    renderer.sharedMaterials = newMaterials;
                }
            }

            // 結果通知
            if (convertedCount > 0)
            {
                result.AddInfo(Id,
                    $"Converted {convertedCount} lilToon material(s) to MToon",
                    $"Replaced {replacedCount} material slot(s) on renderers");

                // 警告があれば追加
                if (allWarnings.Count > 0)
                {
                    result.AddWarning(Id,
                        $"{allWarnings.Count} conversion warning(s)",
                        string.Join("\n", allWarnings.Take(10))); // 最大10件
                }

                if (allUnsupportedFeatures.Count > 0)
                {
                    result.AddWarning(Id,
                        $"{allUnsupportedFeatures.Count} unsupported feature(s) detected",
                        string.Join("\n", allUnsupportedFeatures.Take(10)));
                }
            }

            if (failedCount > 0)
            {
                result.AddWarning(Id, $"{failedCount} material(s) failed to convert");
            }

            return result;
        }
    }
}
