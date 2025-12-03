using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DSGarage.FBX4VRM.Editor.Reports
{
    /// <summary>
    /// 複数アングルからのスクリーンショット撮影ユーティリティ
    /// 前後左右上下の6方向 + 斜め4方向を1枚の画像に合成
    /// </summary>
    public static class MultiAngleCapture
    {
        /// <summary>
        /// キャプチャ設定
        /// </summary>
        public class CaptureSettings
        {
            public int SingleImageSize = 256;
            public int Columns = 4;
            public Color BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            public float CameraDistanceMultiplier = 2.0f; // バウンズサイズに対する倍率
            public float VerticalPadding = 1.2f; // 縦方向の余白（1.0 = ぴったり、1.2 = 20%余白）
            public bool IncludeDiagonals = true;
        }

        /// <summary>
        /// カメラアングル定義
        /// </summary>
        private static readonly (string name, Vector3 direction, Vector3 up, bool isVertical)[] Angles = new[]
        {
            ("Front", Vector3.forward, Vector3.up, false),
            ("Right", Vector3.right, Vector3.up, false),
            ("Back", Vector3.back, Vector3.up, false),
            ("Left", Vector3.left, Vector3.up, false),
            ("Top", Vector3.up, Vector3.forward, true),
            ("Bottom", Vector3.down, Vector3.forward, true),
            // 斜めアングル
            ("Front-Right", (Vector3.forward + Vector3.right).normalized, Vector3.up, false),
            ("Front-Left", (Vector3.forward + Vector3.left).normalized, Vector3.up, false),
            ("Back-Right", (Vector3.back + Vector3.right).normalized, Vector3.up, false),
            ("Back-Left", (Vector3.back + Vector3.left).normalized, Vector3.up, false),
        };

        /// <summary>
        /// モデルを複数アングルから撮影し、1枚の画像に合成
        /// </summary>
        /// <param name="target">撮影対象のGameObject</param>
        /// <param name="settings">キャプチャ設定（nullの場合はデフォルト）</param>
        /// <returns>合成された画像（PNG形式のバイト配列）</returns>
        public static byte[] CaptureMultiAngle(GameObject target, CaptureSettings settings = null)
        {
            if (target == null)
            {
                Debug.LogError("[MultiAngleCapture] Target is null");
                return null;
            }

            settings ??= new CaptureSettings();

            var angleCount = settings.IncludeDiagonals ? 10 : 6;
            var rows = Mathf.CeilToInt((float)angleCount / settings.Columns);
            var totalWidth = settings.SingleImageSize * settings.Columns;
            var totalHeight = settings.SingleImageSize * rows;

            // モデルのバウンズを計算
            var bounds = CalculateBounds(target);
            var center = bounds.center;
            var size = bounds.size;

            // 最大の寸法を使ってカメラ距離を決定（全身が入るように）
            var maxDimension = Mathf.Max(size.x, size.y, size.z);
            var cameraDistance = maxDimension * settings.CameraDistanceMultiplier;

            Debug.Log($"[MultiAngleCapture] Bounds center: {center}, size: {size}, maxDim: {maxDimension}, cameraDist: {cameraDistance}");

            // 合成用テクスチャ
            var compositeTexture = new Texture2D(totalWidth, totalHeight, TextureFormat.RGB24, false);

            // 背景色で塗りつぶし
            var bgPixels = new Color[totalWidth * totalHeight];
            for (int i = 0; i < bgPixels.Length; i++)
            {
                bgPixels[i] = settings.BackgroundColor;
            }
            compositeTexture.SetPixels(bgPixels);

            // 一時カメラを作成
            var cameraGO = new GameObject("_MultiAngleCamera");
            cameraGO.hideFlags = HideFlags.HideAndDontSave;
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = settings.BackgroundColor;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = cameraDistance * 5f;

            // FOVを計算して全身が入るように調整
            // 垂直FOVから必要な距離を逆算
            var requiredFOV = 2f * Mathf.Atan2(maxDimension * settings.VerticalPadding / 2f, cameraDistance) * Mathf.Rad2Deg;
            camera.fieldOfView = Mathf.Clamp(requiredFOV, 20f, 60f);

            Debug.Log($"[MultiAngleCapture] Camera FOV: {camera.fieldOfView}");

            // レンダーテクスチャ
            var renderTexture = new RenderTexture(settings.SingleImageSize, settings.SingleImageSize, 24);
            camera.targetTexture = renderTexture;

            try
            {
                for (int i = 0; i < angleCount; i++)
                {
                    var (name, direction, up, isVertical) = Angles[i];

                    // カメラ位置を設定（バウンズの中心から指定方向に離れる）
                    var cameraPos = center - direction * cameraDistance;
                    camera.transform.position = cameraPos;

                    // カメラをバウンズの中心に向ける
                    camera.transform.LookAt(center, up);

                    // 上下からの撮影時はFOVを調整
                    if (isVertical)
                    {
                        var horizontalSize = Mathf.Max(size.x, size.z);
                        var verticalFOV = 2f * Mathf.Atan2(horizontalSize * settings.VerticalPadding / 2f, cameraDistance) * Mathf.Rad2Deg;
                        camera.fieldOfView = Mathf.Clamp(verticalFOV, 20f, 60f);
                    }
                    else
                    {
                        camera.fieldOfView = Mathf.Clamp(requiredFOV, 20f, 60f);
                    }

                    // レンダリング
                    camera.Render();

                    // RenderTextureからTexture2Dにコピー
                    RenderTexture.active = renderTexture;
                    var singleCapture = new Texture2D(settings.SingleImageSize, settings.SingleImageSize, TextureFormat.RGB24, false);
                    singleCapture.ReadPixels(new Rect(0, 0, settings.SingleImageSize, settings.SingleImageSize), 0, 0);
                    singleCapture.Apply();
                    RenderTexture.active = null;

                    // 合成画像に配置
                    var col = i % settings.Columns;
                    var row = rows - 1 - (i / settings.Columns); // 上から下へ
                    var x = col * settings.SingleImageSize;
                    var y = row * settings.SingleImageSize;

                    compositeTexture.SetPixels(x, y, settings.SingleImageSize, settings.SingleImageSize, singleCapture.GetPixels());

                    // ラベルを追加
                    AddLabel(compositeTexture, x, y + settings.SingleImageSize - 20, name, settings.SingleImageSize);

                    UnityEngine.Object.DestroyImmediate(singleCapture);
                }

                compositeTexture.Apply();

                // PNG形式でエンコード
                var pngData = compositeTexture.EncodeToPNG();
                Debug.Log($"[MultiAngleCapture] Generated {angleCount} angle captures, PNG size: {pngData?.Length ?? 0} bytes");
                return pngData;
            }
            finally
            {
                // クリーンアップ
                camera.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(cameraGO);
                UnityEngine.Object.DestroyImmediate(compositeTexture);
            }
        }

        /// <summary>
        /// GameObjectのバウンディングボックスを計算
        /// </summary>
        private static Bounds CalculateBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                // レンダラーがない場合はTransformから推測
                Debug.LogWarning("[MultiAngleCapture] No renderers found, using default bounds");
                return new Bounds(target.transform.position + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.3f));
            }

            // 有効なバウンズを持つレンダラーを探す
            Bounds? combinedBounds = null;
            foreach (var renderer in renderers)
            {
                if (renderer.bounds.size.sqrMagnitude > 0.0001f) // ほぼゼロでないバウンズ
                {
                    if (combinedBounds == null)
                    {
                        combinedBounds = renderer.bounds;
                    }
                    else
                    {
                        var b = combinedBounds.Value;
                        b.Encapsulate(renderer.bounds);
                        combinedBounds = b;
                    }
                }
            }

            if (combinedBounds == null)
            {
                Debug.LogWarning("[MultiAngleCapture] All renderers have zero bounds, using default");
                return new Bounds(target.transform.position + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.3f));
            }

            return combinedBounds.Value;
        }

        /// <summary>
        /// テクスチャにラベルを追加（簡易実装）
        /// </summary>
        private static void AddLabel(Texture2D texture, int x, int y, string label, int width)
        {
            // 背景バー（半透明の黒）
            var barColor = new Color(0, 0, 0, 0.7f);
            var barHeight = 20;

            for (int py = y; py < y + barHeight && py < texture.height; py++)
            {
                for (int px = x; px < x + width && px < texture.width; px++)
                {
                    var existing = texture.GetPixel(px, py);
                    texture.SetPixel(px, py, Color.Lerp(existing, barColor, 0.7f));
                }
            }
            // 注: 実際のテキスト描画はUnityの制限で困難なため、
            // ラベルは別途UIで表示するか、フォントテクスチャを使用する必要がある
        }
    }
}
