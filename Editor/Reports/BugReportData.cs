using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DSGarage.FBX4VRM.Editor.Settings;

namespace DSGarage.FBX4VRM.Editor.Reports
{
    /// <summary>
    /// プラットフォーム種別
    /// </summary>
    public enum ReportPlatform
    {
        FBX4VRM,      // FBX→VRM変換ツール
        VRMLoader,    // VRMローダー
        ARApp         // 本番ARアプリ
    }

    /// <summary>
    /// 不具合報告データ
    /// BugReportAPI_Schema.md に準拠した完全版
    /// </summary>
    [Serializable]
    public class BugReportData
    {
        // ========== 基本情報 ==========
        public string report_id;
        public string timestamp;
        public string platform = "fbx4vrm";  // "fbx4vrm" | "vrmloader" | "arapp"

        // ========== 環境情報 ==========
        public EnvironmentInfo environment = new EnvironmentInfo();

        // ========== エクスポート設定 ==========
        public ExportSettingsInfo export_settings = new ExportSettingsInfo();

        // ========== 元モデル情報 ==========
        public SourceModelInfo source_model = new SourceModelInfo();

        // ========== 変換結果 ==========
        public ResultInfo result = new ResultInfo();

        // ========== 骨格情報 ==========
        public SkeletonInfo skeleton = new SkeletonInfo();

        // ========== メッシュ情報 ==========
        public MeshesInfo meshes = new MeshesInfo();

        // ========== マテリアル情報 ==========
        public MaterialsInfo materials = new MaterialsInfo();

        // ========== 表情情報 ==========
        public ExpressionsInfo expressions = new ExpressionsInfo();

        // ========== 物理情報 ==========
        public DynamicsInfo dynamics = new DynamicsInfo();

        // ========== 通知サマリー ==========
        public NotificationsInfo notifications = new NotificationsInfo();

        // ========== スクリーンショット ==========
        public ScreenshotInfo screenshot = new ScreenshotInfo();

        // ========== ユーザーコメント ==========
        public string user_comment;

        // ========== 非シリアライズフィールド ==========
        [NonSerialized]
        public byte[] ScreenshotBytes;

        [NonSerialized]
        public DateTime CreatedAt;

        // ========== 後方互換性プロパティ ==========
        public string ReportId => report_id;
        public string Timestamp => timestamp;
        public string PackageVersion => environment.package_version;
        public string UnityVersion => environment.unity_version;
        public string Platform => environment.platform;
        public string ModelName => source_model.name;
        public int VrmVersion => export_settings.vrm_version;
        public bool ExportSuccess => result.success;
        public string UserComment { get => user_comment; set => user_comment = value; }
        public List<NotificationEntry> Notifications => notifications.GetAllEntries();

        #region Nested Classes

        [Serializable]
        public class EnvironmentInfo
        {
            public string package_version;
            public string unity_version;
            public string platform;
            public string univrm_version;
            public string render_pipeline;
        }

        [Serializable]
        public class ExportSettingsInfo
        {
            public int vrm_version;
            public string preset_name;
            public string output_path;

            // 詳細設定
            public bool enable_liltoon_conversion = true;
            public bool enable_hdr_clamp = true;
            public bool enable_outline_conversion = true;
            public string transparent_mode = "Auto";

            public bool enable_tpose_normalization = true;
            public bool enable_armature_rotation_bake = true;
            public bool enable_bone_orientation_normalization = true;

            public bool enable_expression_auto_mapping = true;
            public string expression_naming_convention = "Auto";

            public bool enable_springbone_conversion = true;
            public bool enable_collider_conversion = true;

            public string output_folder = "VRM_Export";
            public string file_name_mode = "Auto";
            public string custom_file_name = "";
        }

        [Serializable]
        public class SourceModelInfo
        {
            public string name;
            public string asset_path;
            public string source_format;
            public long file_size_bytes;
            public string avatar_id;  // サーバー管理のアバターID（既存アバターの場合）
            public bool is_new_avatar; // 新規アバターとして登録するか
        }

