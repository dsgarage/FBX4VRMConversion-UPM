namespace DSGarage.FBX4VRM
{
    /// <summary>
    /// 通知レベル
    /// - Info: 情報通知（lilToon検出、MToon変換成功、Preset適用完了など）
    /// - Warning: 警告（HDRクランプ適用、自動補正/再マップ実施など）Export継続可
    /// - Error: エラー（Humanoid化不可、有効meshなし、必須Meta欠落など）Export停止
    /// </summary>
    public enum NotificationLevel
    {
        Info,
        Warning,
        Error
    }
}
