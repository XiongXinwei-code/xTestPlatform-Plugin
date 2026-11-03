namespace CAN.Helpers;

internal static class CanPayloadProtectionDescription
{
    internal static string Parameters => ParametersFor("Protection");

    internal static string ParametersFor(string path)
    {
        var lines = MemberParameters.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            int start = lines[i].IndexOf('|'), end = start < 0 ? -1 : lines[i].IndexOf('|', start + 1);
            if (end < 0) continue;
            string names = lines[i][(start + 1)..end].Trim();
            if (!names.StartsWith("Rc", StringComparison.Ordinal) && !names.StartsWith("Crc", StringComparison.Ordinal)) continue;
            string qualified = string.Join(" / ", names.Split('/').Select(name => path + "." + name.Trim()));
            lines[i] = lines[i][..(start + 1)] + " " + qualified + " " + lines[i][end..];
        }
        return string.Join("\n", lines).Replace("Protection 字段", path + " 字段", StringComparison.Ordinal);
    }

    private const string MemberParameters = """

        Protection 字段（单次发送及周期每行分别配置；偏移从 0 开始，结束位置包含在内）：

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | RcEnabled / CrcEnabled | bool | 否 | false / false | 独立启用 RC / CRC，关闭时不改对应字节 |
        | RcStartByte / RcEndByte | int | 否 | 0 / 0 | RC 写入字节范围，1～4 字节，须在实际 Data 内 |
        | RcUseBitField / RcBitOffset / RcBitLength | bool/int/int | 否 | false / 0 / 4 | 可选单字节位域；LSB 为 bit0，保留其他业务位；启用时起止字节相同 |
        | RcByteOrder | 枚举 | 否 | LittleEndian | LittleEndian / BigEndian |
        | RcInitialValue | uint | 否 | 0 | 首帧 Counter，由用户配置；P1/P2 均可设为 0 或 1，也可填上下限内其他值；首帧直接使用，不提前累加 |
        | RcMinimum / RcMaximum | uint | 否 | 0 / 255 | 计数上下限，必须可由 RC 字段容纳 |
        | RcStep | long | 否 | 1 | 每次提交尝试后的增量，正数递增、负数递减 |
        | RcOverflow | 枚举 | 否 | Wrap | Wrap 按范围取模循环，Hold 保持边界，Error 在下次越界发送前报错 |
        | CrcStartByte / CrcEndByte | int | 否 | 7 / 7 | CRC 写入范围，长度须等于模型宽度/8，不得与 RC 重叠 |
        | CrcByteOrder | 枚举 | 否 | LittleEndian | CRC 结果存储顺序，独立于 REFIN/REFOUT |
        | CrcDataStartByte / CrcDataEndByte | int | 否 | 0 / -1 | 参与计算范围；-1 表示实际 Data 最后一个字节 |
        | CrcFieldTreatment | 枚举 | 否 | Skip | CRC 字段位于计算范围内时，Skip 跳过这些字节；Zero 用零代替 |
        | CrcDataIdEnabled | bool | 否 | false | 将 Data ID 加入 CRC 输入；CRC 关闭时忽略 |
        | CrcDataIdHex | string | 否 | 空 | 启用时填 1～64 字节 Hex 常量（非表达式），按输入顺序计算；连续 Hex 或完整字节用空白/逗号/短横线分隔，整体可带 0x 前缀 |
        | CrcDataIdPlacement | 枚举 | 否 | BeforeData | BeforeData：Data ID → 选定数据；AfterData：选定数据 → Data ID；数据范围和 Skip/Zero 先作用于报文字节 |
        | CrcDataIdMode | 枚举 | 否 | Fixed | Fixed 保留原 Hex/前后拼接；Profile2 / Profile1Both / Profile1Alt / Profile1Low / Profile1Nibble 自动采用对应 CRC 模型 |
        | CrcDataIdUseProfilePlacement | bool | 否 | true | P1/P2 默认按标准顺序（P1前置/P2后置）；显式关闭后使用 CrcDataIdPlacement 自选前/后，属于自定义 CRC 输入，不再强制标准 Profile 顺序 |
        | CrcDataIdTable | string[16] | P2 时 | 16 项空值 | Counter 0～15 对应的 16 个 Hex 字节；只把当前项追加到 CRC 输入末尾，不写入报文；不得用猜测的表替代设备规范 |
        | CrcDataIdTableUseExpression | bool | 否 | false | P2表来源；默认使用内联表，true使用下述表达式。保留内联表但不作为失败回退 |
        | CrcDataIdTableExpression | string([ExpressionField]) | 表表达式启用时 | 空 | 求值结果为byte[]，必须恰好16字节；单次操作准备时或周期启动时解析、复制冻结，周期运行中不换表；实际表及SHA256记入日志 |
        | CrcProfile1DataIdHex | string | P1 时 | 0000 | Hex 数值（非字节串）：BOTH/ALT≤FFFF，LOW≤FF，NIBBLE≤FFF；BOTH 前置低/高字节，ALT 偶低奇高，LOW 仅低字节，NIBBLE 前置低字节和 00 |
        | CrcCounterByte / CrcCounterBitOffset | int/int | P1/P2 时 | 1 / 0 | 从当前帧此处的四位 Counter 选择 Data ID；位偏移只可 0 或 4，生成 RC 时须与 RC 位域一致；接收始终使用实收 Counter |
        | CrcDataIdNibbleByte / CrcDataIdNibbleBitOffset | int/int | NIBBLE 时 | 1 / 4 | 显式发送 Data ID 高字节的低四位，位置不得与 CRC/Counter 重叠；该字段包含在 CRC 计算范围中 |
        | CrcAlgorithm | 枚举 | 否 | Crc8 | Crc8(SMBUS)、Crc8SaeJ1850、Crc8Autosar、Crc8MaximDow、Crc8Rohc、Crc16Arc、Crc16Modbus、Crc16CcittFalse、Crc16X25、Crc16Xmodem、Crc16Kermit、Crc16Usb、Crc16Dnp、Crc32IsoHdlc、Crc32C、Crc32Mpeg2、Crc32Bzip2、Crc32Jamcrc、Crc64Ecma182、Crc64Xz、Custom |
        | CrcName | string | 否 | Custom | 自定义模型名称，仅作标识 |
        | CrcWidth | int | 否 | 8 | Custom 宽度，可选 8、16、32、64 |
        | CrcPolynomial | string | 否 | 0x07 | Custom POLY，十六进制、可带 0x，采用正向多项式（省略最高次项），MODBUS 填 8005 |
        | CrcInitialValue | string | 否 | 0x00 | Custom INIT，十六进制 |
        | CrcXorOutput | string | 否 | 0x00 | Custom XOROUT，十六进制 |
        | CrcReflectInput / CrcReflectOutput | bool | 否 | false / false | Custom REFIN / REFOUT；预设使用其固定参数 |

        QLP YSJ 0235-2024 配置示例（§4.2.4、§4.2.5、表 3）：

        - RC 起止字节均为 1，启用单字节位域，位起点 0、位宽 4；初值 0、下限 0、上限 15、步长 1、Wrap。Byte1 高四位保留业务数据，例如原值 A0 对应 A0、A1…AF、A0，而不是 Data ID 与 RC 相加。
        - CRC 起止字节均为 0，计算范围从字节 1 到最后一个字节；启用 Data ID、选择 Profile2、保留标准拼接顺序（数据后追加一个隐式 Data ID）。Counter 来源字节 1、位起点 0，与 RC 位域一致；CRC 自动使用 2F / FF / FF，无反射。
        - 每个报文填写通信矩阵指定的 16 项 Data ID 表。Data ID 不写入报文、不增加长度。表 3 的 01…10 只是 CRC 计算方法示例，不能作为实际设备默认表；表头 Data+Counter 表示业务数据与 Counter 共用 Byte1。

        """;

    internal const string Behavior = """
        - 先写 RC 与启用的显式 Data ID 字段，再计算 CRC；只处理本次 Data 的副本，不修改表达式变量或原设置。
        - 计算范围不包含 CAN ID、帧头或适配器后续 FD DLC 补齐字节；需要补零参与 CRC 时请在 Data 中明确提供。
        - Fixed/P2/BOTH/ALT/LOW 的 Data ID 仅拼入 CRC 输入，不增加 DLC、不自动取 CAN ID；P1 NIBBLE 另外写入配置的四位显式字段。所有输入只初始化和最终异或一次。
        - P1 自动使用 Poly=1D/Init=00/XorOut=00，Counter 0～14；P2 自动使用 2F/FF/FF，Counter 0～15。两者都无反射，强制 Skip；原算法和固定 Data ID 字段保留但不参与这两种模式。
        - 标准拼接顺序不是可任意互换的协议选项；默认 P1 前置、P2 后置。需要私有协议时，显式关闭 CrcDataIdUseProfilePlacement 后选前/后；只移动隐式 Data ID 输入，NIBBLE 的显式字段仍在配置的报文位置。
        - RcInitialValue 由用户指定首帧 Counter，P1/P2 均允许 0 或 1；不根据 Profile 强制覆盖。P2 首帧为 0 使用表[0]，为 1 使用表[1]，提交后才按 RcStep 推进。应用 Profile 布局保留初值；先填 RC/显式 NIBBLE 再计算 CRC，未启用 RC 时使用数据已有 Counter。DBC 接收另输出 dataIdValid、counterValid、protectionValid，坏帧仍保留原字节。
        - 本功能是可配置的 CRC/Data ID 组合，不包含完整 E2E 重复帧/丢帧/超时状态机；字段布局、完整计算范围和报文长度仍应按设备规范设置。
        - Data ID 表默认内联。表达式模式允许使用如 FileGlobals.DataIds 的 byte[] 变量；解析失败时拒绝操作，不回退旧表。周期任务只使用启动时的冻结表，之后修改变量须停止并重新启动才生效。
        - 排队取消不消耗 RC；一旦进入驱动提交尝试即消耗，失败或取消不回滚、不自动重发；完成仍只表示驱动提交。
        - 配置越界、重叠、模型宽度不匹配或动态 Data 变短导致范围失效时，拒绝本次提交并报告错误。
        """;
}
