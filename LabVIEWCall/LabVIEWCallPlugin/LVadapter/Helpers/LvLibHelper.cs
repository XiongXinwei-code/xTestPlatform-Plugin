using LabVIEWCallPlugin.LVadapter.Interop;
using LabVIEWCallPlugin.LVadapter;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LabVIEWCallPlugin.LVadapter
{
    /// <summary>
    /// lvLib.dll 高层辅助类。
    /// 封装 VI 加载、运行、参数读写等常用操作。
    /// 参数传递使用 LabVIEW Flatten To String 格式（typeString + dataString），
    /// 全程托管数组，无非托管内存分配，无需手动释放。
    /// </summary>
    public class LvLibHelper
    {
        private const int DefaultBufferSize = 102400;
        private const ushort DefaultPort = 3363;

        // ── 连接 LabVIEW ──────────────────────────────────────────────────────

        /// <summary>
        /// 检查 LabVIEW 是否已连接，不抛出异常。
        /// 专用于连接状态检测（如 UI 状态栏、校验前置检查）。
        /// </summary>
        /// <param name="port">LabVIEW 监听端口，默认 3363</param>
        /// <returns>连接成功返回 <see langword="true"/>，否则返回 <see langword="false"/></returns>
        public static bool ConnectLabVIEW(ushort port = DefaultPort)
            => ConnectLabVIEW(out _, port);

        public static bool ConnectLabVIEW(out string diagnostic, ushort port = DefaultPort)
            => LvLibWrapper.SafeConnect(port, out diagnostic);

        // ── 连接面板 / 编辑 / 库项目 ─────────────────────────────────────────

        /// <summary>
        /// 获取连接面板信息（前面板控件/指示器名称及 Pixmap）。
        /// </summary>
        /// <param name="port">LabVIEW 监听端口，默认 3363</param>
        public static ConnectPanelInfo GetConnectPanel(
            string viPath,
            ushort port = DefaultPort,
            string controlIn = "",
            string indicatorIn = "",
            int bufferSize = DefaultBufferSize)
            => ReadPanel(viPath, port, controlIn, indicatorIn, bufferSize, true);

        internal static ConnectPanelInfo GetTerminalMetadata(string viPath, ushort port = DefaultPort)
            => ReadPanel(viPath, port, "", "", DefaultBufferSize, false);

        private static ConnectPanelInfo ReadPanel(string viPath, ushort port, string controlIn,
            string indicatorIn, int bufferSize, bool includeImage)
        {
            if (bufferSize <= 1 || bufferSize > 8 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "面板缓冲区应为 2 至 8388608 字节。");
            LvAnsiText.Check(viPath, nameof(viPath));
            // LabVIEW Flatten/Unflatten To JSON exchanges UTF-8 even when the
            // native file path API uses the Windows ANSI code page.
            var controlInBuf = EncodeJsonCString(controlIn, bufferSize);
            var indicatorInBuf = EncodeJsonCString(indicatorIn, bufferSize);
            var controlOutBuf = new byte[bufferSize];
            var indicatorOutBuf = new byte[bufferSize];
            IntPtr pixmapHandle = IntPtr.Zero;
            Exception? panelError = null;
            int? pixmapBytes = null;

            try
            {
                LvLibWrapper.LVfunction_GetConnectPanel(
                    viPath,
                    controlInBuf, indicatorInBuf,
                    port,
                    out bool isReentrant,
                    out VIState viState,
                    controlOutBuf, indicatorOutBuf,
                    ref pixmapHandle,
                    bufferSize, bufferSize);

                if (pixmapHandle != IntPtr.Zero)
                {
                    var data = Marshal.ReadIntPtr(pixmapHandle);
                    if (data == IntPtr.Zero) throw new InvalidDataException("VI 面板图像句柄没有数组数据。");
                    pixmapBytes = checked(8 + 4 * GetPixmapPixelCount(Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4)));
                }
                PixmapData pixmap = includeImage ? ReadPixmapData(pixmapHandle) : new();

                return new ConnectPanelInfo
                {
                    ControlIn = ReadJsonCString(controlInBuf),
                    IndicatorIn = ReadJsonCString(indicatorInBuf),
                    ControlOut = ReadJsonCString(controlOutBuf),
                    IndicatorOut = ReadJsonCString(indicatorOutBuf),
                    IsReentrant = isReentrant,
                    VIState = viState,
                    Width = pixmap.Width,
                    Height = pixmap.Height,
                    PixelData = pixmap.PixelData
                };
            }
            catch (Exception ex)
            {
                panelError = ex;
                throw;
            }
            finally
            {
                if (pixmapHandle != IntPtr.Zero)
                {
                    try
                    {
                        // The hash-pinned bridge's generated DLLDeflateArray returns the
                        // released allocation size, despite its header declaring MgErr.
                        // Verified against 1x1, 3x7 and 60x120 arrays in NI Runtime 2026.
                        int releasedBytes = LvLibWrapper.DeAllocateUint32Array(ref pixmapHandle);
                        ValidatePixmapRelease(releasedBytes, pixmapHandle, pixmapBytes);
                    }
                    catch (Exception cleanupError) when (panelError != null)
                    {
                        throw new AggregateException("读取和释放 VI 面板图像均失败。", panelError, cleanupError);
                    }
                }
            }
        }

        /// <summary>
        /// 编辑 VI 文件（在 LabVIEW 中打开编辑）。
        /// </summary>
        /// <param name="port">LabVIEW 监听端口，默认 3363</param>
        /// <exception cref="ArgumentException">VI 路径为空时抛出</exception>
        /// <exception cref="InvalidOperationException">编辑失败时抛出</exception>
        public static bool EditVI(string viPath, ushort port = DefaultPort)
        {
            if (string.IsNullOrWhiteSpace(viPath))
                throw new ArgumentException("VI 路径不能为空", nameof(viPath));

            LvAnsiText.Check(viPath, nameof(viPath));
            int result = LvLibWrapper.LVfunction_EditVI(viPath, port);
            if (result != 0)
                throw new InvalidOperationException(
                    $"编辑 VI 文件失败，错误代码: {result}，路径: {viPath}");

            return true;
        }

        /// <summary>
        /// 获取 LabVIEW 库（.lvlib）中的项目路径列表。
        /// </summary>
        /// <param name="port">LabVIEW 监听端口，默认 3363</param>
        /// <exception cref="ArgumentException">库路径为空时抛出</exception>
        /// <exception cref="InvalidOperationException">获取失败时抛出</exception>
        public static List<string> GetLibItems(
            string lvLibPath,
            ushort port = DefaultPort,
            int bufferSize = DefaultBufferSize)
        {
            if (string.IsNullOrWhiteSpace(lvLibPath))
                throw new ArgumentException("LabVIEW 库路径不能为空", nameof(lvLibPath));

            if (bufferSize <= 1 || bufferSize > 8 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            LvAnsiText.Check(lvLibPath, nameof(lvLibPath));
            var buf = new byte[bufferSize];
            LvLibWrapper.LVfunction_GetLibItems(lvLibPath, port, buf, out int error, bufferSize);

            if (error != 0)
                throw new InvalidOperationException(
                    $"获取库项目失败，错误代码: {error}，路径: {lvLibPath}");

            string raw = ReadCString(buf);
            if (string.IsNullOrWhiteSpace(raw)) return [];

            var items = new List<string>();
            foreach (var line in raw.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            {
                var t = line.Trim();
                if (!string.IsNullOrEmpty(t)) items.Add(t);
            }
            return items;
        }

        // ── DLL 状态 / 执行系统 ────────────────────────────────────────────

        /// <summary>查询 DLL 当前状态，正常返回 "OK"。</summary>
        public static string GetDllStatus(IntPtr module = default)
        {
            var sb = new StringBuilder(256);
            int result = LvLibWrapper.LVDLLStatus(sb, sb.Capacity, module);
            return result == 0 ? "OK" : sb.ToString();
        }

        /// <summary>设置是否在 LabVIEW 私有执行系统中运行 VI。</summary>
        public static void SetPrivateExecutionSystem(bool enable)
            => LvLibWrapper.SetExecuteVIsInPrivateExecutionSystem(enable);

        // ── VI 运行时 ──────────────────────────────────────────────────────

        /// <summary>
        /// 加载 VI，返回 VI 引用句柄。
        /// </summary>
        /// <param name="port">LabVIEW 监听端口，默认 3363</param>
        /// <exception cref="ArgumentException">路径为空时抛出</exception>
        /// <exception cref="InvalidOperationException">加载失败时抛出</exception>
        public static IntPtr LoadVI(string viPath, ushort port = DefaultPort)
        {
            if (string.IsNullOrWhiteSpace(viPath))
                throw new ArgumentException("VI 路径不能为空", nameof(viPath));

            if (!LvLibWrapper.SafeLoadVI(port, viPath, out IntPtr viRef, out string err, out var exception))
                throw new InvalidOperationException($"加载 VI 失败：{err}，路径：{viPath}", exception);

            if (viRef == IntPtr.Zero)
                throw new InvalidOperationException($"加载 VI 未返回有效引用：{viPath}");
            return viRef;
        }

        /// <summary>
        /// 运行已加载的 VI。
        /// </summary>
        /// <exception cref="ArgumentException">VI 引用无效时抛出</exception>
        /// <exception cref="InvalidOperationException">运行失败时抛出</exception>
        public static IntPtr RunVI(IntPtr viReference, bool openPanel = false, bool closePanel = true)
        {
            if (viReference == IntPtr.Zero)
                throw new ArgumentException("VI 引用无效", nameof(viReference));

            if (!LvLibWrapper.SafeRunVI(viReference, openPanel, closePanel, out IntPtr refOut, out string err, out var exception))
                throw new InvalidOperationException($"运行 VI 失败：{err}", exception);

            if (refOut == IntPtr.Zero)
                throw new InvalidOperationException("运行 VI 后桥未返回有效引用，无法确认后续参数可读。");
            return refOut;
        }

        /// <summary>
        /// 加载并运行 VI（一步调用）。
        /// </summary>
        /// <param name="port">LabVIEW 监听端口，默认 3363</param>
        public static IntPtr LoadAndRunVI(
            string viPath,
            ushort port = DefaultPort,
            bool openPanel = false,
            bool closePanel = true)
        {
            IntPtr viRef = LoadVI(viPath, port);
            RunVI(viRef, openPanel, closePanel);
            return viRef;
        }

        // ── 参数设置（Flatten To String 格式） ────────────────────────────

        /// <summary>
        /// 设置 VI 控件参数（手动指定 typeString 和 dataString）。
        /// 适用于自定义类型或需要精确控制格式的场景。
        /// </summary>
        /// <param name="viReference">VI 引用</param>
        /// <param name="parameterName">控件名称</param>
        /// <param name="typeString">类型描述符（由 <see cref="LvFlattenHelper"/> 构建）</param>
        /// <param name="dataString">展开数据字节流（由 <see cref="LvFlattenHelper"/> 编码）</param>
        /// <exception cref="InvalidOperationException">设置失败时抛出</exception>
        public static void SetVIParameter(
            IntPtr viReference, string parameterName,
            short[] typeString, byte[] dataString)
        {
            if (!LvLibWrapper.SafeSetParameter(
                    viReference, parameterName, typeString, dataString,
                    out _, out string err, out var exception))
                throw new InvalidOperationException($"设置参数 [{parameterName}] 失败：{err}", exception);
        }

        /// <summary>
        /// 设置 VI 控件参数（自动根据值类型编码）。
        /// 支持：<see langword="bool"/>、<see langword="int"/>、<see langword="uint"/>、
        /// <see langword="long"/>、<see langword="float"/>、<see langword="double"/>、
        /// <see langword="string"/>、<c>int[]</c>、<c>double[]</c>、<c>string[]</c>、
        /// <see cref="LvClusterValue"/>。
        /// </summary>
        /// <exception cref="NotSupportedException">值类型不受支持时抛出</exception>
        /// <exception cref="InvalidOperationException">设置失败时抛出</exception>
        public static void SetVIParameter(IntPtr viReference, string parameterName, object value)
        {
            var (ts, ds) = LvFlattenHelper.EncodeAuto(value);
            SetVIParameter(viReference, parameterName, ts, ds);
        }

        // ── 类型化设置方法 ────────────────────────────────────────────────

        /// <summary>设置布尔型控件参数。</summary>
        public static void SetBooleanParameter(IntPtr viRef, string name, bool value)
            => SetVIParameter(viRef, name,
                LvFlattenHelper.BooleanTypeString, LvFlattenHelper.Encode(value));

        /// <summary>设置 I32 整数控件参数。</summary>
        public static void SetInt32Parameter(IntPtr viRef, string name, int value)
            => SetVIParameter(viRef, name,
                LvFlattenHelper.I32TypeString, LvFlattenHelper.Encode(value));

        /// <summary>设置 I64 长整数控件参数。</summary>
        public static void SetInt64Parameter(IntPtr viRef, string name, long value)
            => SetVIParameter(viRef, name,
                LvFlattenHelper.I64TypeString, LvFlattenHelper.Encode(value));

        /// <summary>设置单精度浮点控件参数。</summary>
        public static void SetFloat32Parameter(IntPtr viRef, string name, float value)
            => SetVIParameter(viRef, name,
                LvFlattenHelper.Float32TypeString, LvFlattenHelper.Encode(value));

        /// <summary>设置双精度浮点控件参数。</summary>
        public static void SetDoubleParameter(IntPtr viRef, string name, double value)
            => SetVIParameter(viRef, name,
                LvFlattenHelper.Float64TypeString, LvFlattenHelper.Encode(value));

        /// <summary>设置字符串控件参数。</summary>
        public static void SetStringParameter(IntPtr viRef, string name, string value)
            => SetVIParameter(viRef, name,
                LvFlattenHelper.StringTypeString, LvFlattenHelper.Encode(value));

        /// <summary>设置一维整数数组控件参数。</summary>
        public static void SetInt32ArrayParameter(IntPtr viRef, string name, int[] values)
            => SetVIParameter(viRef, name,
                LvFlattenHelper.I32Array1DTypeString, LvFlattenHelper.EncodeArray(values));

        /// <summary>设置一维双精度数组控件参数。</summary>
        public static void SetDoubleArrayParameter(IntPtr viRef, string name, double[] values)
            => SetVIParameter(viRef, name,
                LvFlattenHelper.Float64Array1DTypeString, LvFlattenHelper.EncodeArray(values));

        /// <summary>设置一维字符串数组控件参数。</summary>
        public static void SetStringArrayParameter(IntPtr viRef, string name, string[] values)
            => SetVIParameter(viRef, name,
                LvFlattenHelper.StringArray1DTypeString, LvFlattenHelper.EncodeArray(values));

        /// <summary>
        /// 设置簇控件参数。
        /// 使用 <see cref="LvClusterValue.From"/> 快速构建：
        /// <code>SetClusterParameter(viRef, "Cluster1", LvClusterValue.From(42, 3.14, "OK"));</code>
        /// </summary>
        public static void SetClusterParameter(IntPtr viRef, string name, LvClusterValue cluster)
            => SetVIParameter(viRef, name, cluster);   // EncodeAuto 内部处理 LvClusterValue

        // ── 参数获取（Flatten To String 格式） ────────────────────────────

        /// <summary>
        /// 获取 VI 指示器参数，自动解码为 .NET 对象。
        /// 返回类型由 LabVIEW 类型描述符决定：
        /// <list type="bullet">
        ///   <item><c>bool</c> / <c>int</c> / <c>long</c> / <c>float</c> / <c>double</c> / <c>string</c></item>
        ///   <item><c>int[]</c> / <c>double[]</c> / <c>string[]</c></item>
        ///   <item><c>object?[]</c>（簇，字段顺序与 LabVIEW 一致）</item>
        ///   <item><c>byte[]</c>（未知类型的原始字节）</item>
        /// </list>
        /// </summary>
        /// <exception cref="InvalidOperationException">获取失败时抛出</exception>
        public static object? GetVIParameter(IntPtr viReference, string parameterName)
        {
            if (!LvLibWrapper.SafeGetParameter(
                    viReference, parameterName,
                    out _, out var ts, out var ds, out string err, out var exception))
                throw new InvalidOperationException($"获取参数 [{parameterName}] 失败：{err}", exception);

            return LvFlattenHelper.DecodeAuto(ts, ds);
        }

        /// <summary>
        /// 获取 VI 指示器参数的原始 Flatten 数据，用于自定义解码（如含命名字段的簇）。
        /// </summary>
        /// <returns>(typeString 类型描述符, dataString 展开字节流)</returns>
        /// <exception cref="InvalidOperationException">获取失败时抛出</exception>
        public static (short[] TypeString, byte[] DataString) GetVIParameterRaw(
            IntPtr viReference, string parameterName, CancellationToken ct = default, Action? checkAdmission = null)
        {
            if (!LvLibWrapper.SafeGetParameter(
                    viReference, parameterName,
                    out _, out var ts, out var ds, out string err, out var exception, ct: ct, checkAdmission: checkAdmission))
                throw new InvalidOperationException($"获取参数 [{parameterName}] 失败：{err}", exception);

            return (ts, ds);
        }

        // ── 类型化获取方法 ────────────────────────────────────────────────

        /// <summary>获取布尔型指示器参数。</summary>
        public static bool GetBooleanParameter(IntPtr viRef, string name)
        {
            var (_, ds) = GetVIParameterRaw(viRef, name);
            return LvFlattenHelper.DecodeBoolean(ds);
        }

        /// <summary>获取 I32 整数指示器参数。</summary>
        public static int GetInt32Parameter(IntPtr viRef, string name)
        {
            var (_, ds) = GetVIParameterRaw(viRef, name);
            return LvFlattenHelper.DecodeInt32(ds);
        }

        /// <summary>获取 I64 长整数指示器参数。</summary>
        public static long GetInt64Parameter(IntPtr viRef, string name)
        {
            var (_, ds) = GetVIParameterRaw(viRef, name);
            return LvFlattenHelper.DecodeInt64(ds);
        }

        /// <summary>获取单精度浮点指示器参数。</summary>
        public static float GetFloat32Parameter(IntPtr viRef, string name)
        {
            var (_, ds) = GetVIParameterRaw(viRef, name);
            return LvFlattenHelper.DecodeFloat32(ds);
        }

        /// <summary>获取双精度浮点指示器参数。</summary>
        public static double GetDoubleParameter(IntPtr viRef, string name)
        {
            var (_, ds) = GetVIParameterRaw(viRef, name);
            return LvFlattenHelper.DecodeFloat64(ds);
        }

        /// <summary>获取字符串指示器参数。</summary>
        public static string GetStringParameter(IntPtr viRef, string name)
        {
            var (_, ds) = GetVIParameterRaw(viRef, name);
            return LvFlattenHelper.DecodeString(ds);
        }

        /// <summary>获取一维整数数组指示器参数。</summary>
        public static int[] GetInt32ArrayParameter(IntPtr viRef, string name)
        {
            var (_, ds) = GetVIParameterRaw(viRef, name);
            return LvFlattenHelper.DecodeInt32Array(ds);
        }

        /// <summary>获取一维双精度数组指示器参数。</summary>
        public static double[] GetDoubleArrayParameter(IntPtr viRef, string name)
        {
            var (_, ds) = GetVIParameterRaw(viRef, name);
            return LvFlattenHelper.DecodeFloat64Array(ds);
        }

        /// <summary>获取一维字符串数组指示器参数。</summary>
        public static string[] GetStringArrayParameter(IntPtr viRef, string name)
        {
            var (_, ds) = GetVIParameterRaw(viRef, name);
            return LvFlattenHelper.DecodeStringArray(ds);
        }

        /// <summary>
        /// 获取簇指示器参数，返回各字段值数组（顺序与 LabVIEW 簇字段定义一致）。
        /// 若需要按字段名访问，请改用 <see cref="GetVIParameterRaw"/> 后自行映射。
        /// </summary>
        public static object?[] GetClusterParameter(IntPtr viRef, string name)
        {
            var (ts, ds) = GetVIParameterRaw(viRef, name);
            return LvFlattenHelper.DecodeCluster(ts, ds);
        }

        // ── 完整 VI 调用流程 ──────────────────────────────────────────────

        /// <summary>
        /// 完整调用 VI：加载 → 设置输入参数 → 运行 → 读取输出结果。
        /// </summary>
        /// <param name="viPath">VI 文件路径</param>
        /// <param name="inputs">
        /// 输入参数字典（控件名 → 值）。
        /// 值类型支持：bool、int、uint、long、float、double、string、int[]、double[]、string[]、<see cref="LvClusterValue"/>。
        /// </param>
        /// <param name="outputNames">需要读取的指示器名称列表</param>
        /// <param name="port">LabVIEW 监听端口，默认 3363</param>
        /// <returns>
        /// 输出参数字典（指示器名 → 自动解码的 .NET 对象）。
        /// 值类型由 LabVIEW 类型描述符决定，簇返回 <c>object?[]</c>。
        /// </returns>
        public static Dictionary<string, object?> CallVI(
            string viPath,
            Dictionary<string, object> inputs,
            IEnumerable<string> outputNames,
            ushort port = DefaultPort)
        {
            IntPtr viRef = LoadVI(viPath, port);

            foreach (var (name, value) in inputs)
                SetVIParameter(viRef, name, value);

            RunVI(viRef, openPanel: false, closePanel: true);

            var outputs = new Dictionary<string, object?>();
            foreach (var name in outputNames)
                outputs[name] = GetVIParameter(viRef, name);

            return outputs;
        }

        // ── 私有辅助 ──────────────────────────────────────────────────────

        /// <summary>
        /// 从 LabVIEW Uint32Array Handle（指针的指针）读取 Pixmap 数据。
        /// </summary>
        private static PixmapData ReadPixmapData(IntPtr handle)
        {
            var empty = new PixmapData { Width = 0, Height = 0, PixelData = [] };

            if (handle == IntPtr.Zero) return empty;

            IntPtr dataPtr = Marshal.ReadIntPtr(handle);
            if (dataPtr == IntPtr.Zero) return empty;

            // 读取数组维度（前 2 个 int32：dimSizes[0]=height, dimSizes[1]=width）
            int[] dimSizes = new int[2];
            Marshal.Copy(dataPtr, dimSizes, 0, 2);

            int height = dimSizes[0];
            int width = dimSizes[1];
            int total = GetPixmapPixelCount(height, width);
            if (total == 0) return empty;

            // 像素数据紧跟 dimSizes 之后（偏移 8 字节 = 2 × int32）
            IntPtr pixelPtr = IntPtr.Add(dataPtr, 8);
            uint[] pixelData = new uint[total];
            // Marshal.Copy 按位拷贝 32 位像素；int[] 与 uint[] 具有相同 CLR 数组布局。
            Marshal.Copy(pixelPtr, (int[])(object)pixelData, 0, total);

            return new PixmapData { Width = width, Height = height, PixelData = pixelData };
        }

        internal static string ReadCString(byte[] buffer)
        {
            // 原生容量以字节计；中文 ANSI 多字节文本不能按 StringBuilder.Length 检查截断。
            int terminator = Array.IndexOf(buffer, (byte)0);
            if (terminator < 0 || terminator == buffer.Length - 1)
                throw new InvalidDataException("VI 元数据达到缓冲区上限，无法确认配置完整。");
            return LvAnsiText.Decode(buffer, terminator);
        }

        private static readonly UTF8Encoding JsonEncoding = new(false, true);

        internal static byte[] EncodeJsonCString(string value, int capacity)
        {
            if (capacity <= 1 || value.Contains('\0'))
                throw new ArgumentException("VI 端子 JSON 缓冲区无效或包含空字符。");
            int bytes = JsonEncoding.GetByteCount(value);
            if (bytes >= capacity - 1)
                throw new InvalidDataException("VI 端子 JSON 超过原生缓冲区容量，未提交请求。");
            var buffer = new byte[capacity];
            JsonEncoding.GetBytes(value, buffer);
            return buffer;
        }

        internal static string ReadJsonCString(byte[] buffer)
        {
            int terminator = Array.IndexOf(buffer, (byte)0);
            if (terminator < 0 || terminator == buffer.Length - 1)
                throw new InvalidDataException("VI 端子 JSON 达到缓冲区上限，无法确认配置完整。");
            return JsonEncoding.GetString(buffer, 0, terminator);
        }

        internal static int GetPixmapPixelCount(int height, int width)
        {
            const int maxPixels = 16 * 1024 * 1024;
            long pixels = (long)height * width;
            if (width < 0 || height < 0 || pixels > maxPixels)
                throw new InvalidDataException($"VI 面板图像尺寸无效或超出上限：{width} × {height}。");
            return (int)pixels;
        }

        internal static void ValidatePixmapRelease(int releasedBytes, IntPtr remainingHandle, int? expectedBytes)
        {
            if (remainingHandle != IntPtr.Zero || releasedBytes < 0 ||
                expectedBytes.HasValue && releasedBytes != expectedBytes.Value)
                throw new InvalidOperationException($"释放 VI 面板图像未确认：返回值={releasedBytes}，"
                    + $"预期字节数={expectedBytes?.ToString() ?? "未知"}，句柄已清零={remainingHandle == IntPtr.Zero}。");
        }
    }

    // ── 数据模型 ──────────────────────────────────────────────────────────────

    /// <summary>Pixmap（24-bit 像素图）数据</summary>
    public class PixmapData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public uint[] PixelData { get; set; } = [];
    }

    /// <summary>连接面板信息（控件/指示器名称 + VI 状态 + Pixmap）</summary>
    public class ConnectPanelInfo
    {
        public string ControlIn { get; set; } = string.Empty;
        public string IndicatorIn { get; set; } = string.Empty;
        public string ControlOut { get; set; } = string.Empty;
        public string IndicatorOut { get; set; } = string.Empty;
        /// <summary>VI 是否可重入</summary>
        public bool IsReentrant { get; set; }
        /// <summary>VI 当前运行状态</summary>
        public VIState VIState { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public uint[] PixelData { get; set; } = [];
    }
}
