using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// lilToon → MToon 変換ユーティリティ
    /// lilToonマテリアルのプロパティをMToon10にマッピングする
    /// </summary>
    public static class LilToonToMToonConverter
    {
        // lilToonプロパティ名
        private static class LilToonProps
        {
            // Main
            public const string MainTex = "_MainTex";
            public const string MainColor = "_Color";
            public const string MainTexHSVG = "_MainTexHSVG"; // Hue, Saturation, Value, Gamma

            // Normal
            public const string BumpMap = "_BumpMap";
            public const string BumpScale = "_BumpScale";

            // Emission
            public const string EmissionMap = "_EmissionMap";
            public const string EmissionColor = "_EmissionColor";
            public const string EmissionBlend = "_EmissionBlend";

            // Shadow
            public const string UseShadow = "_UseShadow";
            public const string ShadowColor = "_ShadowColor";
            public const string Shadow2ndColor = "_Shadow2ndColor";
            public const string ShadowMainStrength = "_ShadowMainStrength";
            public const string ShadowBorderColor = "_ShadowBorderColor";

            // Rim
            public const string UseRim = "_UseRim";
            public const string RimColor = "_RimColor";
            public const string RimFresnelPower = "_RimFresnelPower";

            // Outline
            public const string UseOutline = "_UseOutline";
            public const string OutlineColor = "_OutlineColor";
            public const string OutlineWidth = "_OutlineWidth";
            public const string OutlineTex = "_OutlineTex";

            // Alpha
            public const string Cutoff = "_Cutoff";
            public const string TransparentMode = "_TransparentMode";

            // Rendering
            public const string Cull = "_Cull";
            public const string SrcBlend = "_SrcBlend";
            public const string DstBlend = "_DstBlend";
            public const string ZWrite = "_ZWrite";
        }

        // MToon10プロパティ名
        private static class MToonProps
        {
            // Rendering
            public const string AlphaMode = "_AlphaMode";
            public const string TransparentWithZWrite = "_TransparentWithZWrite";
            public const string AlphaCutoff = "_Cutoff";
            public const string RenderQueueOffsetNumber = "_RenderQueueOffsetNumber";
            public const string DoubleSided = "_DoubleSided";

            // Color
            public const string BaseColorFactor = "_Color";
            public const string BaseColorTexture = "_MainTex";
            public const string ShadeColorFactor = "_ShadeColor";
            public const string ShadeColorTexture = "_ShadeTexture";

            // Normal
            public const string NormalTexture = "_BumpMap";
            public const string NormalTextureScale = "_BumpScale";

            // Shading
            public const string ShadingShiftFactor = "_ShadingShiftFactor";
            public const string ShadingShiftTexture = "_ShadingShiftTexture";
            public const string ShadingShiftTextureScale = "_ShadingShiftTextureScale";
            public const string ShadingToonyFactor = "_ShadingToonyFactor";

            // Rim
            public const string RimLightingMixFactor = "_RimLightingMixFactor";
            public const string RimMultiplyTexture = "_RimMultiplyTexture";
            public const string ParametricRimColorFactor = "_RimColor";
            public const string ParametricRimFresnelPowerFactor = "_RimFresnelPower";
            public const string ParametricRimLiftFactor = "_RimLift";

            // Emission
            public const string EmissiveFactor = "_EmissionColor";
            public const string EmissiveTexture = "_EmissionMap";

            // MatCap
            public const string MatcapColorFactor = "_MatcapColor";
            public const string MatcapTexture = "_MatcapTex";

            // Outline
            public const string OutlineWidthMode = "_OutlineWidthMode";
            public const string OutlineWidthFactor = "_OutlineWidth";
            public const string OutlineWidthMultiplyTexture = "_OutlineWidthTex";
            public const string OutlineColorFactor = "_OutlineColor";
            public const string OutlineLightingMixFactor = "_OutlineLightingMixFactor";

            // UV Animation
            public const string UvAnimationMaskTexture = "_UvAnimMaskTexture";
            public const string UvAnimationScrollXSpeedFactor = "_UvAnimScrollX";
            public const string UvAnimationScrollYSpeedFactor = "_UvAnimScrollY";
            public const string UvAnimationRotationSpeedFactor = "_UvAnimRotation";
        }

        /// <summary>
        /// 変換結果
        /// </summary>
        public class ConversionResult
        {
            public Material ConvertedMaterial { get; set; }
            public bool Success { get; set; }
            public List<string> Warnings { get; } = new List<string>();
            public List<string> UnsupportedFeatures { get; } = new List<string>();
        }

        /// <summary>
        /// lilToonマテリアルをMToon10に変換
        /// </summary>
        /// <param name="source">元のlilToonマテリアル</param>
        /// <param name="mtoonShader">MToon10シェーダー</param>
        /// <returns>変換結果</returns>
        public static ConversionResult Convert(Material source, Shader mtoonShader)
        {
            var result = new ConversionResult();

            if (source == null || mtoonShader == null)
            {
                result.Success = false;
                result.Warnings.Add("Source material or MToon shader is null");
                return result;
            }

            // 新しいマテリアルを作成（元マテリアルを変更しない）
            var converted = new Material(mtoonShader);
            converted.name = source.name + "_MToon";
            result.ConvertedMaterial = converted;

            try
            {
                // ベースカラー
                ConvertBaseColor(source, converted, result);

                // ノーマルマップ
                ConvertNormalMap(source, converted, result);

                // シェーディング（影）
                ConvertShading(source, converted, result);

                // エミッション
                ConvertEmission(source, converted, result);

                // リムライト
                ConvertRimLight(source, converted, result);

                // アウトライン
                ConvertOutline(source, converted, result);

                // アルファ/透過
                ConvertAlphaMode(source, converted, result);

                // レンダリング設定
                ConvertRenderingSettings(source, converted, result);

                result.Success = true;
            }
            catch (System.Exception ex)
            {
                result.Success = false;
                result.Warnings.Add($"Conversion error: {ex.Message}");
            }

            return result;
        }

        private static void ConvertBaseColor(Material source, Material dest, ConversionResult result)
        {
            // メインテクスチャ
            if (source.HasProperty(LilToonProps.MainTex))
            {
                var tex = source.GetTexture(LilToonProps.MainTex);
                if (tex != null)
                {
                    dest.SetTexture(MToonProps.BaseColorTexture, tex);
                }
            }

            // メインカラー
            if (source.HasProperty(LilToonProps.MainColor))
            {
                var color = source.GetColor(LilToonProps.MainColor);
                dest.SetColor(MToonProps.BaseColorFactor, color);

                // シェードカラーはメインカラーを暗くしたものをデフォルトに
                var shadeColor = color * 0.7f;
                shadeColor.a = 1f;
                dest.SetColor(MToonProps.ShadeColorFactor, shadeColor);
            }
        }

        private static void ConvertNormalMap(Material source, Material dest, ConversionResult result)
        {
            if (source.HasProperty(LilToonProps.BumpMap))
            {
                var normalTex = source.GetTexture(LilToonProps.BumpMap);
                if (normalTex != null)
                {
                    dest.SetTexture(MToonProps.NormalTexture, normalTex);
                }
            }

            if (source.HasProperty(LilToonProps.BumpScale))
            {
                var scale = source.GetFloat(LilToonProps.BumpScale);
                dest.SetFloat(MToonProps.NormalTextureScale, scale);
            }
        }

        private static void ConvertShading(Material source, Material dest, ConversionResult result)
        {
            // lilToonの影設定をMToonのシェーディングに変換
            if (source.HasProperty(LilToonProps.UseShadow) && source.GetFloat(LilToonProps.UseShadow) > 0)
            {
                if (source.HasProperty(LilToonProps.ShadowColor))
                {
                    var shadowColor = source.GetColor(LilToonProps.ShadowColor);
                    dest.SetColor(MToonProps.ShadeColorFactor, shadowColor);
                }

                // シェーディングパラメータ
                dest.SetFloat(MToonProps.ShadingToonyFactor, 0.9f);
                dest.SetFloat(MToonProps.ShadingShiftFactor, 0.0f);
            }
            else
            {
                // 影なしの場合、シェードカラーをベースカラーと同じに
                if (source.HasProperty(LilToonProps.MainColor))
                {
                    dest.SetColor(MToonProps.ShadeColorFactor, source.GetColor(LilToonProps.MainColor));
                }
            }
        }

        private static void ConvertEmission(Material source, Material dest, ConversionResult result)
        {
            if (source.HasProperty(LilToonProps.EmissionMap))
            {
                var emissionTex = source.GetTexture(LilToonProps.EmissionMap);
                if (emissionTex != null)
                {
                    dest.SetTexture(MToonProps.EmissiveTexture, emissionTex);
                }
            }

            if (source.HasProperty(LilToonProps.EmissionColor))
            {
                var emissionColor = source.GetColor(LilToonProps.EmissionColor);

                // HDR値をクランプ（別Processorでも処理するが、ここでも安全のため）
                emissionColor.r = Mathf.Clamp01(emissionColor.r);
                emissionColor.g = Mathf.Clamp01(emissionColor.g);
                emissionColor.b = Mathf.Clamp01(emissionColor.b);

                dest.SetColor(MToonProps.EmissiveFactor, emissionColor);

                // HDRだった場合は警告
                var originalColor = source.GetColor(LilToonProps.EmissionColor);
                if (originalColor.r > 1 || originalColor.g > 1 || originalColor.b > 1)
                {
                    result.Warnings.Add($"Emission color was HDR ({originalColor}), clamped to 0-1 range");
                }
            }
        }

        private static void ConvertRimLight(Material source, Material dest, ConversionResult result)
        {
            if (source.HasProperty(LilToonProps.UseRim) && source.GetFloat(LilToonProps.UseRim) > 0)
            {
                if (source.HasProperty(LilToonProps.RimColor))
                {
                    var rimColor = source.GetColor(LilToonProps.RimColor);
                    dest.SetColor(MToonProps.ParametricRimColorFactor, rimColor);
                }

                if (source.HasProperty(LilToonProps.RimFresnelPower))
                {
                    var fresnelPower = source.GetFloat(LilToonProps.RimFresnelPower);
                    dest.SetFloat(MToonProps.ParametricRimFresnelPowerFactor, fresnelPower);
                }

                dest.SetFloat(MToonProps.RimLightingMixFactor, 1.0f);
            }
        }

        private static void ConvertOutline(Material source, Material dest, ConversionResult result)
        {
            // lilToonのアウトライン設定を確認
            bool hasOutline = false;

            // シェーダー名にOutlineが含まれているか確認
            if (source.shader != null && source.shader.name.Contains("Outline"))
            {
                hasOutline = true;
            }

            // または_UseOutlineプロパティがあるか確認
            if (source.HasProperty(LilToonProps.UseOutline))
            {
                hasOutline = source.GetFloat(LilToonProps.UseOutline) > 0;
            }

            if (hasOutline)
            {
                // MToonではアウトラインは別シェーダーバリアントが必要なので警告
                result.Warnings.Add("Outline detected. MToon outline requires separate material pass setup.");

                if (source.HasProperty(LilToonProps.OutlineColor))
                {
                    dest.SetColor(MToonProps.OutlineColorFactor, source.GetColor(LilToonProps.OutlineColor));
                }

                if (source.HasProperty(LilToonProps.OutlineWidth))
                {
                    var width = source.GetFloat(LilToonProps.OutlineWidth);
                    dest.SetFloat(MToonProps.OutlineWidthFactor, width * 0.01f); // 単位調整
                }
            }
        }

        private static void ConvertAlphaMode(Material source, Material dest, ConversionResult result)
        {
            // lilToonの透過モードを判定
            var shaderName = source.shader?.name ?? "";
            int alphaMode = 0; // 0=Opaque, 1=Cutout, 2=Transparent

            if (shaderName.Contains("Cutout"))
            {
                alphaMode = 1;

                if (source.HasProperty(LilToonProps.Cutoff))
                {
                    dest.SetFloat(MToonProps.AlphaCutoff, source.GetFloat(LilToonProps.Cutoff));
                }
            }
            else if (shaderName.Contains("Transparent"))
            {
                alphaMode = 2;
            }

            dest.SetFloat(MToonProps.AlphaMode, alphaMode);
        }

        private static void ConvertRenderingSettings(Material source, Material dest, ConversionResult result)
        {
            // カリング
            if (source.HasProperty(LilToonProps.Cull))
            {
                var cull = (int)source.GetFloat(LilToonProps.Cull);
                // Cull: 0=Off(両面), 1=Front, 2=Back
                dest.SetFloat(MToonProps.DoubleSided, cull == 0 ? 1 : 0);
            }

            // レンダーキュー
            dest.renderQueue = source.renderQueue;
        }

        /// <summary>
        /// MToon10シェーダーを取得
        /// </summary>
        public static Shader GetMToon10Shader()
        {
            // VRM 1.0用MToon10シェーダー
            var shader = Shader.Find("VRM10/MToon10");
            if (shader != null) return shader;

            // フォールバック
            shader = Shader.Find("VRM/MToon");
            if (shader != null) return shader;

            return null;
        }

        /// <summary>
        /// MToon(VRM0.x用)シェーダーを取得
        /// </summary>
        public static Shader GetMToonShader()
        {
            return Shader.Find("VRM/MToon");
        }
    }
}
