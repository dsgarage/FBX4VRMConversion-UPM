using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// VRM Export用Processorインターフェース
    /// 各Processorは独立して実行可能で、順序変更・ON/OFFが可能
    /// </summary>
    public interface IExportProcessor
    {
        /// <summary>
        /// ProcessorのユニークID
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 表示名
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 説明
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 実行順序（小さいほど先に実行）
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 有効/無効
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Processorを実行
        /// </summary>
        /// <param name="context">変換コンテキスト（複製されたGameObject等）</param>
        /// <returns>実行結果</returns>
        ProcessorResult Execute(ExportContext context);
    }
}