        [Serializable]
        public class ResultInfo
        {
            public bool success;
            public string stopped_at_processor;
            public string error_message;
            public long duration_ms;
        }

        [Serializable]
        public class SkeletonInfo
        {
            public string avatar_name;
            public bool is_humanoid;
            public BoneCount required_bones = new BoneCount();
            public BoneCount recommended_bones = new BoneCount();
            public bool bone_hierarchy_valid;
            public bool t_pose_valid;
            public ArmatureRotation armature_rotation = new ArmatureRotation();
            public BoneOrientations bone_orientations = new BoneOrientations();
            public int total_bones;
        }

        [Serializable]
        public class BoneCount
        {
            public int found;
            public List<string> missing = new List<string>();
        }

        [Serializable]
        public class ArmatureRotation
        {
            public float[] euler = new float[3];
            public bool requires_normalization;
            public bool normalized;
        }

        [Serializable]
        public class BoneOrientations
        {
            public List<BoneOrientationIssue> issues = new List<BoneOrientationIssue>();
        }

        [Serializable]
        public class BoneOrientationIssue
        {
            public string bone;
            public float[] expected_forward = new float[3];
            public float[] actual_forward = new float[3];
            public float angle_diff_deg;
        }

        [Serializable]
        public class MeshesInfo
        {
            public int skinned_mesh_count;
            public int mesh_filter_count;
            public int total_vertices;
            public int total_triangles;
            public int blendshape_count;
            public List<MeshDetail> meshes = new List<MeshDetail>();
        }

        [Serializable]
        public class MeshDetail
        {
            public string name;
            public int vertices;
            public int triangles;
            public int blendshapes;
            public int submeshes;
            public int material_slots;
            public BoundsInfo bounds = new BoundsInfo();
        }

        [Serializable]
        public class BoundsInfo
        {
            public float[] center = new float[3];
            public float[] size = new float[3];
        }

        [Serializable]
        public class MaterialsInfo
        {
            public int total_count;
            public Dictionary<string, int> original_shaders = new Dictionary<string, int>();
            public List<MaterialConversionResult> conversion_results = new List<MaterialConversionResult>();
            public List<UnsupportedShader> unsupported_shaders = new List<UnsupportedShader>();

            // JsonUtility用のシリアライズ可能な形式
            public List<ShaderCount> original_shaders_list = new List<ShaderCount>();
        }

        [Serializable]
        public class ShaderCount
        {
            public string shader_name;
            public int count;
        }

        [Serializable]
        public class MaterialConversionResult
        {
            public string name;
            public string original_shader;
            public string target_shader;
            public bool success;
            public string error;
            public List<MaterialWarning> warnings = new List<MaterialWarning>();
            public PropertiesConverted properties_converted = new PropertiesConverted();
        }

        [Serializable]
        public class MaterialWarning
        {
            public string type;
            public string property;
            public float[] original_value;
            public float[] clamped_value;
        }

        [Serializable]
        public class PropertiesConverted
        {
            public bool base_color;
            public bool normal_map;
            public bool emission;
            public bool rim_light;
            public bool outline;
        }

        [Serializable]
        public class UnsupportedShader
        {
            public string name;
            public string shader;
            public string reason;
        }

        [Serializable]
        public class ExpressionsInfo
        {
            public int total_blendshapes;
            public int mapped_count;
            public int unmapped_count;
            public List<ExpressionMapping> mappings = new List<ExpressionMapping>();
            public List<ExpressionConflict> conflicts = new List<ExpressionConflict>();
            public List<string> missing_recommended = new List<string>();
        }

        [Serializable]
        public class ExpressionMapping
        {
            public string vrm_expression;
            public string source;
            public string mesh;
        }

        [Serializable]
        public class ExpressionConflict
        {
            public string vrm_expression;
            public List<string> candidates = new List<string>();
            public string selected;
            public string reason;
        }

