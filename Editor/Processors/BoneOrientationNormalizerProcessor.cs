using System.Collections.Generic;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// VRM 1.0専用: ボーン回転正規化Processor
    /// Blenderからエクスポートされたモデルの肩等のボーン回転を正規化
    /// VRM 0.xでは実行されない（スキップ）
    /// </summary>
    public class BoneOrientationNormalizerProcessor : ExportProcessorBase
    {
        public override string Id => "bone_orientation_normalizer";
        public override string DisplayName => "Bone Orientation Normalizer (VRM 1.0)";
        public override string Description => "Normalizes bone orientations for VRM 1.0 export (Blender compatibility)";
        public override int Order => 2; // PoseFreezeの後、HumanoidValidationの前

        // 正規化対象のHumanBodyBones（肩、腕など回転が問題になりやすいボーン）
        private static readonly HumanBodyBones[] TargetBones = new[]
        {
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
        };

        // 回転が問題とみなされる閾値（度数）
        private const float RotationThreshold = 5.0f;

        public override ProcessorResult Execute(ExportContext context)
        {
            var result = CreateResult();

            // VRM 0.xの場合はスキップ（影響を与えない）
            if (context.VrmVersion != 1)
            {
                result.AddInfo(Id, "Skipped: VRM 0.x export does not require bone orientation normalization");
                return result;
            }

            var root = context.ClonedRoot ?? context.SourceRoot;
            if (root == null)
            {
                result.AddError(Id, "Root object is null");
                return result;
            }

            var animator = root.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                result.AddWarning(Id, "No humanoid Animator found, skipping bone normalization");
                return result;
            }

            int normalizedCount = 0;

            // 対象ボーンをチェック・正規化
            foreach (var boneType in TargetBones)
            {
                var bone = animator.GetBoneTransform(boneType);
                if (bone == null) continue;

                var localRotation = bone.localRotation;
                var euler = localRotation.eulerAngles;

                // 回転を-180～180の範囲に正規化
                euler.x = NormalizeAngle(euler.x);
                euler.y = NormalizeAngle(euler.y);
                euler.z = NormalizeAngle(euler.z);

                // 閾値以上の回転があるかチェック
                if (HasSignificantRotation(euler))
                {
                    result.AddInfo(Id, $"Normalizing bone '{boneType}' ({bone.name})",
                        $"Original rotation: ({euler.x:F1}°, {euler.y:F1}°, {euler.z:F1}°)");

                    // ボーンの回転を子に焼き込む
                    NormalizeBoneRotation(bone, result);
                    normalizedCount++;
                }
            }

            // SkinnedMeshRendererのバインドポーズを更新
            if (normalizedCount > 0)
            {
                UpdateBindPoses(root, result);
            }

            result.AddInfo(Id, $"VRM 1.0 bone normalization completed",
                $"Normalized {normalizedCount} bone(s)");

            return result;
        }

        /// <summary>
        /// 角度を-180～180の範囲に正規化
        /// </summary>
        private float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// 閾値以上の回転があるか判定
        /// </summary>
        private bool HasSignificantRotation(Vector3 euler)
        {
            return Mathf.Abs(euler.x) > RotationThreshold ||
                   Mathf.Abs(euler.y) > RotationThreshold ||
                   Mathf.Abs(euler.z) > RotationThreshold;
        }

        /// <summary>
        /// ボーンの回転を子に焼き込んでIdentityにする
        /// </summary>
        private void NormalizeBoneRotation(Transform bone, ProcessorResult result)
        {
            var originalRotation = bone.localRotation;

            // 子オブジェクトのワールド座標を保存
            var childData = new List<(Transform transform, Vector3 position, Quaternion rotation)>();

            foreach (Transform child in bone)
            {
                childData.Add((child, child.position, child.rotation));
            }

            // ボーンの回転をIdentityに
            bone.localRotation = Quaternion.identity;

            // 子のワールド座標を復元
            foreach (var (transform, position, rotation) in childData)
            {
                transform.position = position;
                transform.rotation = rotation;
            }
        }

        /// <summary>
        /// すべてのSkinnedMeshRendererのバインドポーズを更新
        /// </summary>
        private void UpdateBindPoses(GameObject root, ProcessorResult result)
        {
            var skinnedMeshRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int updatedCount = 0;

            foreach (var smr in skinnedMeshRenderers)
            {
                if (UpdateSkinnedMeshBindPoses(smr))
                {
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                result.AddInfo(Id, $"Updated bind poses for {updatedCount} SkinnedMeshRenderer(s)");
            }
        }

        /// <summary>
        /// SkinnedMeshRendererのバインドポーズを現在のボーン位置から再計算
        /// </summary>
        private bool UpdateSkinnedMeshBindPoses(SkinnedMeshRenderer smr)
        {
            if (smr.sharedMesh == null) return false;

            var bones = smr.bones;
            if (bones == null || bones.Length == 0) return false;

            // 新しいバインドポーズを計算
            var newBindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                {
                    // バインドポーズ = ワールド座標からボーンローカルへの変換行列
                    newBindposes[i] = bones[i].worldToLocalMatrix * smr.transform.localToWorldMatrix;
                }
                else
                {
                    newBindposes[i] = Matrix4x4.identity;
                }
            }

            // メッシュをコピーして更新（元のメッシュを変更しないため）
            var newMesh = Object.Instantiate(smr.sharedMesh);
            newMesh.name = smr.sharedMesh.name + "_normalized";
            newMesh.bindposes = newBindposes;
            smr.sharedMesh = newMesh;

            return true;
        }
    }
}
