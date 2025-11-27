using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// SpringBone変換Processor
    /// VRChat PhysBone/DynamicBoneをVRM SpringBoneに変換する
    /// </summary>
    public class SpringBoneConvertProcessor : ExportProcessorBase
    {
        public override string Id => "springbone_convert";
        public override string DisplayName => "SpringBone Convert";
        public override string Description => "Converts PhysBone/DynamicBone to VRM SpringBone";
        public override int Order => 50; // ExpressionsSetup(40)の後

        /// <summary>
        /// 検出されたダイナミクスボーン情報
        /// </summary>
        public class DynamicsBoneInfo
        {
            public string SourceType { get; set; } // "PhysBone", "DynamicBone", "VRMSpringBone"
            public Transform RootBone { get; set; }
            public List<Transform> AffectedBones { get; set; } = new List<Transform>();
            public Component SourceComponent { get; set; }

            // パラメータ（正規化済み）
            public float Stiffness { get; set; } = 0.5f;
            public float Gravity { get; set; } = 0f;
            public Vector3 GravityDirection { get; set; } = Vector3.down;
            public float DragForce { get; set; } = 0.4f;
            public float HitRadius { get; set; } = 0f;
        }

        /// <summary>
        /// VRChat PhysBoneの型名
        /// </summary>
        private const string PhysBoneTypeName = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone";

        /// <summary>
        /// DynamicBoneの型名
        /// </summary>
        private const string DynamicBoneTypeName = "DynamicBone";

        public override ProcessorResult Execute(ExportContext context)
        {
            var result = CreateResult();
            var root = context.ClonedRoot ?? context.SourceRoot;

            if (root == null)
            {
                result.AddWarning(Id, "Root object is null, skipping SpringBone conversion");
                return result;
            }

            var detectedBones = new List<DynamicsBoneInfo>();

            // 既存のVRM SpringBoneを検出
            DetectExistingVrmSpringBones(root, detectedBones, result);

            // PhysBoneを検出・変換
            DetectAndConvertPhysBones(root, detectedBones, result);

            // DynamicBoneを検出・変換
            DetectAndConvertDynamicBones(root, detectedBones, result);

            // 結果をSharedDataに保存
            context.SharedData["DynamicsBones"] = detectedBones;

            // 統計情報
            if (detectedBones.Count > 0)
            {
                var byType = detectedBones.GroupBy(b => b.SourceType)
                    .Select(g => $"{g.Key}: {g.Count()}")
                    .ToList();

                result.AddInfo(Id,
                    $"Detected {detectedBones.Count} dynamics bone chain(s)",
                    string.Join(", ", byType));

                // 変換されたボーンの詳細
                var details = detectedBones
                    .Where(b => b.RootBone != null)
                    .Select(b => $"  {b.RootBone.name} ({b.SourceType}, {b.AffectedBones.Count} bones)")
                    .ToList();

                if (details.Count > 0)
                {
                    result.AddInfo(Id,
                        "Bone chains:",
                        string.Join("\n", details.Take(10)) +
                        (details.Count > 10 ? $"\n  (+{details.Count - 10} more)" : ""));
                }
            }
            else
            {
                result.AddInfo(Id, "No dynamics bones detected");
            }

            return result;
        }

        /// <summary>
        /// 既存のVRM SpringBoneを検出
        /// </summary>
        private void DetectExistingVrmSpringBones(GameObject root, List<DynamicsBoneInfo> detectedBones, ProcessorResult result)
        {
            // VRM 1.0 SpringBone
            var vrm10Springs = root.GetComponentsInChildren<UniVRM10.Vrm10Instance>(true);
            foreach (var vrm10 in vrm10Springs)
            {
                if (vrm10.SpringBone?.Springs != null)
                {
                    foreach (var spring in vrm10.SpringBone.Springs)
                    {
                        if (spring.Joints == null || spring.Joints.Count == 0) continue;

                        var info = new DynamicsBoneInfo
                        {
                            SourceType = "VRM10SpringBone",
                            RootBone = spring.Joints[0]?.transform,
                            SourceComponent = vrm10
                        };

                        foreach (var joint in spring.Joints)
                        {
                            if (joint != null)
                            {
                                info.AffectedBones.Add(joint.transform);
                            }
                        }

                        detectedBones.Add(info);
                    }
                }
            }

            // VRM 0.x SpringBone
            var vrm0Springs = root.GetComponentsInChildren<VRM.VRMSpringBone>(true);
            foreach (var spring in vrm0Springs)
            {
                if (spring.RootBones == null) continue;

                foreach (var rootBone in spring.RootBones)
                {
                    if (rootBone == null) continue;

                    var info = new DynamicsBoneInfo
                    {
                        SourceType = "VRM0SpringBone",
                        RootBone = rootBone,
                        SourceComponent = spring,
                        Stiffness = spring.m_stiffnessForce,
                        Gravity = spring.m_gravityPower,
                        GravityDirection = spring.m_gravityDir,
                        DragForce = spring.m_dragForce,
                        HitRadius = spring.m_hitRadius
                    };

                    // 子ボーンを収集
                    CollectChildBones(rootBone, info.AffectedBones);
                    detectedBones.Add(info);
                }
            }

            if (vrm10Springs.Length > 0 || vrm0Springs.Length > 0)
            {
                result.AddInfo(Id, "Existing VRM SpringBone detected, will be preserved");
            }
        }

        /// <summary>
        /// PhysBoneを検出・変換
        /// </summary>
        private void DetectAndConvertPhysBones(GameObject root, List<DynamicsBoneInfo> detectedBones, ProcessorResult result)
        {
            // PhysBone型をリフレクションで取得（SDKがない環境でもエラーにならないように）
            var physBoneType = GetTypeByName(PhysBoneTypeName);
            if (physBoneType == null)
            {
                // PhysBoneがインストールされていない
                return;
            }

            var physBones = root.GetComponentsInChildren(physBoneType, true);
            if (physBones.Length == 0) return;

            result.AddInfo(Id, $"Found {physBones.Length} VRChat PhysBone component(s)");

            foreach (var pb in physBones)
            {
                try
                {
                    var info = ConvertPhysBone(pb, physBoneType);
                    if (info != null)
                    {
                        detectedBones.Add(info);
                    }
                }
                catch (Exception e)
                {
                    result.AddWarning(Id,
                        $"Failed to convert PhysBone on '{pb.name}'",
                        e.Message);
                }
            }

            result.AddWarning(Id,
                $"PhysBone conversion is approximate",
                "VRChat PhysBone and VRM SpringBone have different physics models. " +
                "Manual adjustment may be required after export.");
        }

        /// <summary>
        /// PhysBoneをDynamicsBoneInfoに変換
        /// </summary>
        private DynamicsBoneInfo ConvertPhysBone(Component physBone, Type physBoneType)
        {
            var info = new DynamicsBoneInfo
            {
                SourceType = "PhysBone",
                SourceComponent = physBone
            };

            // rootTransformを取得
            var rootTransformField = physBoneType.GetField("rootTransform",
                BindingFlags.Public | BindingFlags.Instance);
            if (rootTransformField != null)
            {
                info.RootBone = rootTransformField.GetValue(physBone) as Transform;
            }

            // rootTransformがnullの場合はコンポーネントのTransformを使用
            if (info.RootBone == null)
            {
                info.RootBone = (physBone as MonoBehaviour)?.transform;
            }

            if (info.RootBone == null) return null;

            // パラメータを取得・変換
            // PhysBone: pull (0-1) → VRM: stiffness
            var pullField = physBoneType.GetField("pull", BindingFlags.Public | BindingFlags.Instance);
            if (pullField != null)
            {
                info.Stiffness = Convert.ToSingle(pullField.GetValue(physBone));
            }

            // PhysBone: gravity → VRM: gravity
            var gravityField = physBoneType.GetField("gravity", BindingFlags.Public | BindingFlags.Instance);
            if (gravityField != null)
            {
                info.Gravity = Mathf.Abs(Convert.ToSingle(gravityField.GetValue(physBone)));
            }

            // PhysBone: gravityFalloff direction
            info.GravityDirection = Vector3.down;

            // PhysBone: spring (0-1) → VRM: dragForce (inverse relationship)
            var springField = physBoneType.GetField("spring", BindingFlags.Public | BindingFlags.Instance);
            if (springField != null)
            {
                var springValue = Convert.ToSingle(springField.GetValue(physBone));
                info.DragForce = 1f - springValue; // 逆変換
            }

            // PhysBone: radius → VRM: hitRadius
            var radiusField = physBoneType.GetField("radius", BindingFlags.Public | BindingFlags.Instance);
            if (radiusField != null)
            {
                info.HitRadius = Convert.ToSingle(radiusField.GetValue(physBone));
            }

            // 子ボーンを収集
            CollectChildBones(info.RootBone, info.AffectedBones);

            return info;
        }

        /// <summary>
        /// DynamicBoneを検出・変換
        /// </summary>
        private void DetectAndConvertDynamicBones(GameObject root, List<DynamicsBoneInfo> detectedBones, ProcessorResult result)
        {
            var dynamicBoneType = GetTypeByName(DynamicBoneTypeName);
            if (dynamicBoneType == null)
            {
                return;
            }

            var dynamicBones = root.GetComponentsInChildren(dynamicBoneType, true);
            if (dynamicBones.Length == 0) return;

            result.AddInfo(Id, $"Found {dynamicBones.Length} DynamicBone component(s)");

            foreach (var db in dynamicBones)
            {
                try
                {
                    var info = ConvertDynamicBone(db, dynamicBoneType);
                    if (info != null)
                    {
                        detectedBones.Add(info);
                    }
                }
                catch (Exception e)
                {
                    result.AddWarning(Id,
                        $"Failed to convert DynamicBone on '{db.name}'",
                        e.Message);
                }
            }

            result.AddWarning(Id,
                $"DynamicBone conversion is approximate",
                "DynamicBone and VRM SpringBone have different physics models. " +
                "Manual adjustment may be required after export.");
        }

        /// <summary>
        /// DynamicBoneをDynamicsBoneInfoに変換
        /// </summary>
        private DynamicsBoneInfo ConvertDynamicBone(Component dynamicBone, Type dynamicBoneType)
        {
            var info = new DynamicsBoneInfo
            {
                SourceType = "DynamicBone",
                SourceComponent = dynamicBone
            };

            // m_Rootを取得
            var rootField = dynamicBoneType.GetField("m_Root",
                BindingFlags.Public | BindingFlags.Instance);
            if (rootField != null)
            {
                info.RootBone = rootField.GetValue(dynamicBone) as Transform;
            }

            if (info.RootBone == null) return null;

            // パラメータを取得・変換
            // DynamicBone: m_Stiffness → VRM: stiffness
            var stiffnessField = dynamicBoneType.GetField("m_Stiffness",
                BindingFlags.Public | BindingFlags.Instance);
            if (stiffnessField != null)
            {
                info.Stiffness = Convert.ToSingle(stiffnessField.GetValue(dynamicBone));
            }

            // DynamicBone: m_Gravity → VRM: gravity + direction
            var gravityField = dynamicBoneType.GetField("m_Gravity",
                BindingFlags.Public | BindingFlags.Instance);
            if (gravityField != null)
            {
                var gravity = (Vector3)gravityField.GetValue(dynamicBone);
                info.Gravity = gravity.magnitude;
                info.GravityDirection = gravity.normalized;
            }

            // DynamicBone: m_Damping → VRM: dragForce
            var dampingField = dynamicBoneType.GetField("m_Damping",
                BindingFlags.Public | BindingFlags.Instance);
            if (dampingField != null)
            {
                info.DragForce = Convert.ToSingle(dampingField.GetValue(dynamicBone));
            }

            // DynamicBone: m_Radius → VRM: hitRadius
            var radiusField = dynamicBoneType.GetField("m_Radius",
                BindingFlags.Public | BindingFlags.Instance);
            if (radiusField != null)
            {
                info.HitRadius = Convert.ToSingle(radiusField.GetValue(dynamicBone));
            }

            // 子ボーンを収集
            CollectChildBones(info.RootBone, info.AffectedBones);

            return info;
        }

        /// <summary>
        /// 子ボーンを再帰的に収集
        /// </summary>
        private void CollectChildBones(Transform root, List<Transform> bones)
        {
            bones.Add(root);
            foreach (Transform child in root)
            {
                CollectChildBones(child, bones);
            }
        }

        /// <summary>
        /// 型名から型を取得（アセンブリをまたいで検索）
        /// </summary>
        private Type GetTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }
    }
}
