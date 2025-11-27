using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// Humanoid検証Processor
    /// VRM出力に必要なHumanoidボーン構成を詳細に検証する
    /// </summary>
    public class HumanoidValidationProcessor : ExportProcessorBase
    {
        public override string Id => "humanoid_validation";
        public override string DisplayName => "Humanoid Validation";
        public override string Description => "Validates Humanoid bone configuration for VRM export";
        public override int Order => 5; // RootValidation(0)の後、他の処理の前

        /// <summary>
        /// VRMで必須のHumanoidボーン
        /// </summary>
        private static readonly HumanBodyBones[] RequiredBones = new[]
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot
        };

        /// <summary>
        /// VRMで推奨のHumanoidボーン
        /// </summary>
        private static readonly HumanBodyBones[] RecommendedBones = new[]
        {
            HumanBodyBones.Neck,
            HumanBodyBones.Chest,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightToes,
            HumanBodyBones.LeftEye,
            HumanBodyBones.RightEye,
            HumanBodyBones.Jaw
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

            var animator = root.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                result.AddError(Id, "Valid Humanoid Animator is required");
                return result;
            }

            // 必須ボーンのチェック
            var missingRequired = new List<HumanBodyBones>();
            var foundRequired = new List<HumanBodyBones>();

            foreach (var bone in RequiredBones)
            {
                var boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform == null)
                {
                    missingRequired.Add(bone);
                }
                else
                {
                    foundRequired.Add(bone);
                }
            }

            if (missingRequired.Count > 0)
            {
                result.AddError(Id,
                    $"Missing {missingRequired.Count} required bone(s)",
                    "Missing: " + string.Join(", ", missingRequired.Select(b => b.ToString())));
                return result;
            }

            result.AddInfo(Id,
                $"All {RequiredBones.Length} required bones found",
                string.Join(", ", foundRequired.Select(b => b.ToString())));

            // 推奨ボーンのチェック
            var missingRecommended = new List<HumanBodyBones>();
            var foundRecommended = new List<HumanBodyBones>();

            foreach (var bone in RecommendedBones)
            {
                var boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform == null)
                {
                    missingRecommended.Add(bone);
                }
                else
                {
                    foundRecommended.Add(bone);
                }
            }

            if (missingRecommended.Count > 0)
            {
                result.AddWarning(Id,
                    $"Missing {missingRecommended.Count} recommended bone(s)",
                    "Missing: " + string.Join(", ", missingRecommended.Select(b => b.ToString())));
            }
            else
            {
                result.AddInfo(Id, $"All {RecommendedBones.Length} recommended bones found");
            }

            // T-Poseチェック（簡易）
            CheckTPose(animator, result);

            // ボーン階層の整合性チェック
            CheckBoneHierarchy(animator, result);

            return result;
        }

        /// <summary>
        /// T-Poseの簡易チェック
        /// </summary>
        private void CheckTPose(Animator animator, ProcessorResult result)
        {
            var leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

            if (leftUpperArm == null || rightUpperArm == null) return;

            // 腕がほぼ水平かどうかをチェック（簡易判定）
            var leftDir = (animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position - leftUpperArm.position).normalized;
            var rightDir = (animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position - rightUpperArm.position).normalized;

            var leftAngle = Vector3.Angle(leftDir, Vector3.left);
            var rightAngle = Vector3.Angle(rightDir, Vector3.right);

            // 45度以上傾いていたら警告
            if (leftAngle > 45 || rightAngle > 45)
            {
                result.AddWarning(Id,
                    "Model may not be in T-Pose",
                    $"Left arm angle: {leftAngle:F1}°, Right arm angle: {rightAngle:F1}° from horizontal.\n" +
                    "VRM export may apply automatic T-Pose correction.");
            }
            else
            {
                result.AddInfo(Id, "T-Pose check passed");
            }
        }

        /// <summary>
        /// ボーン階層の整合性チェック
        /// </summary>
        private void CheckBoneHierarchy(Animator animator, ProcessorResult result)
        {
            var issues = new List<string>();

            // HipsがRootの子かどうか
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                var hipsDepth = GetDepth(hips, animator.transform);
                if (hipsDepth > 3)
                {
                    issues.Add($"Hips bone is {hipsDepth} levels deep from root (recommended: 1-2)");
                }
            }

            // SpineがHipsの子孫かどうか
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (hips != null && spine != null)
            {
                if (!IsDescendant(spine, hips))
                {
                    issues.Add("Spine is not a descendant of Hips");
                }
            }

            // HeadがSpineの子孫かどうか
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (spine != null && head != null)
            {
                if (!IsDescendant(head, spine))
                {
                    issues.Add("Head is not a descendant of Spine");
                }
            }

            if (issues.Count > 0)
            {
                result.AddWarning(Id,
                    $"Found {issues.Count} bone hierarchy issue(s)",
                    string.Join("\n", issues));
            }
            else
            {
                result.AddInfo(Id, "Bone hierarchy check passed");
            }
        }

        /// <summary>
        /// 階層の深さを取得
        /// </summary>
        private int GetDepth(Transform target, Transform root)
        {
            int depth = 0;
            var current = target;
            while (current != null && current != root)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }

        /// <summary>
        /// targetがparentの子孫かどうか
        /// </summary>
        private bool IsDescendant(Transform target, Transform parent)
        {
            var current = target;
            while (current != null)
            {
                if (current == parent) return true;
                current = current.parent;
            }
            return false;
        }
    }
}
