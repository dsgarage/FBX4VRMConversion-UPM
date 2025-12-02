using System.Collections.Generic;

namespace DSGarage.FBX4VRM
{
    /// <summary>
    /// Processor実行結果
    /// </summary>
    public class ProcessorResult
    {
        /// <summary>
        /// 処理が成功したか（Errorがない場合true）
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Export継続可能か（Errorがない場合true）
        /// </summary>
        public bool CanContinue => !HasError;

        /// <summary>
        /// Errorレベルの通知があるか
        /// </summary>
        public bool HasError { get; private set; }

        /// <summary>
        /// Warningレベルの通知があるか
        /// </summary>
        public bool HasWarning { get; private set; }

        /// <summary>
        /// 通知リスト
        /// </summary>
        public List<ProcessorNotification> Notifications { get; } = new List<ProcessorNotification>();

        public ProcessorResult()
        {
            Success = true;
        }

        public void AddNotification(ProcessorNotification notification)
        {
            Notifications.Add(notification);

            switch (notification.Level)
            {
                case NotificationLevel.Error:
                    HasError = true;
                    Success = false;
                    break;
                case NotificationLevel.Warning:
                    HasWarning = true;
                    break;
            }
        }

        public void AddInfo(string processorId, string message, string details = null)
        {
            AddNotification(new ProcessorNotification(processorId, NotificationLevel.Info, message, details));
        }

        public void AddWarning(string processorId, string message, string details = null)
        {
            AddNotification(new ProcessorNotification(processorId, NotificationLevel.Warning, message, details));
        }

        public void AddError(string processorId, string message, string details = null)
        {
            AddNotification(new ProcessorNotification(processorId, NotificationLevel.Error, message, details));
        }
    }
}