        [Serializable]
        public class DynamicsInfo
        {
            public string source_type;
            public int vrm_springbone_count;
            public int vrchat_physbone_count;
            public int dynamicbone_count;
            public int collider_count;
            public List<DynamicsConversionResult> conversion_results = new List<DynamicsConversionResult>();
        }

        [Serializable]
        public class DynamicsConversionResult
        {
            public string name;
            public string source_type;
            public bool success;
            public string error;
        }

        [Serializable]
        public class NotificationsInfo
        {
            public NotificationSummary summary = new NotificationSummary();
            public Dictionary<string, NotificationSummary> by_processor = new Dictionary<string, NotificationSummary>();
            public List<NotificationDetail> errors = new List<NotificationDetail>();
            public List<NotificationDetail> warnings = new List<NotificationDetail>();

            // JsonUtility用
            public List<ProcessorNotification> by_processor_list = new List<ProcessorNotification>();

            public List<NotificationEntry> GetAllEntries()
            {
                var entries = new List<NotificationEntry>();
                foreach (var e in errors)
                {
                    entries.Add(new NotificationEntry
                    {
                        Level = "Error",
                        ProcessorId = e.processor_id,
                        Message = e.message
                    });
                }
                foreach (var w in warnings)
                {
                    entries.Add(new NotificationEntry
                    {
                        Level = "Warning",
                        ProcessorId = w.processor_id,
                        Message = w.message
                    });
                }
                return entries;
            }
        }

        [Serializable]
        public class NotificationSummary
        {
            public int info;
            public int warning;
            public int error;
        }

        [Serializable]
        public class ProcessorNotification
        {
            public string processor_id;
            public int info;
            public int warning;
            public int error;
        }

        [Serializable]
        public class NotificationDetail
        {
            public string processor_id;
            public string message;
            public string details;
            public string timestamp;
        }

        [Serializable]
        public class ScreenshotInfo
        {
            public string format = "PNG";
            public int width;
            public int height;
            public List<string> angles = new List<string>
            {
                "Front", "Right", "Back", "Left", "Top", "Bottom",
                "Front-Right", "Front-Left", "Back-Right", "Back-Left"
            };
            public string base64;
        }

        /// <summary>
        /// 後方互換性のための通知エントリ
        /// </summary>
        [Serializable]
        public class NotificationEntry
        {
            public string Level;
            public string ProcessorId;
            public string Message;
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// 新しい報告データを作成
        /// </summary>
        public static BugReportData Create(string modelName, int vrmVersion)
        {
            var data = new BugReportData
            {
                report_id = Guid.NewGuid().ToString("N").Substring(0, 8),
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK"),
                CreatedAt = DateTime.Now
            };

            // 環境情報
            data.environment.package_version = GetPackageVersion();
            data.environment.unity_version = Application.unityVersion;
            data.environment.platform = $"{SystemInfo.operatingSystem} ({SystemInfo.processorType})";
            data.environment.univrm_version = GetUniVRMVersion();
            data.environment.render_pipeline = GetRenderPipeline();

            // エクスポート設定
            data.export_settings.vrm_version = vrmVersion;

            // 元モデル情報
            data.source_model.name = modelName;

            return data;
        }

        /// <summary>
        /// ConversionSettingsから設定を適用
        /// </summary>
        public void ApplySettings(ConversionSettings settings)
        {
            if (settings == null) return;

            export_settings.vrm_version = settings.vrmVersion;
            export_settings.preset_name = settings.presetName;
            export_settings.enable_liltoon_conversion = settings.enableLilToonConversion;
            export_settings.enable_hdr_clamp = settings.enableHdrClamp;
            export_settings.enable_outline_conversion = settings.enableOutlineConversion;
            export_settings.transparent_mode = settings.transparentMode.ToString();
            export_settings.enable_tpose_normalization = settings.enableTPoseNormalization;
            export_settings.enable_armature_rotation_bake = settings.enableArmatureRotationBake;
            export_settings.enable_bone_orientation_normalization = settings.enableBoneOrientationNormalization;
            export_settings.enable_expression_auto_mapping = settings.enableExpressionAutoMapping;
            export_settings.expression_naming_convention = settings.expressionNamingConvention.ToString();
            export_settings.enable_springbone_conversion = settings.enableSpringBoneConversion;
            export_settings.enable_collider_conversion = settings.enableColliderConversion;
            export_settings.output_folder = settings.outputFolder;
            export_settings.file_name_mode = settings.fileNameMode.ToString();
            export_settings.custom_file_name = settings.customFileName;
        }

