using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// lilToonマテリアル検出Processor
    /// モデルに使用されているlilToonマテリアルを検出し、SharedDataに保存する
    /// </summary>
    public class LilToonDetectProcessor : ExportProcessorBase
    {
        public override string Id => "liltoon_detect";
        public override string DisplayName => "lilToon Detect";
        public override string Description => "Detects lilToon materials used in the model";
        public override int Order => 10; // RootValidation(0)の後

        /// <summary>
        /// SharedDataに保存するキー
        /// </summary>
        public const string SHARED_KEY_LILTOON_MATERIALS = "LilToonMaterials";
        public const string SHARED_KEY_LILTOON_RENDERERS = "LilToonRenderers";

        /// <summary>
        /// lilToonシェーダー名のプレフィックス一覧
        /// </summary>
        private static readonly string[] LilToonShaderPrefixes = new[]
        {
            "lilToon",
            "Hidden/lilToon",
            "_lil/",
            "lil/"
        };

        /// <summary>
        /// lilToonシェーダーの種類を判別するためのキーワード
        /// </summary>
        private static readonly Dictionary<string, string> LilToonVariants = new Dictionary<string, string>
        {
            { "Cutout", "Cutout (Alpha Clip)" },
            { "Transparent", "Transparent" },
            { "Outline", "With Outline" },
            { "Fur", "Fur" },
            { "Refraction", "Refraction" },
            { "Gem", "Gem" },
            { "FakeShadow", "Fake Shadow" },
            { "Overlay", "Overlay" },
            { "Lite", "Lite Version" },
            { "Multi", "Multi" }
        };

        public override ProcessorResult Execute(ExportContext context)
        {
            var result = CreateResult();
            var root = context.ClonedRoot ?? context.SourceRoot;

            if (root == null)
            {
                result.AddError(Id, "Root object is null");
                return result;
            }

            // 全Rendererを取得
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var lilToonMaterials = new List<Material>();
            var lilToonRenderers = new Dictionary<Renderer, List<int>>(); // Renderer -> マテリアルインデックスのリスト

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    if (mat == null) continue;

                    if (IsLilToonMaterial(mat))
                    {
                        if (!lilToonMaterials.Contains(mat))
                        {
                            lilToonMaterials.Add(mat);
                        }

                        if (!lilToonRenderers.ContainsKey(renderer))
                        {
                            lilToonRenderers[renderer] = new List<int>();
                        }
                        lilToonRenderers[renderer].Add(i);
                    }
                }
            }

            // 結果をSharedDataに保存
            context.SetSharedData(SHARED_KEY_LILTOON_MATERIALS, lilToonMaterials);
            context.SetSharedData(SHARED_KEY_LILTOON_RENDERERS, lilToonRenderers);

            // 通知を生成
            if (lilToonMaterials.Count > 0)
            {
                var materialNames = string.Join(", ", lilToonMaterials.Select(m => m.name));
                var variantInfo = GetVariantInfo(lilToonMaterials);

                result.AddInfo(Id,
                    $"Detected {lilToonMaterials.Count} lilToon material(s)",
                    $"Materials: {materialNames}\n{variantInfo}");

                // 各マテリアルの詳細をログ
                foreach (var mat in lilToonMaterials)
                {
                    var shaderName = mat.shader?.name ?? "Unknown";
                    var variant = GetMaterialVariant(mat);
                    Debug.Log($"[FBX4VRM] lilToon material: {mat.name} (Shader: {shaderName}, Variant: {variant})");
                }
            }
            else
            {
                result.AddInfo(Id, "No lilToon materials detected");
            }

            return result;
        }

        /// <summary>
        /// マテリアルがlilToonかどうかを判定
        /// </summary>
        public static bool IsLilToonMaterial(Material material)
        {
            if (material == null || material.shader == null) return false;

            var shaderName = material.shader.name;
            return LilToonShaderPrefixes.Any(prefix => shaderName.StartsWith(prefix));
        }

        /// <summary>
        /// lilToonマテリアルのバリアント（種類）を取得
        /// </summary>
        public static string GetMaterialVariant(Material material)
        {
            if (material == null || material.shader == null) return "Unknown";

            var shaderName = material.shader.name;
            var variants = new List<string>();

            foreach (var kvp in LilToonVariants)
            {
                if (shaderName.Contains(kvp.Key))
                {
                    variants.Add(kvp.Value);
                }
            }

            if (variants.Count == 0)
            {
                return "Standard";
            }

            return string.Join(", ", variants);
        }

        /// <summary>
        /// 検出したマテリアルのバリアント情報を集計
        /// </summary>
        private string GetVariantInfo(List<Material> materials)
        {
            var variantCounts = new Dictionary<string, int>();

            foreach (var mat in materials)
            {
                var variant = GetMaterialVariant(mat);
                if (!variantCounts.ContainsKey(variant))
                {
                    variantCounts[variant] = 0;
                }
                variantCounts[variant]++;
            }

            var lines = variantCounts.Select(kvp => $"  - {kvp.Key}: {kvp.Value}");
            return "Variants:\n" + string.Join("\n", lines);
        }
    }
}
