using UnityEditor;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.UI
{
    /// <summary>
    /// Hierarchyコンテキストメニュー拡張
    /// 右クリックからワンボタンVRM化
    /// </summary>
    public static class HierarchyContextMenu
    {
        private const int MenuPriority = 49; // Createメニューの直前

        /// <summary>
        /// Quick Export VRM メニュー項目
        /// </summary>
        [MenuItem("GameObject/FBX4VRM/Quick Export VRM", false, MenuPriority)]
        private static void QuickExportVrm(MenuCommand command)
        {
            var obj = command.context as GameObject;
            if (obj == null)
            {
                obj = Selection.activeGameObject;
            }

            if (obj != null)
            {
                QuickExportWindow.ShowWithObject(obj);
            }
            else
            {
                QuickExportWindow.ShowWindow();
            }
        }

        /// <summary>
        /// Quick Export VRM メニューのバリデーション
        /// </summary>
        [MenuItem("GameObject/FBX4VRM/Quick Export VRM", true)]
        private static bool ValidateQuickExportVrm()
        {
            var obj = Selection.activeGameObject;
            if (obj == null) return true; // メニュー表示はする

            // シーンオブジェクトのみ
            return obj.scene.IsValid();
        }

        /// <summary>
        /// Export with Settings メニュー項目
        /// </summary>
        [MenuItem("GameObject/FBX4VRM/Export with Settings...", false, MenuPriority + 1)]
        private static void ExportWithSettings(MenuCommand command)
        {
            var obj = command.context as GameObject;
            if (obj == null)
            {
                obj = Selection.activeGameObject;
            }

            // Export Windowを開いてオブジェクトを選択
            FBX4VRMExportWindow.ShowWindow();
        }

        /// <summary>
        /// Export with Settings メニューのバリデーション
        /// </summary>
        [MenuItem("GameObject/FBX4VRM/Export with Settings...", true)]
        private static bool ValidateExportWithSettings()
        {
            var obj = Selection.activeGameObject;
            if (obj == null) return true;
            return obj.scene.IsValid();
        }
    }
}
