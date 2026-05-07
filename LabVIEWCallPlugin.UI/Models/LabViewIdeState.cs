namespace LabVIEWCallPlugin.UI.Models
{
    /// <summary>
    /// LabVIEW 开发环境（IDE）连接状态
    /// </summary>
    public enum LabViewIdeState
    {
        /// <summary>尚未检测</summary>
        Unknown,
        /// <summary>IDE 已连接</summary>
        Connected,
        /// <summary>IDE 未打开或无法连接</summary>
        Disconnected
    }
}