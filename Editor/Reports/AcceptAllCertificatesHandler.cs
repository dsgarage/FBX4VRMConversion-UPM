using UnityEngine.Networking;

namespace DSGarage.FBX4VRM.Editor.Reports
{
    /// <summary>
    /// 自己署名証明書を許可するCertificateHandler
    /// エディタ専用 - 本番ビルドでは使用しないこと
    /// </summary>
    public class AcceptAllCertificatesHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            // 自己署名証明書を許可
            // 注意: これはセキュリティリスクがあるため、
            // 開発・テスト環境でのみ使用すること
            return true;
        }
    }
}
