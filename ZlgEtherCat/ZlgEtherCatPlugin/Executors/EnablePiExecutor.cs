using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Executors;

public sealed class EnablePiExecutor : ZlgExecutorBase<EnablePiSetting>
{
    public EnablePiExecutor() : base(new EnablePiPlugin()) { }
}
