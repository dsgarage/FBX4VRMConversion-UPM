using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// Expression/BlendShape整理Processor
    /// FBXのBlendShapeをVRM Expressionにマッピングする
    /// </summary>
    public class ExpressionsSetupProcessor : ExportProcessorBase
    {
        public override string Id => "expressions_setup";
        public override string DisplayName => "Expressions Setup";
        public override string Description => "Maps BlendShapes to VRM Expressions";
        public override int Order => 40; // GltfValueClamp(30)の後

        /// <summary>
        /// VRM標準Expression名
        /// </summary>
        public static class VrmExpressions
        {
            // 感情系
            public const string Happy = "happy";
            public const string Angry = "angry";
            public const string Sad = "sad";
            public const string Relaxed = "relaxed";
            public const string Surprised = "surprised";

            // リップシンク
            public const string Aa = "aa";
            public const string Ih = "ih";
            public const string Ou = "ou";
            public const string Ee = "ee";
            public const string Oh = "oh";

            // まばたき
            public const string Blink = "blink";
            public const string BlinkLeft = "blinkLeft";
            public const string BlinkRight = "blinkRight";

            // 視線
            public const string LookUp = "lookUp";
            public const string LookDown = "lookDown";
            public const string LookLeft = "lookLeft";
            public const string LookRight = "lookRight";

            // その他
            public const string Neutral = "neutral";
        }

        /// <summary>
        /// BlendShape名からVRM Expression名へのマッピング
        /// 複数のパターンに対応
        /// </summary>
        private static readonly Dictionary<string, string[]> ExpressionMappings = new Dictionary<string, string[]>
        {
            // 感情系
            { VrmExpressions.Happy, new[] { "happy", "joy", "smile", "笑顔", "笑い", "にっこり" } },
            { VrmExpressions.Angry, new[] { "angry", "anger", "怒り", "怒る" } },
            { VrmExpressions.Sad, new[] { "sad", "sorrow", "悲しみ", "悲しい" } },
            { VrmExpressions.Relaxed, new[] { "relaxed", "calm", "リラックス" } },
            { VrmExpressions.Surprised, new[] { "surprised", "surprise", "驚き", "びっくり" } },

            // リップシンク (様々な命名規則に対応)
            { VrmExpressions.Aa, new[] { "aa", "a", "vrc.v_aa", "mouth_a", "あ" } },
            { VrmExpressions.Ih, new[] { "ih", "i", "vrc.v_ih", "mouth_i", "い" } },
            { VrmExpressions.Ou, new[] { "ou", "u", "vrc.v_ou", "mouth_u", "う" } },
            { VrmExpressions.Ee, new[] { "ee", "e", "vrc.v_ee", "mouth_e", "え" } },
            { VrmExpressions.Oh, new[] { "oh", "o", "vrc.v_oh", "mouth_o", "お" } },

            // まばたき
            { VrmExpressions.Blink, new[] { "blink", "eye_close", "まばたき", "目閉じ", "目を閉じる" } },
            { VrmExpressions.BlinkLeft, new[] { "blink_l", "blink_left", "wink_l", "左ウィンク" } },
            { VrmExpressions.BlinkRight, new[] { "blink_r", "blink_right", "wink_r", "右ウィンク" } },

            // 視線
            { VrmExpressions.LookUp, new[] { "lookup", "look_up", "eye_up", "上を見る" } },
            { VrmExpressions.LookDown, new[] { "lookdown", "look_down", "eye_down", "下を見る" } },
            { VrmExpressions.LookLeft, new[] { "lookleft", "look_left", "eye_left", "左を見る" } },
            { VrmExpressions.LookRight, new[] { "lookright", "look_right", "eye_right", "右を見る" } },
        };

        /// <summary>
        /// 検出されたBlendShape情報
        /// </summary>
        public class BlendShapeInfo
        {
            public SkinnedMeshRenderer Renderer { get; set; }
            public int Index { get; set; }
            public string Name { get; set; }
            public string MappedExpression { get; set; } // マッピング先のVRM Expression名（nullの場合マッピングなし）
        }

        public override ProcessorResult Execute(ExportContext context)
        {
            var result = CreateResult();
            var root = context.ClonedRoot ?? context.SourceRoot;

            if (root == null)
            {
                result.AddWarning(Id, "Root object is null, skipping expression setup");
                return result;
            }

            // BlendShapeを持つSkinnedMeshRendererを検索
            var skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var allBlendShapes = new List<BlendShapeInfo>();
            var mappedExpressions = new Dictionary<string, BlendShapeInfo>();

            foreach (var smr in skinnedMeshes)
            {
                if (smr.sharedMesh == null) continue;

                var mesh = smr.sharedMesh;
                var blendShapeCount = mesh.blendShapeCount;

                for (int i = 0; i < blendShapeCount; i++)
                {
                    var shapeName = mesh.GetBlendShapeName(i);
                    var info = new BlendShapeInfo
                    {
                        Renderer = smr,
                        Index = i,
                        Name = shapeName,
                        MappedExpression = FindExpressionMapping(shapeName)
                    };

                    allBlendShapes.Add(info);

                    // マッピングがある場合は記録（重複チェック）
                    if (info.MappedExpression != null)
                    {
                        if (!mappedExpressions.ContainsKey(info.MappedExpression))
                        {
                            mappedExpressions[info.MappedExpression] = info;
                        }
                        else
                        {
                            // 重複の場合は警告（最初のものを優先）
                            result.AddWarning(Id,
                                $"Duplicate mapping for '{info.MappedExpression}'",
                                $"'{info.Name}' ignored, using '{mappedExpressions[info.MappedExpression].Name}'");
                        }
                    }
                }
            }

            // 結果をSharedDataに保存
            context.SharedData["BlendShapes"] = allBlendShapes;
            context.SharedData["MappedExpressions"] = mappedExpressions;

            // 統計情報
            result.AddInfo(Id,
                $"Found {allBlendShapes.Count} BlendShape(s) in {skinnedMeshes.Length} mesh(es)");

            if (mappedExpressions.Count > 0)
            {
                var mappingDetails = mappedExpressions
                    .Select(kvp => $"  {kvp.Key}: {kvp.Value.Name}")
                    .ToList();

                result.AddInfo(Id,
                    $"Mapped {mappedExpressions.Count} expression(s)",
                    string.Join("\n", mappingDetails));
            }

            // 未マッピングのBlendShapeをリスト
            var unmapped = allBlendShapes.Where(b => b.MappedExpression == null).ToList();
            if (unmapped.Count > 0)
            {
                // 主要なものだけ表示（最大10件）
                var unmappedNames = unmapped.Take(10).Select(b => b.Name).ToList();
                var moreCount = unmapped.Count - unmappedNames.Count;

                var detail = string.Join(", ", unmappedNames);
                if (moreCount > 0)
                {
                    detail += $" (+{moreCount} more)";
                }

                result.AddInfo(Id,
                    $"{unmapped.Count} BlendShape(s) not mapped to VRM expressions",
                    detail);
            }

            // 推奨Expressionが欠けている場合は警告
            CheckRecommendedExpressions(mappedExpressions, result);

            return result;
        }

        /// <summary>
        /// BlendShape名からVRM Expression名を検索
        /// </summary>
        private string FindExpressionMapping(string blendShapeName)
        {
            var lowerName = blendShapeName.ToLowerInvariant();

            foreach (var mapping in ExpressionMappings)
            {
                foreach (var pattern in mapping.Value)
                {
                    if (lowerName == pattern.ToLowerInvariant() ||
                        lowerName.Contains(pattern.ToLowerInvariant()))
                    {
                        return mapping.Key;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 推奨Expressionの存在チェック
        /// </summary>
        private void CheckRecommendedExpressions(Dictionary<string, BlendShapeInfo> mapped, ProcessorResult result)
        {
            // VRMで推奨されるExpression
            var recommended = new[]
            {
                VrmExpressions.Happy,
                VrmExpressions.Angry,
                VrmExpressions.Sad,
                VrmExpressions.Blink,
                VrmExpressions.Aa,
                VrmExpressions.Ih,
                VrmExpressions.Ou,
                VrmExpressions.Ee,
                VrmExpressions.Oh
            };

            var missing = recommended.Where(r => !mapped.ContainsKey(r)).ToList();

            if (missing.Count > 0)
            {
                result.AddWarning(Id,
                    $"Missing {missing.Count} recommended expression(s)",
                    "Missing: " + string.Join(", ", missing));
            }
            else
            {
                result.AddInfo(Id, "All recommended expressions found");
            }
        }
    }
}
