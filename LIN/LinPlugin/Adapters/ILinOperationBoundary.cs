namespace LIN.Adapters;

/// <summary>同构建内的多阶段驱动操作：每个后续原生提交前重查调用方、代次与单调期限。</summary>
internal interface ILinOperationBoundary
{
    void Open(LinAdapterConfig config, Action beforeSubmit);
    void Wakeup(bool remote, Action beforeSubmit);
}
