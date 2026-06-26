using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using xTestPlatform.Core.SequenceModels;

namespace LabVIEWCallPlugin.UI.Helper
{
    public static class LabVIEWTypeConverter
    {
        // 基础类型字典
        private static readonly Dictionary<string, VariableDataType> BaseTypeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Boolean"] = VariableDataType.Bool,
            ["I8"] = VariableDataType.SByte,
            ["U8"] = VariableDataType.Byte,
            ["I16"] = VariableDataType.Short,
            ["U16"] = VariableDataType.UShort,
            ["I32"] = VariableDataType.Int,
            ["U32"] = VariableDataType.UInt,
            ["I64"] = VariableDataType.Long,
            ["U64"] = VariableDataType.ULong,
            ["Single Float"] = VariableDataType.Float,
            ["Double Float"] = VariableDataType.Double,
            ["String"] = VariableDataType.String,
            ["Cluster"] = VariableDataType.Struct,
            ["Variant"] = VariableDataType.Dynamic,
        };

        // List 元素类型映射（基础类型 → List 枚举值）
        private static readonly Dictionary<string, VariableDataType> ListTypeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Boolean"] = VariableDataType.ListBool,
            ["Int32"] = VariableDataType.ListInt,
            ["I32"] = VariableDataType.ListInt,
            ["Int64"] = VariableDataType.ListLong,
            ["I64"] = VariableDataType.ListLong,
            ["Single Float"] = VariableDataType.ListFloat,
            ["Double Float"] = VariableDataType.ListDouble,
            ["String"] = VariableDataType.ListString,
            ["Byte"] = VariableDataType.ListByte,
            ["Dynamic"] = VariableDataType.ListDynamic,
        };

        // Matrix 元素类型映射（基础类型 → Matrix 枚举值）
        private static readonly Dictionary<string, VariableDataType> MatrixTypeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Boolean"] = VariableDataType.MatrixBool,
            ["Int32"] = VariableDataType.MatrixInt,
            ["I32"] = VariableDataType.MatrixInt,
            ["Int64"] = VariableDataType.MatrixLong,
            ["I64"] = VariableDataType.MatrixLong,
            ["Single Float"] = VariableDataType.MatrixFloat,
            ["Double Float"] = VariableDataType.MatrixDouble,
            ["String"] = VariableDataType.MatrixString,
        };

        /// <summary>
        /// 将 LabVIEW 数据类型字符串转换为平台 VariableDataType 枚举
        /// </summary>
        public static VariableDataType Convert(string labviewType)
        {
            if (string.IsNullOrWhiteSpace(labviewType))
                return VariableDataType.Dynamic;

            string type = labviewType.Trim();

            // 1. 处理枚举类型（Enum I16, Enum U32 等）
            if (type.StartsWith("Enum", StringComparison.OrdinalIgnoreCase))
                return VariableDataType.Enum;

            // 2. 处理二维数组（2D Array of ...）
            var matrixMatch = Regex.Match(type, @"^2D\s*Array\s+of\s+(.+)$", RegexOptions.IgnoreCase);
            if (matrixMatch.Success)
            {
                string elementType = matrixMatch.Groups[1].Value.Trim();
                if (MatrixTypeMap.TryGetValue(elementType, out var matrixType))
                    return matrixType;
                return VariableDataType.Matrix; // 通用矩阵兜底
            }

            // 3. 处理一维数组（1D Array of ... 或 Array of ...）
            var arrayMatch = Regex.Match(type, @"^(?:1D\s*)?Array\s+of\s+(.+)$", RegexOptions.IgnoreCase);
            if (arrayMatch.Success)
            {
                string elementType = arrayMatch.Groups[1].Value.Trim();
                if (ListTypeMap.TryGetValue(elementType, out var listType))
                    return listType;
                // 对于未列出的元素类型，尝试映射基础类型，若无则用 Dynamic
                if (BaseTypeMap.TryGetValue(elementType, out var baseType))
                    return MapBaseTypeToGenericList(baseType);
                return VariableDataType.ListDynamic;
            }

            // 4. 处理基础标量类型
            if (BaseTypeMap.TryGetValue(type, out var scalarType))
                return scalarType;

            // 5. 无法识别，返回 Dynamic
            return VariableDataType.Dynamic;
        }

        // 辅助：将基础类型映射到对应的 List 枚举（尽可能精确）
        private static VariableDataType MapBaseTypeToGenericList(VariableDataType baseType)
        {
            return baseType switch
            {
                VariableDataType.Bool => VariableDataType.ListBool,
                VariableDataType.Int => VariableDataType.ListInt,
                VariableDataType.Long => VariableDataType.ListLong,
                VariableDataType.Float => VariableDataType.ListFloat,
                VariableDataType.Double => VariableDataType.ListDouble,
                VariableDataType.String => VariableDataType.ListString,
                VariableDataType.Byte => VariableDataType.ListByte,
                VariableDataType.Dynamic => VariableDataType.ListDynamic,
                _ => VariableDataType.ListDynamic
            };
        }

        public static string ConvertToString(string labviewType)
        {
            return Convert(labviewType).ToString(); // 直接输出枚举名，如 "Int", "Double" 等
        }

    }
}