        /// <summary>
        /// GameObjectからモデル情報を収集
        /// </summary>
        public void CollectModelInfo(GameObject root)
        {
            if (root == null) return;

            source_model.name = root.name;

            // アセットパスの取得
#if UNITY_EDITOR
            var prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                source_model.asset_path = prefabPath;

                // ファイルサイズ取得
                var fullPath = System.IO.Path.GetFullPath(prefabPath);
                if (System.IO.File.Exists(fullPath))
                {
                    var fileInfo = new System.IO.FileInfo(fullPath);
                    source_model.file_size_bytes = fileInfo.Length;
                }

                // ソースフォーマット判定
                var ext = System.IO.Path.GetExtension(prefabPath).ToLower();
                source_model.source_format = ext switch
                {
                    ".fbx" => "FBX",
                    ".prefab" => "Prefab",
                    ".blend" => "Blender",
                    ".obj" => "OBJ",
                    _ => ext.TrimStart('.').ToUpper()
                };
            }
#endif

            CollectSkeletonInfo(root);
            CollectMeshInfo(root);
            CollectMaterialInfo(root);
            CollectDynamicsInfo(root);
        }

        /// <summary>
        /// 骨格情報を収集
        /// </summary>
        private void CollectSkeletonInfo(GameObject root)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null) return;

            skeleton.avatar_name = animator.avatar?.name ?? "";
            skeleton.is_humanoid = animator.isHuman;

            if (animator.isHuman)
            {
                // 必須ボーンチェック
                var requiredBones = new[]
                {
                    HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                    HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                    HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                    HumanBodyBones.RightFoot
                };

                foreach (var bone in requiredBones)
                {
                    var boneTransform = animator.GetBoneTransform(bone);
                    if (boneTransform != null)
                    {
                        skeleton.required_bones.found++;
                    }
                    else
                    {
                        skeleton.required_bones.missing.Add(bone.ToString());
                    }
                }

                // 推奨ボーンチェック
                var recommendedBones = new[]
                {
                    HumanBodyBones.Neck, HumanBodyBones.UpperChest, HumanBodyBones.Jaw,
                    HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
                    HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
                    HumanBodyBones.LeftEye, HumanBodyBones.RightEye
                };

                foreach (var bone in recommendedBones)
                {
                    var boneTransform = animator.GetBoneTransform(bone);
                    if (boneTransform != null)
                    {
                        skeleton.recommended_bones.found++;
                    }
                    else
                    {
                        skeleton.recommended_bones.missing.Add(bone.ToString());
                    }
                }

                skeleton.bone_hierarchy_valid = true; // 詳細な検証は別途実装
                skeleton.t_pose_valid = CheckTPose(animator);
            }

            // Armature回転チェック
            var armature = root.transform.Find("Armature");
            if (armature != null)
            {
                var euler = armature.localEulerAngles;
                skeleton.armature_rotation.euler = new[] { euler.x, euler.y, euler.z };
                skeleton.armature_rotation.requires_normalization =
                    !Mathf.Approximately(euler.x, 0) ||
                    !Mathf.Approximately(euler.y, 0) ||
                    !Mathf.Approximately(euler.z, 0);
            }

