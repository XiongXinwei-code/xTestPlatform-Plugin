namespace ZlgEtherCat.Tests;

internal static class NativeCompilationProbe
{
    // 此编译探针保证离线测试使用真实封装类型，而不加载设备 DLL。
    internal static System.Type ApiType => typeof(ZlgEtherCat.Native.ZlgNativeApi);
}
