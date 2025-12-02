using System;

namespace DSGarage.FBX4VRM
{
    /// <summary>
    /// Processorからの通知情報
    /// </summary>
    [Serializable]
    public class ProcessorNotification
    {
        public string ProcessorId { get; set; }
        public NotificationLevel Level { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public DateTime Timestamp { get; set; }

        public ProcessorNotification(string processorId, NotificationLevel level, string message, string details = null)
        {
            ProcessorId = processorId;
            Level = level;
            Message = message;
            Details = details;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"[{Level}] {ProcessorId}: {Message}";
        }
    }
}
