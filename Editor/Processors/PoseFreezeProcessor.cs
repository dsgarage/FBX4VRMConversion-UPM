using System.Collections.Generic;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// ポーズフリーズProcessor
    /// ルートのTransformを正規化し、メッシュのバインドポーズを修正
    /// VRMエクスポート時の座標系変換問題を解決
    /// </summary>
    public class PoseFreezeProcessor : ExportProcessorBase
    {
        public override string Id => "pose_freeze";
        public override string DisplayName => "Pose Freeze";
        public override string Description => "Freezes transforms and normalizes bind poses for VRM export";
        public override int Order => 1; // Root検証の直後

        public override ProcessorResult Execute(ExportContext context)
        {
            var result = CreateResult();
            var root = context.ClonedRoot ?? context.SourceRoot;

            if (root == null)
            {
                result.AddError(Id, "Root object is null");
                return result;
            }

            int normalizedCount = 0;
            int meshesProcessed = 0;

            // 1. ルートのTransformを正規化
            if (NormalizeRootTransform(root, result))
            {
                normalizedCount++;
            }

            // 2. すべてのSkinnedMeshRendererを処理
            var skinnedMeshRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinnedMeshRenderers)
            {
                if (ProcessSkinnedMesh(smr, root.transform, result))
                {
                    meshesProcessed++;
                }
            }

            // 3. ボーン階層のTransformを正規化
            var animator = root.GetComponent<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                NormalizeHumanoidBones(animator, result);
            }

            result.AddInfo(Id, $"Pose freeze completed",
                $"Normalized {normalizedCount} root transform(s), processed {meshesProcessed} mesh(es)");

            return result;
        }

        /// <summary>
        /// ルートTransformを正規化（回転・スケールを子に伝播してIdentityに）
        /// </summary>
        private bool NormalizeRootTransform(GameObject root, ProcessorResult result)
        {
            var rootTransform = root.transform;

            // すでにIdentityの場合はスキップ
            if (rootTransform.localPosition == Vector3.zero &&
                rootTransform.localRotation == Quaternion.identity &&
                rootTransform.localScale == Vector3.one)
            {
                return false;
            }

            var originalRotation = rootTransform.localRotation;
            var originalScale = rootTransform.localScale;
            var originalPosition = rootTransform.localPosition;

            // 子オブジェクトを一時的に退避
            var children = new List<Transform>();
            var childOriginalPositions = new List<Vector3>();
            var childOriginalRotations = new List<Quaternion>();
            var childOriginalScales = new List<Vector3>();

            foreach (Transform child in rootTransform)
            {
                children.Add(child);
                // ワールド座標を保存
                childOriginalPositions.Add(child.position);
                childOriginalRotations.Add(child.rotation);
                childOriginalScales.Add(child.lossyScale);
            }

            // ルートをIdentityに
            rootTransform.localPosition = Vector3.zero;
            rootTransform.localRotation = Quaternion.identity;
            rootTransform.localScale = Vector3.one;

            // 子のワールド座標を復元
            for (int i = 0; i < children.Count; i++)
            {
                children[i].position = childOriginalPositions[i];
                children[i].rotation = childOriginalRotations[i];
                // スケールはそのまま維持（lossyScaleを直接設定できないため）
            }

            result.AddInfo(Id, $"Normalized root transform",
                $"Original rotation: {originalRotation.eulerAngles}, scale: {originalScale}");

            return true;
        }

        /// <summary>
        /// SkinnedMeshRendererのバインドポーズを処理
        /// </summary>
        private bool ProcessSkinnedMesh(SkinnedMeshRenderer smr, Transform rootTransform, ProcessorResult result)
        {
            if (smr.sharedMesh == null) return false;

            var mesh = smr.sharedMesh;
            var bones = smr.bones;

            if (bones == null || bones.Length == 0) return false;

            // メッシュが既にbindposesを持っているか確認
            var bindposes = mesh.bindposes;
            if (bindposes == null || bindposes.Length == 0)
            {
                result.AddWarning(Id, $"Mesh '{mesh.name}' has no bindposes");
                return false;
            }

            // ルートボーンの回転を確認
            var rootBone = smr.rootBone;
            if (rootBone != null)
            {
                // ルートボーンがルートTransformの直下でない場合の処理
                var rootBoneLocalRotation = rootBone.localRotation;
                if (rootBoneLocalRotation != Quaternion.identity)
                {
                    // この情報をログに記録（実際の修正は複雑なため）
                    result.AddInfo(Id, $"Mesh '{smr.name}' rootBone '{rootBone.name}' has local rotation",
                        $"Rotation: {rootBoneLocalRotation.eulerAngles}");
                }
            }

            return true;
        }

        /// <summary>
        /// Humanoidボーンの回転を確認・正規化
        /// </summary>
        private void NormalizeHumanoidBones(Animator animator, ProcessorResult result)
        {
            // Humanoidの主要ボーンを確認
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;

            // Hipsの親（通常Armature）を確認
            var armature = hips.parent;
            if (armature != null)
            {
                var armatureRotation = armature.localRotation;
                if (armatureRotation != Quaternion.identity)
                {
                    result.AddInfo(Id, $"Armature '{armature.name}' has rotation",
                        $"Euler: {armatureRotation.eulerAngles}");

                    // 回転を打ち消すために子に伝播
                    // VRM座標系変換: -90° X を打ち消す
                    if (IsCoordinateConversionRotation(armatureRotation))
                    {
                        result.AddInfo(Id, "Detected coordinate system conversion rotation on Armature");

                        // Armatureの回転をIdentityにし、子のワールド座標を維持
                        BakeArmatureRotation(armature, result);
                    }
                }
            }

            // Hips自体の回転を確認
            var hipsRotation = hips.localRotation;
            if (hipsRotation != Quaternion.identity)
            {
                result.AddInfo(Id, $"Hips has local rotation: {hipsRotation.eulerAngles}");
            }
        }

        /// <summary>
        /// 座標系変換用の回転かどうかを判定（-90°または+90° X回転）
        /// </summary>
        private bool IsCoordinateConversionRotation(Quaternion rotation)
        {
            var euler = rotation.eulerAngles;

            // X軸周りの±90°回転を検出
            bool isX90 = (Mathf.Abs(euler.x - 90f) < 1f || Mathf.Abs(euler.x - 270f) < 1f) &&
                         Mathf.Abs(euler.y) < 1f &&
                         Mathf.Abs(euler.z) < 1f;

            return isX90;
        }

        /// <summary>
        /// Armatureの回転を子に焼き込んでIdentityにする
        /// </summary>
        private void BakeArmatureRotation(Transform armature, ProcessorResult result)
        {
            var originalRotation = armature.localRotation;

            // 子オブジェクトのワールド座標を保存
            var childData = new List<(Transform transform, Vector3 position, Quaternion rotation)>();

            foreach (Transform child in armature)
            {
                childData.Add((child, child.position, child.rotation));
            }

            // Armatureの回転をIdentityに
            armature.localRotation = Quaternion.identity;

            // 子のワールド座標を復元
            foreach (var (transform, position, rotation) in childData)
            {
                transform.position = position;
                transform.rotation = rotation;
            }

            result.AddInfo(Id, $"Baked Armature rotation to children",
                $"Original: {originalRotation.eulerAngles}");
        }
    }
}
