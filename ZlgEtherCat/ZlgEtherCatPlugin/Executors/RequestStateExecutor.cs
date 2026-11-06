using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class RequestStateExecutor : ZlgExecutorBase<RequestStateSetting>
{
    public RequestStateExecutor() : base(new RequestStatePlugin()) { }
}
