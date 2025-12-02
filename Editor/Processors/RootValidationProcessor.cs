using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// Root検証Processor
    /// PrefabインスタンスRootの妥当性を検証する
    /// </summary>
    public class RootValidationProcessor : ExportProcessorBase
    {
        public override string Id => "root_validation";
        public override string DisplayName => "Root Validation";
        public override string Description => "Validates the root object for VRM export (Humanoid, Animator, etc.)";
        public override int Order => 0; // 最初に実行

        public override ProcessorResult Execute(ExportContext context)
        {
            var result = CreateResult();
            var root = context.ClonedRoot ?? context.SourceRoot;

            if (root == null)
            {
                result.AddError(Id, "Root object is null");
                return result;
            }

            // シーン上のオブジェクトか確認
            if (!root.scene.IsValid())
            {
                result.AddError(Id, "Root must be a scene object (not a prefab asset)");
                return result;
            }

            // 親がないことを確認（Rootであること）
            if (root.transform.parent != null)
            {
                result.AddWarning(Id, "Root object has a parent. Export will use this object as root.",
                    $"Parent: {root.transform.parent.name}");
            }

            // Animatorの確認
            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                result.AddError(Id, "No Animator component found on root object");
                return result;
            }

            // Humanoid Avatarの確認
            if (animator.avatar == null)
            {
                result.AddError(Id, "Animator has no Avatar assigned");
                return result;
            }

            if (!animator.avatar.isHuman)
            {
                result.AddError(Id, "Avatar is not Humanoid. VRM requires a Humanoid avatar.");
                return result;
            }

            result.AddInfo(Id, "Root validation passed",
                $"Animator: {animator.name}, Avatar: {animator.avatar.name}");

            // メッシュの確認
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);

            if (renderers.Length == 0 && meshFilters.Length == 0)
            {
                result.AddWarning(Id, "No mesh renderers found. VRM will have no visible geometry.");
            }
            else
            {
                result.AddInfo(Id, $"Found {renderers.Length} SkinnedMeshRenderers, {meshFilters.Length} MeshFilters");
            }

            // VRM 1.0用: Vrm10Instance確認
            if (context.VrmVersion == 1)
            {
                if (!root.TryGetComponent<UniVRM10.Vrm10Instance>(out _))
                {
                    result.AddWarning(Id,
                        "No Vrm10Instance component found. Meta and Expression data may be missing.",
                        "Consider adding Vrm10Instance component for full VRM 1.0 support.");
                }
            }

            // VRM 0.x用: VRMMeta確認
            if (context.VrmVersion == 0)
            {
                if (!root.TryGetComponent<VRM.VRMMeta>(out _))
                {
                    result.AddWarning(Id,
                        "No VRMMeta component found. Meta data will be empty.",
                        "Consider adding VRMMeta component for proper VRM 0.x metadata.");
                }
            }

            return result;
        }
    }
}
