using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DSGarage.FBX4VRM.Editor.Processors
{
    /// <summary>
    /// Processorパイプライン管理
    /// 順序可変・ON/OFF可の独立Processor群を管理し、順次実行する
    /// </summary>
    public class ProcessorPipeline
    {
        private readonly List<IExportProcessor> _processors = new List<IExportProcessor>();

        /// <summary>
        /// 登録されているProcessor一覧（順序順）
        /// </summary>
        public IReadOnlyList<IExportProcessor> Processors => _processors.OrderBy(p => p.Order).ToList();

        /// <summary>
        /// Processorを追加
        /// </summary>
        public void AddProcessor(IExportProcessor processor)
        {
            _processors.Add(processor);
        }

        /// <summary>
        /// Processorを削除
        /// </summary>
        public void RemoveProcessor(string processorId)
        {
            _processors.RemoveAll(p => p.Id == processorId);
        }

        /// <summary>
        /// Processorを取得
        /// </summary>
        public IExportProcessor GetProcessor(string processorId)
        {
            return _processors.FirstOrDefault(p => p.Id == processorId);
        }

        /// <summary>
        /// パイプラインを実行
        /// Errorが発生した時点で中断
        /// </summary>
        public PipelineResult Execute(ExportContext context)
        {
            var pipelineResult = new PipelineResult();
            var orderedProcessors = _processors.Where(p => p.Enabled).OrderBy(p => p.Order);

            foreach (var processor in orderedProcessors)
            {
                Debug.Log($"[FBX4VRM] Executing processor: {processor.DisplayName}");

                var result = processor.Execute(context);
                context.MergeResult(result);
                pipelineResult.ProcessorResults[processor.Id] = result;

                if (!result.CanContinue)
                {
                    Debug.LogError($"[FBX4VRM] Pipeline stopped at {processor.DisplayName} due to error");
                    pipelineResult.Success = false;
                    pipelineResult.StoppedAtProcessorId = processor.Id;
                    break;
                }
            }

            if (string.IsNullOrEmpty(pipelineResult.StoppedAtProcessorId))
            {
                pipelineResult.Success = true;
            }

            return pipelineResult;
        }

        /// <summary>
        /// デフォルトのProcessor群を登録
        /// </summary>
        public void RegisterDefaultProcessors()
        {
            // Phase 0: Root検証
            AddProcessor(new RootValidationProcessor());

            // Phase 1: lilToonサポート
            AddProcessor(new LilToonDetectProcessor());
            AddProcessor(new LilToonToMToonProcessor());

            // Phase 2以降で追加予定:
            // AddProcessor(new GltfValueClampProcessor());
            // AddProcessor(new ExpressionSetupProcessor());
            // AddProcessor(new SpringBoneConvertProcessor());
            // AddProcessor(new MetaPresetApplyProcessor());
            // AddProcessor(new PoseFreezeProcessor());
        }
    }

    /// <summary>
    /// パイプライン実行結果
    /// </summary>
    public class PipelineResult
    {
        public bool Success { get; set; }
        public string StoppedAtProcessorId { get; set; }
        public Dictionary<string, ProcessorResult> ProcessorResults { get; } = new Dictionary<string, ProcessorResult>();

        /// <summary>
        /// 全通知を取得
        /// </summary>
        public IEnumerable<ProcessorNotification> GetAllNotifications()
        {
            return ProcessorResults.Values.SelectMany(r => r.Notifications);
        }

        /// <summary>
        /// 指定レベル以上の通知を取得
        /// </summary>
        public IEnumerable<ProcessorNotification> GetNotifications(NotificationLevel minLevel)
        {
            return GetAllNotifications().Where(n => n.Level >= minLevel);
        }
    }
}
