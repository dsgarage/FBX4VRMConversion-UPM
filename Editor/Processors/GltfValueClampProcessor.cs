using System.Collections.Generic;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// glTF値クランプProcessor
    /// HDR値や範囲外値をglTF仕様の範囲(0-1)に自動クランプする
    /// クランプ時は必ずWarning通知を生成（サイレント改変禁止）
    /// </summary>
    public class GltfValueClampProcessor : ExportProcessorBase
    {
        public override string Id => "gltf_value_clamp";
        public override string DisplayName => "glTF Value Clamp";
        public override string Description => "Clamps HDR and out-of-range values to glTF specification (0-1)";
        public override int Order => 30; // lilToon変換(20)の後

        /// <summary>
        /// クランプ対象のシェーダープロパティ名（色系）
        /// </summary>
        private static readonly string[] ColorProperties = new[]
        {
            "_Color",
            "_MainColor",
            "_BaseColor",
            "_BaseColorFactor",
            "_ShadeColor",
            "_ShadeColorFactor",
            "_EmissionColor",
            "_EmissiveColor",
            "_EmissiveFactor",
            "_RimColor",
            "_ParametricRimColorFactor",
            "_OutlineColor",
            "_OutlineColorFactor",
            "_MatcapColor",
            "_MatcapColorFactor",
            "_SpecColor",
            "_SpecularColor",
            "_ReflectionColor"
        };

        /// <summary>
        /// クランプ対象のシェーダープロパティ名（0-1範囲のfloat）
        /// </summary>
        private static readonly string[] FloatProperties = new[]
        {
            "_Cutoff",
            "_AlphaCutoff",
            "_Metallic",
            "_Glossiness",
            "_Smoothness",
            "_OcclusionStrength",
            "_BumpScale",
            "_NormalScale"
        };

        /// <summary>
        /// クランプ結果
        /// </summary>
        public class ClampResult
        {
            public Material Material { get; set; }
            public string PropertyName { get; set; }
            public string PropertyType { get; set; } // "Color" or "Float"
            public string OriginalValue { get; set; }
            public string ClampedValue { get; set; }
        }

        public override ProcessorResult Execute(ExportContext context)
        {
            var result = CreateResult();
            var root = context.ClonedRoot;

            if (root == null)
            {
                result.AddWarning(Id, "Cloned root is null, skipping value clamping");
                return result;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var processedMaterials = new HashSet<Material>();
            var clampResults = new List<ClampResult>();

            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    if (processedMaterials.Contains(material)) continue;

                    processedMaterials.Add(material);

                    // 色プロパティのクランプ
                    foreach (var propName in ColorProperties)
                    {
                        if (material.HasProperty(propName))
                        {
                            var clampResult = ClampColorProperty(material, propName);
                            if (clampResult != null)
                            {
                                clampResults.Add(clampResult);
                            }
                        }
                    }

                    // Floatプロパティのクランプ
                    foreach (var propName in FloatProperties)
                    {
                        if (material.HasProperty(propName))
                        {
                            var clampResult = ClampFloatProperty(material, propName);
                            if (clampResult != null)
                            {
                                clampResults.Add(clampResult);
                            }
                        }
                    }
                }
            }

            // 結果通知
            if (clampResults.Count > 0)
            {
                // マテリアルごとにグループ化
                var byMaterial = new Dictionary<string, List<ClampResult>>();
                foreach (var cr in clampResults)
                {
                    var matName = cr.Material.name;
                    if (!byMaterial.ContainsKey(matName))
                    {
                        byMaterial[matName] = new List<ClampResult>();
                    }
                    byMaterial[matName].Add(cr);
                }

                // 各マテリアルの警告
                foreach (var kvp in byMaterial)
                {
                    var details = new List<string>();
                    foreach (var cr in kvp.Value)
                    {
                        details.Add($"  {cr.PropertyName}: {cr.OriginalValue} → {cr.ClampedValue}");
                    }

                    result.AddWarning(Id,
                        $"Clamped {kvp.Value.Count} value(s) in material '{kvp.Key}'",
                        string.Join("\n", details));
                }

                result.AddInfo(Id,
                    $"Total: {clampResults.Count} value(s) clamped across {byMaterial.Count} material(s)");
            }
            else
            {
                result.AddInfo(Id, "No values required clamping");
            }

            return result;
        }

        /// <summary>
        /// 色プロパティをクランプ
        /// </summary>
        private ClampResult ClampColorProperty(Material material, string propertyName)
        {
            var color = material.GetColor(propertyName);
            var originalColor = color;
            bool needsClamp = false;

            // 各チャンネルをチェック
            if (color.r < 0 || color.r > 1)
            {
                color.r = Mathf.Clamp01(color.r);
                needsClamp = true;
            }
            if (color.g < 0 || color.g > 1)
            {
                color.g = Mathf.Clamp01(color.g);
                needsClamp = true;
            }
            if (color.b < 0 || color.b > 1)
            {
                color.b = Mathf.Clamp01(color.b);
                needsClamp = true;
            }
            if (color.a < 0 || color.a > 1)
            {
                color.a = Mathf.Clamp01(color.a);
                needsClamp = true;
            }

            if (needsClamp)
            {
                material.SetColor(propertyName, color);

                return new ClampResult
                {
                    Material = material,
                    PropertyName = propertyName,
                    PropertyType = "Color",
                    OriginalValue = FormatColor(originalColor),
                    ClampedValue = FormatColor(color)
                };
            }

            return null;
        }

        /// <summary>
        /// Floatプロパティをクランプ
        /// </summary>
        private ClampResult ClampFloatProperty(Material material, string propertyName)
        {
            var value = material.GetFloat(propertyName);
            var originalValue = value;

            if (value < 0 || value > 1)
            {
                value = Mathf.Clamp01(value);
                material.SetFloat(propertyName, value);

                return new ClampResult
                {
                    Material = material,
                    PropertyName = propertyName,
                    PropertyType = "Float",
                    OriginalValue = originalValue.ToString("F3"),
                    ClampedValue = value.ToString("F3")
                };
            }

            return null;
        }

        /// <summary>
        /// 色を文字列にフォーマット
        /// </summary>
        private string FormatColor(Color color)
        {
            return $"RGBA({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})";
        }
    }
}
