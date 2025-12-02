namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// Processor基底クラス
    /// </summary>
    public abstract class ExportProcessorBase : IExportProcessor
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract int Order { get; }

        public bool Enabled { get; set; } = true;

        public abstract ProcessorResult Execute(ExportContext context);

        /// <summary>
        /// 結果オブジェクトを生成するヘルパー
        /// </summary>
        protected ProcessorResult CreateResult()
        {
            return new ProcessorResult();
        }
    }
}