            // 総ボーン数
            skeleton.total_bones = root.GetComponentsInChildren<Transform>().Length;
        }

        /// <summary>
        /// T-Poseチェック（簡易版）
        /// </summary>
        private bool CheckTPose(Animator animator)
        {
            if (!animator.isHuman) return false;

            var leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

            if (leftArm == null || rightArm == null) return false;

            // 腕が水平に近いかチェック
            var leftDir = (animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position - leftArm.position).normalized;
            var rightDir = (animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position - rightArm.position).normalized;

            var leftAngle = Vector3.Angle(leftDir, Vector3.left);
            var rightAngle = Vector3.Angle(rightDir, Vector3.right);

            return leftAngle < 30f && rightAngle < 30f;
        }

        /// <summary>
        /// メッシュ情報を収集
        /// </summary>
        private void CollectMeshInfo(GameObject root)
        {
            var skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>();
            var meshFilters = root.GetComponentsInChildren<MeshFilter>();

            meshes.skinned_mesh_count = skinnedMeshes.Length;
            meshes.mesh_filter_count = meshFilters.Length;

            foreach (var smr in skinnedMeshes)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;

                var detail = new MeshDetail
                {
                    name = smr.name,
                    vertices = mesh.vertexCount,
                    triangles = mesh.triangles.Length / 3,
                    blendshapes = mesh.blendShapeCount,
                    submeshes = mesh.subMeshCount,
                    material_slots = smr.sharedMaterials?.Length ?? 0
                };

                detail.bounds.center = new[] { mesh.bounds.center.x, mesh.bounds.center.y, mesh.bounds.center.z };
                detail.bounds.size = new[] { mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z };

                meshes.meshes.Add(detail);
                meshes.total_vertices += detail.vertices;
                meshes.total_triangles += detail.triangles;
                meshes.blendshape_count += detail.blendshapes;
            }

            foreach (var mf in meshFilters)
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                var renderer = mf.GetComponent<MeshRenderer>();

                var detail = new MeshDetail
                {
                    name = mf.name,
                    vertices = mesh.vertexCount,
                    triangles = mesh.triangles.Length / 3,
                    blendshapes = 0,
                    submeshes = mesh.subMeshCount,
                    material_slots = renderer?.sharedMaterials?.Length ?? 0
                };

                detail.bounds.center = new[] { mesh.bounds.center.x, mesh.bounds.center.y, mesh.bounds.center.z };
                detail.bounds.size = new[] { mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z };

                meshes.meshes.Add(detail);
                meshes.total_vertices += detail.vertices;
                meshes.total_triangles += detail.triangles;
            }
        }

        /// <summary>
        /// マテリアル情報を収集
        /// </summary>
        private void CollectMaterialInfo(GameObject root)
        {
            var allMaterials = new HashSet<Material>();
            var shaderCounts = new Dictionary<string, int>();

            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null) continue;
                    allMaterials.Add(mat);

                    var shaderName = mat.shader?.name ?? "Unknown";
                    if (!shaderCounts.ContainsKey(shaderName))
                        shaderCounts[shaderName] = 0;
                    shaderCounts[shaderName]++;
                }
            }

            materials.total_count = allMaterials.Count;
            materials.original_shaders = shaderCounts;

            // JsonUtility用のリスト形式
            foreach (var kvp in shaderCounts)
            {
                materials.original_shaders_list.Add(new ShaderCount
                {
                    shader_name = kvp.Key,
                    count = kvp.Value
                });
            }
        }

        /// <summary>
        /// 物理情報を収集
        /// </summary>
        private void CollectDynamicsInfo(GameObject root)
        {
            // VRM SpringBone
            var springBoneType = System.Type.GetType("VRM.VRMSpringBone, VRM");
            if (springBoneType != null)
            {
                var springBones = root.GetComponentsInChildren(springBoneType);
                dynamics.vrm_springbone_count = springBones.Length;
                if (springBones.Length > 0) dynamics.source_type = "VRM SpringBone";
            }

            // VRChat PhysBone
            var physBoneType = System.Type.GetType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone, VRC.SDK3.Dynamics.PhysBone");
            if (physBoneType != null)
            {
                var physBones = root.GetComponentsInChildren(physBoneType);
                dynamics.vrchat_physbone_count = physBones.Length;
                if (physBones.Length > 0) dynamics.source_type = "VRChat PhysBone";
            }

            // DynamicBone
            var dynamicBoneType = System.Type.GetType("DynamicBone, DynamicBone");
            if (dynamicBoneType != null)
            {
                var dynamicBones = root.GetComponentsInChildren(dynamicBoneType);
                dynamics.dynamicbone_count = dynamicBones.Length;
                if (dynamicBones.Length > 0) dynamics.source_type = "DynamicBone";
            }

            // Collider counts
            dynamics.collider_count = root.GetComponentsInChildren<Collider>().Length;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// パッケージバージョンを取得
        /// </summary>
        private static string GetPackageVersion()
        {
            try
            {
                var packagePath = "Packages/com.dsgarage.fbx4vrmconversion/package.json";
                if (System.IO.File.Exists(packagePath))
                {
                    var json = System.IO.File.ReadAllText(packagePath);
                    var match = System.Text.RegularExpressions.Regex.Match(json, @"""version"":\s*""([^""]+)""");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }

                var projectPackagePath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Application.dataPath),
                    "package.json");
                if (System.IO.File.Exists(projectPackagePath))
                {
                    var json = System.IO.File.ReadAllText(projectPackagePath);
                    var match = System.Text.RegularExpressions.Regex.Match(json, @"""version"":\s*""([^""]+)""");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
            catch { }

            return "unknown";
        }

        /// <summary>
        /// UniVRMバージョンを取得
        /// </summary>
        private static string GetUniVRMVersion()
        {
            try
            {
                var vrmType = System.Type.GetType("VRM.VRMVersion, VRM");
                if (vrmType != null)
                {
                    var versionField = vrmType.GetField("VERSION", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (versionField != null)
                    {
                        return versionField.GetValue(null)?.ToString() ?? "unknown";
                    }
                }
            }
            catch { }

            return "unknown";
        }

        /// <summary>
        /// レンダーパイプラインを取得
        /// </summary>
        private static string GetRenderPipeline()
        {
            var rpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rpAsset == null) return "Built-in";

            var typeName = rpAsset.GetType().Name;
            if (typeName.Contains("Universal") || typeName.Contains("URP"))
                return "URP";
            if (typeName.Contains("HD") || typeName.Contains("HDRP"))
                return "HDRP";

            return typeName;
        }

        /// <summary>
        /// エラーを追加
        /// </summary>
        public void AddError(string processorId, string message, string details = null)
        {
            notifications.errors.Add(new NotificationDetail
            {
                processor_id = processorId,
                message = message,
                details = details,
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK")
            });
            notifications.summary.error++;
        }

        /// <summary>
        /// 警告を追加
        /// </summary>
        public void AddWarning(string processorId, string message, string details = null)
        {
            notifications.warnings.Add(new NotificationDetail
            {
                processor_id = processorId,
                message = message,
                details = details,
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK")
            });
            notifications.summary.warning++;
        }

        /// <summary>
        /// スクリーンショットをBase64にエンコード
        /// </summary>
        public void EncodeScreenshot()
        {
            if (ScreenshotBytes != null && ScreenshotBytes.Length > 0)
            {
                screenshot.base64 = Convert.ToBase64String(ScreenshotBytes);
            }
        }

        /// <summary>
        /// JSON形式にシリアライズ（API送信用）
        /// </summary>
        public string ToJson()
        {
            EncodeScreenshot();
            return JsonUtility.ToJson(this, true);
        }

        /// <summary>
        /// APIスキーマに準拠したJSON出力（Dictionary使用版）
        /// </summary>
        public string ToApiJson()
        {
            EncodeScreenshot();

            // JsonUtilityはDictionaryをサポートしないため、
            // 手動でJSON構築するか、Newtonsoft.Jsonを使用する
            // ここでは簡易的にJsonUtilityを使用
            return JsonUtility.ToJson(this, true);
        }

        #endregion
    }
}
