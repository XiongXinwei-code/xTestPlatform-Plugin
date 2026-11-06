/*******************************************************************************
*                                   ZMC
*                       ----------------------------
*                          ZHIYUAN Machine Control
*
* Copyright (c) 2001-2020 Guangzhou ZHIYUAN Electronics Co., Ltd.
* All rights reserved.
*
* Contact information:
* web site:    http://www.zlg.cn/
*******************************************************************************/
 
/*----------------------------------------------------------------------------*/
/**
 * @file
 * @brief PCIe-XX系列API
 *
 * @internal
 * @par Modification history
 * - 1.00 2023-5-29  ZBL, 初次实现
 * @endinternal
 */
/*----------------------------------------------------------------------------*/
#ifndef ZMC_PCI_ZECM_H_
#define ZMC_PCI_ZECM_H_

#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <stdbool.h>
#include "pci_errno.h"

#ifdef __cplusplus
extern "C" {
#endif

#ifndef IN
#define IN
#endif

#ifndef OUT
#define OUT
#endif

#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#else
#define EXPORT
#endif

typedef void* BOARD_HANDLE;
typedef void* ECAT_HANDLE;

typedef bool(*PFN_PROGRESS_CALLBACK)(void *pvUser, float fRatio);

typedef void(*PFN_PI_ASYNC_MODE_CYCLE_CALLBACK)(void *context, ECAT_HANDLE hHandle);

/** @brief  驱动信息  */
typedef struct
{
    char            caDriverVersion[32];            //!< 驱动版本信息
    char            caApiVersion[32];               //!< 动态库的API版本
    uint32_t        u32BoardCnt;                    //!< 板卡的数量
}DRIVER_INFO;

/** @brief  板卡信息  */
typedef struct
{
    char            cName[32];                      //!< 板卡的名称
    char            cVersion[32];                   //!< 板卡的版本号
    char            cSerialNo[32];                  //!< 板卡的序列号，可用于打开板卡或通道
    uint32_t        u32Alias;                       //!< 板卡的别名，可用于打开板卡或通道，如BOARD_ALIAS(name, 10)
    uint32_t        u32EcatChannelCnt;              //!< EtherCAT通道个数
    uint32_t        u32FpgaVersion;                 //!< FPGA版本
    uint32_t        u32Res[4];                      //!< 保留
}BOARD_INFO;

/** @brief  ECAT信息  */
typedef struct
{
    uint32_t        u32PiQueueNum;                  //!< PI镜像队列的深度
    uint32_t        u32Res[4];                      //!< 保留
}ECAT_INFO;

/** @brief  ENI信息  */
typedef struct
{
    uint32_t        u32MainCycle;                   //!< 主循环周期，PDO周期
    uint32_t        u32MailboxCycle;                //!< 邮箱周期
    uint32_t        u32PIOutputByteSize;            //!< 镜像输出有效字节大小
    uint32_t        u32PIInputByteSize;             //!< 镜像输入有效字节大小
}ECAT_ENI_INFO;

/** @brief  ENI扩展信息  */
typedef struct
{
    ECAT_ENI_INFO   tBase;                          //!< 基础信息
    uint8_t         u8Crc[64];                      //!< CRC校验和
    uint8_t         u8Res[64];                      //!< 保留
}ECAT_ENI_EX_INFO;

/** @brief  PI镜像索引定义  */
enum
{
    PI_AREA_LOCAL_OUTPUT = 0,                       //!< 镜像本地输出缓存，可读写，非线程安全，同步控制
    PI_AREA_LOCAL_INPUT,                            //!< 镜像本地输入缓存，可读写，非线程安全，同步控制
    PI_AREA_FAST_OUTPUT,                            //!< 快速输出缓存，可读写，进程安全，异步控制
    PI_AREA_SHADOW_OUTPUT,                          //!< 镜像影子输出缓存，只读，进程安全，实时控制
    PI_AREA_SHADOW_INPUT,                           //!< 镜像影子输入缓存，只读，进程安全，异步控制
    PI_AREA_NUM,
};

/** @brief  PI异步控制模式  */
typedef enum{
    ECAT_PI_ASYNC_MODE_DIRECT_ACCESS = 0,           //!< 调用PIRead/PIWrite时，数据直接同步到板卡
    ECAT_PI_ASYNC_MODE_CYCLE_ACCESS,                //!< 调用PIRead/PIWrite时，数据写到缓存，再周期同步到板卡
    ECAT_PI_ASYNC_MODE_MANUAL_ACCESS,               //!< 调用PIRead/PIWrite时，数据写到缓存，
                                                    //!<   直到用户调用EcatPIASyncUpdateData数据才同步到板卡
}ECAT_PI_ASYNC_MODE;

/** @brief  PI镜像初始化模式  */
typedef enum {
    ECAT_PI_REINIT_AUTO = 0,                        //!< 自动模式，先根据本地模式与全局模式（本地>全局），
                                                    //!< 最后确定复位行为，如果还是AUTO则等价于ECAT_PI_REINIT_ALL。
    ECAT_PI_REINIT_ALL,                             //!< 清空全部缓存
    ECAT_PI_REINIT_EXCEPT_ASYNC_AREA,               //!< 仅保留异步缓存（PI_AREA_FAST_OUTPUT、PI_AREA_SHADOW_INPUT）的数据
    ECAT_PI_REINIT_EXCEPT_SYNC_AREA,                //!< 仅保留同步缓存（PI_AREA_LOCAL_OUTPUT、PI_AREA_LOCAL_INPUT）的数据
    ECAT_PI_REINIT_RESERVE_ALL,                     //!< 保留同步和异步缓存（PI_AREA_FAST_OUTPUT、PI_AREA_SHADOW_INPUT、PI_AREA_LOCAL_OUTPUT、PI_AREA_LOCAL_INPUT）的数据
}ECAT_PI_REINIT_MODE;

/** @brief  主从站状态标志  */
typedef enum {
    EcatStateNotSet = 0,                            //!< 未设置，从站离线
    EcatStateI = 1,                                 //!< 初始化状态
    EcatStateP = 2,                                 //!< 预操作状态
    EcatStateB = 3,                                 //!< 引导状态
    EcatStateS = 4,                                 //!< 安全操作状态
    EcatStateO = 8,                                 //!< 操作状态
    EcatStateMask = 15,                             //!< 状态掩码
    EcatStateErrorFlag = 16,                        //!< 状态错误标志位
}EcatState;

/** @brief ECAT总线状态状态   */
typedef enum
{
    //! 主站状态正常
    EcatMasterBusStatusOK                         = 0x00000000,
 
    //! 检测到不可热插拔的从站状态错误
    EcatMasterBusStatusWrongSlaveState            = 0x00000001,
    //! 检测到不可热插拔的从站掉线
    EcatMasterBusStatusSlaveIsOffline             = 0x00000002,
    //! 检测到网络掉线
    EcatMasterBusStatusNetLinkDown                = 0x00000004,   
 
    //! 检测到可热插拔的从站状态错误
    EcatMasterBusStatusWrongHotConnectSlaveState  = 0x00010000,
    //! 检测到可热插拔的从站掉线
    EcatMasterBusStatusHotConnectSlaveIsOffline   = 0x00020000,
 
    //! 错误掩码
    EcatMasterBusStatusErrorMask                  = 0x0000FFFF,
    //! 警告掩码
    EcatMasterBusStatusWarningMask                = 0xFFFF0000,

} EcatBusStatusFlag;

/** @brief ECAT看门狗类型   */
typedef enum {
    ECAT_WATCHDOG_IO_DATA = 0,
    ECAT_WATCHDOG_NUM,
}ECAT_WATCHDOG_TYPE;

/** @brief ECAT FORCE TYPE */
typedef enum{
    ECAT_FORCE_OUTPUT_DISABLE = 0,
    ECAT_FORCE_OUTPUT_SET_WITHOUT_MASK,
    ECAT_FORCE_OUTPUT_SET_WITH_MASK,
    ECAT_FORCE_OUTPUT_NUM,
}ECAT_FORCE_OUTPUT_TYPE;

/** @brief 从站信息   */
typedef struct {
    uint32_t        u32Vendor;                    //!< 厂商ID
    uint32_t        u32ProductCode;               //!< 产品代码
    uint32_t        u32SN;                        //!< 序列号
    uint32_t        u32Revision;                  //!< 版本
    uint32_t        u32DevType;                   //!< 设备类型，未使用，固定为0
    uint32_t        u32Alias;                     //!< 别名
    uint32_t        u32PIOutputsBitSize;          //!< 从站过程镜像的输出位大小，主站生产，从站消费
    uint32_t        u32PIInputsBitSize;           //!< 从站过程镜像的输入位大小，主站消费，从站生产
    uint32_t        u32Res[6];                    //!< 保留
} ECAT_SLAVE_INFO;

/** @brief 板卡类型 */
typedef enum{
    ECAT_PCIe_2E = 0,
    ECAT_PCIe_4E,
    ECAT_MiniPCIe_2E,
    ECAT_PCIe_1E16,
    ECAT_Unknow_Type,
} BOARD_TYPE;

/*******************************************************************************
  驱动接口
*******************************************************************************/

/**
  @brief  获取驱动信息
  
  @param[out]  ptDriverInfo     输出驱动信息
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t DriverGetInfo(OUT DRIVER_INFO *ptDriverInfo);

/**
  @brief  枚举板卡的信息
  
  @param       u32BoardIdx      板卡的索引，范围：0~DRIVER_INFO::u32BoardCnt-1
  @param[out]  ptBoardInfo      输出板卡信息
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t DriverEnumBoard(IN uint32_t u32BoardIdx, OUT BOARD_INFO *ptBoardInfo);

/**
  @brief  枚举板卡ECAT通道的信息
  
  @param       u32BoardIdx      板卡的索引，有效范围：0~DRIVER_INFO::u32BoardCnt-1
  @param       u32EcatChannel   板卡EtherCAT的通道号，有效范围：0~BOARD_INFO::u32EcatChannelCnt-1
  @param[out]  ptEcatInfo       输出EtherCAT通道的信息
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t DriverEnumEcat(IN uint32_t u32BoardIdx, IN uint32_t u32EcatChannel, OUT ECAT_INFO *ptEcatInfo);

/**
  @brief  获取错误码对应的错误信息
  
  @param  err_code              错误码
  @return  返回对应的字符串
*/
EXPORT const char *DriverGetErrStr(uint32_t err_code);

/**
  @brief  内存快速拷贝
  
  @param  destination       目标地址，必须为有效地址
  @param  source            源地址，必须为有效地址
  @param  size              拷贝内存大小
  @return  返回destination指针
*/
EXPORT void* DriverFastMemcpy(void *destination, const void *source, size_t size);

/*******************************************************************************
  板卡接口
*******************************************************************************/
#define BOARD_IDX(buff, index)      (sprintf(buff, "ID%d", (int)index), buff)
#define BOARD_ALIAS(buff, alias)    (sprintf(buff, "AL%d", (int)alias), buff)

/**
  @brief  打开指定的板卡
  
  @param[out]  phHandle         返回板卡的控制句柄
  @param       sBoardIf         板卡接口字符串，可以是
                                    ID<Board Index>     索引，BOARD_IDX(0)
                                    AL<Board Alias>     别名，BOARD_ALIAS(0)
                                    NOxxxxxx            序列号
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardOpen(OUT BOARD_HANDLE *phHandle, IN const char* sBoardIf);

/**
  @brief  返回当前板卡的信息
  
  @param  hHandle               板卡的控制句柄
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardClose(IN BOARD_HANDLE hHandle);

/**
  @brief  设置通信超时间时间，默认为20000
  
  @param  hHandle               板卡的控制句柄
  @param  timeout_ms            超时时间，毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardSetTimeout(IN BOARD_HANDLE hHandle, uint32_t timeout_ms);

/**
  @brief  返回当前板卡是否活着
  
  @param       hHandle          板卡的控制句柄
  @return  返回true表示活着，false表示死亡
*/
EXPORT bool BoardIsAlive(IN BOARD_HANDLE hHandle);

/**
 * @brief 返回当前板卡连接的心跳计数
 * @param hHandle              板卡的控制句柄
 * @param coreIndex            通道号
 * @param result               读取的结果
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
 */
EXPORT uint32_t BoardHeardCnt(IN BOARD_HANDLE hHandle, uint32_t coreIndex, uint32_t *result);


/**
  @brief  返回当前板卡的信息
  
  @param       hHandle          板卡的控制句柄
  @param[out]  ptBoardInfo      输出板卡信息
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardGetInfo(IN BOARD_HANDLE hHandle, OUT BOARD_INFO *ptBoardInfo);

/**
  @brief  返回当前板卡的alias
  
  @param       hHandle          板卡的控制句柄
  @param[out]  pu32Alias      输出alias
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardGetInfo_Alias(IN BOARD_HANDLE hHandle, OUT uint32_t* pu32Alias);

/**
  @brief  复位板卡
  
  @param  hHandle               板卡的控制句柄
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardReset(IN BOARD_HANDLE hHandle);

/**
  @brief  写板卡文件
  
  @param  hHandle               板卡的控制句柄
  @param  psLocalFileName       本地文件系统的文件名
  @param  psDeviceFileName      设备文件系统的文件名
  @param  pfnPropfnCallback     进度回调，可为空
  @param  pvUser                进度回调的第一个参数，可为空
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardFileDownload(
    IN BOARD_HANDLE             hHandle,
    IN const char              *psLocalFileName,
    IN const char              *psDeviceFileName,
    IN PFN_PROGRESS_CALLBACK    pfnPropfnCallback,
    IN void                    *pvUser);

/**
  @brief  读板卡文件
  
  @param  hHandle               板卡的控制句柄
  @param  psLocalFileName       本地文件系统的文件名
  @param  psDeviceFileName      设备文件系统的文件名
  @param  pfnPropfnCallback     进度回调，可为空
  @param  pvUser                进度回调的第一个参数，可为空
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardFileUpload(
    IN BOARD_HANDLE             hHandle,
    IN const char              *psLocalFileName,
    IN const char              *psDeviceFileName,
    IN PFN_PROGRESS_CALLBACK    pfnPropfnCallback,
    IN void                    *pvUser);


/**
  @brief  更新板卡文件
  
  @param  hHandle               板卡的控制句柄
  @param  psLocalFileName       本地文件系统的文件名
  @return  返回0表示成功，非0表示失败
*/
EXPORT int32_t BoardUpdate(
    IN BOARD_HANDLE             hHandle,
    IN const char              *psLocalFileName);

/**
  @brief  更新板卡文件
  
  @param  hHandle               板卡的控制句柄
  @param  psLocalFileName       本地文件系统的文件名
  @param  prop_callback         进度回调函数
  @param  user                  回调函数的用户句柄
  @return  返回0表示成功，非0表示失败
*/
EXPORT int32_t BoardUpdateEx(
    IN BOARD_HANDLE             hHandle,
    IN const char              *psLocalFileName,
    PFN_PROGRESS_CALLBACK prop_callback, 
    void *user);

/*************** IO Module ******************/
/**
 * @brief 读DO输出
 * @param       hHandle   板卡的控制句柄
 * @param[out]  u32DO     当前DO输出值，[15:0]：DO15~0，[31:16]：保留
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardDORead(IN BOARD_HANDLE hHandle, OUT uint32_t* u32DO);
/**
 * @brief 写DO输出
 * @param hHandle         板卡的控制句柄
 * @param u32DO           配置DO输出值，[15:0]：DO15~0，[31:16]：保留
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardDOWrite(IN BOARD_HANDLE hHandle, IN uint32_t u32DO);
/**
 * @brief 读DI输入
 * @param hHandle         板卡的控制句柄
 * @param[out] u32DI      当前DI输出值，[15:0]：DI15~0，[31:16]：保留
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardDIRead(IN BOARD_HANDLE hHandle, OUT uint32_t* u32DI);

/**
 *  @brief 锁存模式 
*/
enum {
  // 单次锁存模式
  IO_LATCH_Z_RAISING_TO_POS_A = 1,
  IO_LATCH_Z_FALLING_TO_POS_A,
  
  IO_LATCH_R0_RAISING_TO_POS_A = 3,
  IO_LATCH_R0_FALLING_TO_POS_A,
  
  IO_LATCH_R0_RAISING_TO_POS_A_AND_Z_RAISING_TO_POS_B = 6,
  IO_LATCH_R0_RAISING_TO_POS_A_AND_Z_FALLING_TO_POS_B,
  IO_LATCH_R0_FALLING_TO_POS_A_AND_Z_RAISING_TO_POS_B,
  IO_LATCH_R0_FALLING_TO_POS_A_AND_Z_FALLING_TO_POS_B,
  
  IO_LATCH_R0_RAISING_TO_POS_A_AND_R1_RAISING_TO_POS_B = 10,
  IO_LATCH_R0_RAISING_TO_POS_A_AND_R1_FALLING_TO_POS_B,
  IO_LATCH_R0_FALLING_TO_POS_A_AND_R1_RAISING_TO_POS_B,
  IO_LATCH_R0_FALLING_TO_POS_A_AND_R1_FALLING_TO_POS_B,
  
  IO_LATCH_R1_RAISING_TO_POS_B = 14,
  IO_LATCH_R1_FALLING_TO_POS_B,

  IO_LATCH_Z_RAISING_TO_POS_B = 16,
  IO_LATCH_Z_FALLING_TO_POS_B,

  IO_LATCH_R2_RAISING_TO_POS_C = 18,
  IO_LATCH_R2_FALLING_TO_POS_C,

  IO_LATCH_R3_RAISING_TO_POS_D = 20,
  IO_LATCH_R3_FALLING_TO_POS_D,

  IO_LATCH_MAX,

  // 连续锁存模式
  IO_SEQ_LATCH_Z_RAISING_TO_POS_A = 101,
  IO_SEQ_LATCH_Z_FALLING_TO_POS_A,
  
  IO_SEQ_LATCH_R0_RAISING_TO_POS_A = 103,
  IO_SEQ_LATCH_R0_FALLING_TO_POS_A,

  IO_SEQ_LATCH_R1_RAISING_TO_POS_B = 114,
  IO_SEQ_LATCH_R1_FALLING_TO_POS_B,

  IO_SEQ_LATCH_Z_RAISING_TO_POS_B = 116,
  IO_SEQ_LATCH_Z_FALLING_TO_POS_B,

  IO_SEQ_LATCH_R2_RAISING_TO_POS_C = 118,
  IO_SEQ_LATCH_R2_FALLING_TO_POS_C,

  IO_SEQ_LATCH_R3_RAISING_TO_POS_D = 120,
  IO_SEQ_LATCH_R3_FALLING_TO_POS_D,

  IO_SEQ_LATCH_R0_RAISING_TO_POS_B = 123,
  IO_SEQ_LATCH_R0_FALLING_TO_POS_B,

  IO_SEQ_LATCH_R0_RAISING_THEN_FALLING_TO_POS_A = 133,
  IO_SEQ_LATCH_R0_FALLING_THEN_RAISING_TO_POS_A,

  IO_SEQ_LATCH_R1_RAISING_THEN_FALLING_TO_POS_B = 135,
  IO_SEQ_LATCH_R1_FALLING_THEN_RAISING_TO_POS_B,

  IO_SEQ_LATCH_MAX,
};

/**
 * @brief 开启锁存功能
 * @param hHandle         板卡的控制句柄
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardLatchEnable(IN BOARD_HANDLE hHandle);
/**
 * @brief 关闭锁存功能
 * @param hHandle         板卡的控制句柄
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardLatchDisable(IN BOARD_HANDLE hHandle);
/**
 * @brief 锁存状态清零
 * @param hHandle           板卡的控制句柄
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardLatchClear(IN BOARD_HANDLE hHandle);
/**
 * @brief 编码器计数器清零
 * @param hHandle           板卡的控制句柄
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardLatchCounterReset(IN BOARD_HANDLE hHandle);
/**
 * @brief 配置锁存引脚映射
 * @param hHandle         板卡的控制句柄
 * @param u32PortSel      配置锁存引脚映射，寄存器映射为：
 *                            [1:0]: R0映射
 *                            [3:2]: R1映射
 *                            [5:4]: R2映射
 *                            [7:6]: R3映射
 *                            [31:8]：保留
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardLatchPortSel(IN BOARD_HANDLE hHandle, IN uint32_t u32PortSel);
/**
 * @brief 配置单次锁存功能
 * @param hHandle         板卡的控制句柄
 * @param u32LatchMode    配置单次锁存模式（1~21）
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardLatchCfg(IN BOARD_HANDLE hHandle, IN uint32_t u32LatchMode);
/**
 * @brief 切换单次锁存运行状态，开启一次锁存
 * @param hHandle         板卡的控制句柄
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardLatchRun(IN BOARD_HANDLE hHandle);
/**
 * @brief 读取单次锁存编码器值
 * @param       hHandle     板卡的控制句柄
 * @param       u32Channel  锁存通道（0~3：A~D）
 * @param[out]  u32PosL     锁存编码器值低32位
 * @param[out]  u32PosH     锁存编码器值高32位
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardLatchOnRead(IN BOARD_HANDLE hHandle, IN uint32_t u32Channel, OUT uint32_t* u32PosL, OUT uint32_t* u32PosH);
/**
 * @brief 配置连续锁存功能
 * @param hHandle             板卡的控制句柄
 * @param u32SeqLatchMode     配置连续锁存模式（101~136）
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardSeqLatchCfg(IN BOARD_HANDLE hHandle, IN uint32_t u32SeqLatchMode);
/**
 * @brief 切换连续锁存运行状态，开启锁存
 * @param hHandle                 板卡的控制句柄
 * @param u32LatchSeqQueueLength  连续锁存次数（0~1023）(输入0时可触发但不会记录锁存值)
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardSeqLatchRun(IN BOARD_HANDLE hHandle, IN uint32_t u32LatchSeqQueueLength);
/**
 * @brief 读取连续锁存编码器值
 * @param       hHandle       板卡的控制句柄
 * @param       u32Channel    锁存通道（0~3：A~D）
 * @param       u32SeqIndex   读取的连续锁存值索引
 * @param[out]  u32PosL       锁存编码器值低32位
 * @param[out]  u32PosH       锁存编码器值高32位
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardSeqLatchOnRead(IN BOARD_HANDLE hHandle, IN uint32_t u32Channel, IN uint32_t u32SeqIndex, OUT uint32_t* u32PosL, OUT uint32_t* u32PosH);
/**
 * @brief 读取锁存状态
 * @param       hHandle         板卡的控制句柄
 * @param       u32Channel      锁存通道（0~3：A~D）
 * @param[out]  u32LatchState   指定通道锁存状态，1：已触发锁存，0：未触发
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardLatchState(IN BOARD_HANDLE hHandle, IN uint32_t u32Channel, OUT uint32_t* u32LatchState);
/**
 * @brief 切换锁存运行状态，进入空闲状态
 * @param hHandle         板卡的控制句柄
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardLatchStop(IN BOARD_HANDLE hHandle);

/**
 * @brief 脉冲轴输出模块
*/
/**
 * @brief 配置脉冲控制输出（开启功能复用）
 * @param hHandle         板卡的控制句柄
 * @param u32Channel      通道号（0-3）
 * @param u32Mode         脉冲输出模式:
 *                        0 AB相
 *                        1 正/逆脉冲序列
 *                        2 脉冲+符号（servo_sign == !(u32Sign)）
 *                        3 脉冲+符号（servo_sign == u32Sign)
 * @param u32TriggerMode  0：连续输出脉冲模式（直接输出），1：指定脉冲数量输出模式（需要BoardPulseOutput触发才能输出）
 * @param u32Sign         脉冲方向
 * @param u32Period       脉冲周期（单位：Hz）
 * @param u32Duty         脉冲占空比（单位：%，整数，范围：0~100%）
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardPulseConfig(
  IN BOARD_HANDLE hHandle,
  IN uint32_t u32Channel,
  IN uint32_t u32Mode,
  IN uint32_t u32TriggerMode,
  IN uint32_t u32Sign,
  IN uint32_t u32Period, 
  IN uint32_t u32Duty
);
/**
 * @brief 输出指定数量脉冲，仅当TriggerMode == 1指定脉冲数量输出模式时生效
 * @param hHandle         板卡的控制句柄
 * @param u32Channel      通道号（0-3）
 * @param u32PulseNum     脉冲数量
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardPulseOutput(IN BOARD_HANDLE hHandle, IN uint32_t u32Channel, IN uint32_t u32PulseNum);
/**
 * @brief 停止脉冲控制输出（关闭功能复用）
 * @param hHandle         板卡的控制句柄
 * @param u32Channel      通道号（0-3）
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardPulseStop(IN BOARD_HANDLE hHandle, IN uint32_t u32Channel);
/**
 * @brief 读脉冲输出配置
 * @param hHandle             板卡的控制句柄
 * @param u32Channel          通道号（0-3）
 * @param[out] u32Mode        脉冲输出模式:
 *                            0 AB相
 *                            1 正/逆脉冲序列
 *                            2 脉冲+符号（servo_sign == !(u32Sign)）
 *                            3 脉冲+符号（servo_sign == u32Sign)
 * @param[out] u32TriggerMode 0：连续输出脉冲模式（直接输出），1：指定脉冲数量输出模式（需要BoardPulseOutput触发才能输出）
 * @param[out] u32Sign        脉冲方向
 * @param[out] u32Period      脉冲周期（单位：Hz）
 * @param[out] u32Duty        脉冲占空比（单位：%，整数，范围：0~100%）
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardPulseGetConfig(
  IN BOARD_HANDLE hHandle,
  IN uint32_t u32Channel,
  OUT uint32_t *u32Mode,
  OUT uint32_t *u32TriggerMode,
  OUT uint32_t *u32Sign,
  OUT uint32_t *u32Period, 
  OUT uint32_t *u32Duty
);

/**
 * @brief PWM输出模块
*/
/**
 * @brief 开启PWM输出
 * @param hHandle         板卡的控制句柄
 * @param u32Channel      通道号（0-3）
 * @param u32Period       脉冲周期（单位：Hz）
 * @param u32Duty         脉冲占空比（单位：%，整数，范围：0~100%）
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardPWMOutput(
  IN BOARD_HANDLE hHandle,
  IN uint32_t u32Channel,
  IN uint32_t u32Period,
  IN uint32_t u32Duty
);
/**
 * @brief 关闭PWM输出
 * @param hHandle         板卡的控制句柄
 * @param u32Channel      通道号（0-3）
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardPWMStop(IN BOARD_HANDLE hHandle, IN uint32_t u32Channel);
/**
 * @brief 读PWM配置
 * @param hHandle         板卡的控制句柄
 * @param u32Channel      通道号（0-3）
 * @param[out] u32Period  脉冲周期（单位：Hz）
 * @param[out] u32Duty    脉冲占空比（单位：%，整数，范围：0~100%）
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t BoardPWMGetConfig(
  IN BOARD_HANDLE hHandle,
  IN uint32_t u32Channel,
  OUT uint32_t *u32Period,
  OUT uint32_t *u32Duty
);

/*******************************************************************************
  ECAT接口
*******************************************************************************/
/**
  @brief  打开指定的ECAT
  
  @param[out]  phHandle         返回ECAT控制句柄
  @param       sBoardIf         板卡接口字符串，可以是
                                    ID<Board Index>     索引，BOARD_IDX(0)
                                    AL<Board Alias>     别名，BOARD_ALIAS(0)
                                    NOxxxxxx            序列号
  @param       u32EcatChannel   板卡EtherCAT的通道号，有效范围：0~BOARD_INFO::u32EcatChannelCnt-1
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatOpen(OUT ECAT_HANDLE *phHandle, IN const char* sBoardIf, IN uint32_t u32EcatChannel);

/**
  @brief  关闭ECAT
  
  @param  hHandle               板卡的控制句柄
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatClose(IN ECAT_HANDLE hHandle);

/**
  @brief  设置邮箱通信超时间时间，默认为20000
  
  @param  hHandle               板卡的控制句柄
  @param  timeout_ms            超时时间，毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSetTimeout(IN ECAT_HANDLE hHandle, uint32_t timeout_ms);

/**
 * @brief  返回当前板卡的EtherCAT通道号
 * 
 * @param       hHandle          板卡的控制句柄
 * @param[out]  u32EcatChannel   输出EtherCAT通道号
 * @return  返回ECAT_S_OK(0)表示成功，非0表示失败
 */
EXPORT int32_t EcatGetCurrentChannel(IN ECAT_HANDLE hHandle, OUT uint32_t *u32EcatChannel);


/**
  @brief  返回当前板卡的信息
  
  @param       hHandle          板卡的控制句柄
  @param[out]  ptBoardInfo      输出板卡信息
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetInfo(IN ECAT_HANDLE hHandle, OUT BOARD_INFO *ptBoardInfo);

/**
  @brief  返回当前板卡的BOARD_HANDLE
  
  @param       hHandle          板卡的控制句柄
  @param[out]  ptBoardInfo      输出板卡信息
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetBoardHandle(IN ECAT_HANDLE hHandle, OUT BOARD_HANDLE *boardHandle);

/**
  @brief  复位ECAT
  
  @param  hHandle               ECAT控制句柄
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatReset(IN ECAT_HANDLE hHandle);

/**
  @brief  启动ECAT总线运行
  
  @param  hHandle               ECAT控制句柄
  @param  cEniFileName          ENI配置文件名
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatBusRun(IN ECAT_HANDLE hHandle, IN const char *cEniFileName);

/**
  @brief  启动ECAT总线运行
  
  @param  hHandle               ECAT控制句柄
  @param  cEniFileName          ENI配置文件名
  @param  PropfnCallback        进度回调，可为空
  @param  Context               进度回调的第一个参数，可为空
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatBusRunEx(IN ECAT_HANDLE hHandle, IN const char* cEniFileName, PFN_PROGRESS_CALLBACK PropfnCallback, void *Context);


/**
  @brief  停止ECAT总线运行
  
  @param  hHandle               ECAT控制句柄
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatBusStop(IN ECAT_HANDLE hHandle);

/**
  @brief  获得总线状态
  
  @param       hHandle          ECAT控制句柄
  @param[OUT]  pu32BusStatus    返回总线状态，@see EcatBusStatusFlag
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatBusStatus(IN ECAT_HANDLE hHandle, OUT uint32_t *pu32BusStatus);

/**
  @brief  检查ECAT状态
  
  @param  hHandle               ECAT控制句柄
  @return  返回true表示在运行状态，返回false表示在停止状态，参数错误时亦返回false
*/
EXPORT bool EcatBusIsRunning(IN ECAT_HANDLE hHandle);

/**
  @brief  设置PI复位本地模式，该模式会在调用EcatBusRun时触发行为
  
  @param  hHandle               ECAT控制句柄
  @param  u32ReinitMode         PI复位模式: 见ECAT_PI_REINIT_MODE，默认值为ECAT_PI_REINIT_AUTO
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIReinitModeSet(IN ECAT_HANDLE hHandle, IN uint32_t u32ReinitMode);

/**
  @brief  获取PI复位本地模式
  
  @param  hHandle               ECAT控制句柄
  @param[out] pu32ReinitMode    PI复位模式: 见ECAT_PI_REINIT_MODE
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIReinitModeGet(IN ECAT_HANDLE hHandle, OUT uint32_t *pu32ReinitMode);

/**
  @brief  设置PI复位全局模式，该模式会在调用EcatBusRun时触发行为
          本地模式优先级大于全局，当本地模式为AUTO时，将使全局模式作为复位的依据
          如果全局模式也为AUTO，则等价于ECAT_PI_REINIT_ALL
  
  @param  hHandle               ECAT控制句柄
  @param  u32ReinitMode         PI复位模式: 见ECAT_PI_REINIT_MODE，默认值为ECAT_PI_REINIT_AUTO
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIReinitModeSetGlobal(IN ECAT_HANDLE hHandle, IN uint32_t u32ReinitMode);

/**
  @brief  获取PI复位全局模式
  
  @param  hHandle               ECAT控制句柄
  @param[out] pu32ReinitMode    PI复位模式: 见ECAT_PI_REINIT_MODE
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIReinitModeGetGlobal(IN ECAT_HANDLE hHandle, OUT uint32_t *pu32ReinitMode);

/**
  @brief  获取总线EtherCAT主站OUT端口起的第一个出现网络连接断开的设备

  @param       hHandle                     ECAT控制句柄
  @param[out]  p32FirstLinkDownPortStatus  端口状态
                                             bit[31-16]: Slave id, 0~N-1
                                             bit[15] Link status on Port 3      1 : Link up   0: Link down
                                             bit[14] Loop Port 0                1 : Closed.   0: Open.
                                             bit[13] Link status on Port 2      1 : Link up   0: Link down
                                             bit[12] Loop Port 0                1 : Closed.   0: Open.
                                             bit[11] Link status on Port 1      1 : Link up   0: Link down
                                             bit[10] Loop Port 0                1 : Closed.   0: Open.
                                             bit[9]  Link status on Port 0      1 : Link up   0: Link down
                                             bit[8]  Loop Port 0                1 : Closed.   0: Open.
                                             bit[7-2]: reserved
                                             bit[1]: OUT Port Status            1 : Closed.   0: Open.
                                             bit[0]: IN  Port Status            1 : Closed.   0: Open.  

  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetFirstLinkDownPortStatus(IN ECAT_HANDLE hHandle, OUT uint32_t *pdwFirstLinkDownPortStatus);

/**
  @brief  读ENI信息
  
  @param  hHandle               ECAT控制句柄
  @param[out] ptInfo            信息
  @return  返回true表示在运行状态，返回false表示在停止状态，参数错误时亦返回false
*/
EXPORT int32_t EcatGetEniInfo(IN ECAT_HANDLE hHandle, OUT ECAT_ENI_INFO* ptInfo);

/**
  @brief  读ENI扩展信息
  
  @param  hHandle               ECAT控制句柄
  @param[out] ptInfo            信息
  @param  u32Length             ptInfo的字节长度信息
  @return  返回true表示在运行状态，返回false表示在停止状态，参数错误时亦返回false
*/
EXPORT int32_t EcatGetEniInfoEx(IN ECAT_HANDLE hHandle, void *ptInfo, uint32_t u32Length);

/**
  @brief  COE命令：写从站SDO
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  u16Index              SDO索引
  @param  u8SubIndex            SDO子索引
  @param  u32DataLen            数据的字节长度，通常为：1,2,4
  @param  pvData                写入的数据，发送多字节时，须以小端排列
  @param  u32TimeOutMs          超时时间，以毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatCoeSDODownload(
    IN ECAT_HANDLE      hHandle,
    IN uint16_t         u16SlaveId,
    IN uint16_t         u16Index,
    IN uint8_t          u8SubIndex,
    IN uint32_t         u32DataLen,
    IN const void       *pvData,
    IN uint32_t         u32TimeOutMs);

/**
  @brief  COE命令：读从站SDO
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  u16Index              SDO索引
  @param  u8SubIndex            SDO子索引
  @param[INOUT]  pu32DataLen    IN：作为pu8Data的最大长度，OUT：作为读取到的有效字节数，通常为：1,2,4
  @param[OUT]  pvData           存储读取到的数据，发送多字节时，须以小端排列
  @param  u32TimeOutMs          超时时间，以毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatCoeSDOUpload(
    IN ECAT_HANDLE      hHandle,
    IN uint16_t         u16SlaveId,
    IN uint16_t         u16Index,
    IN uint8_t          u8SubIndex,
    IN OUT uint32_t    *pu32DataLen,
    OUT void           *pvData,
    IN uint32_t         u32TimeOutMs);

/**
  @brief  COE命令扩展：写从站SDO
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  u16Index              SDO索引
  @param  u8SubIndex            SDO子索引，若CompleteAccess == True，该值为0或1
  @param  bCompleteAccess       false: 非完全访问，true: 完全访问，写完整的对象，子索引应为0（包含子索引0）或1（不包含子索引0）
  @param  u32DataLen            数据的字节长度，通常为：1,2,4
  @param  pvData                写入的数据，发送多字节时，须以小端排列
  @param  u32TimeOutMs          超时时间，以毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatCoeSDODownloadEx(
    IN ECAT_HANDLE      hHandle,
    IN uint16_t         u16SlaveId,
    IN uint16_t         u16Index,
    IN uint8_t          u8SubIndex,
    IN uint8_t          bCompleteAccess,
    IN uint32_t         u32DataLen,
    IN const void       *pvData,
    IN uint32_t         u32TimeOutMs);

/**
  @brief  COE命令扩展：读从站SDO
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  u16Index              SDO索引
  @param  u8SubIndex            SDO子索引，若CompleteAccess == True，该值为0或1
  @param  bCompleteAccess       false: 非完全访问，true: 完全访问，读完整的对象，子索引应为0（包含子索引0）或1（不包含子索引0）
  @param[INOUT]  pu32DataLen    IN：作为pu8Data的最大长度，OUT：作为读取到的有效字节数，通常为：1,2,4
  @param[OUT]  pvData           存储读取到的数据，发送多字节时，须以小端排列
  @param  u32TimeOutMs          超时时间，以毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatCoeSDOUploadEx(
    IN ECAT_HANDLE      hHandle,
    IN uint16_t         u16SlaveId,
    IN uint16_t         u16Index,
    IN uint8_t          u8SubIndex,
    IN uint8_t          bCompleteAccess,
    IN OUT uint32_t    *pu32DataLen,
    OUT void           *pvData,
    IN uint32_t         u32TimeOutMs);

/**
  @brief  设置异步控制缓存的数据同步模式
  
  @param  hHandle               ECAT控制句柄
  @param  mode                  异步缓存工作模式，见ECAT_PI_ASYNC_MODE，
                                默认值为ECAT_PI_ASYNC_MODE_DIRECT_ACCESS
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIASyncModeSet(
    IN ECAT_HANDLE           hHandle,
    IN uint32_t              mode);

/**
  @brief  设置异步控制缓存的周期同步模式的回调函数
  
  @param  hHandle               ECAT控制句柄
  @param  pvContext             传输给回调函数的第一个参数
  @param  pfnCallback           回调函数
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIAsyncModeCycleCallbackHook(
    IN ECAT_HANDLE                      hHandle,
    IN void                            *pvContext,
    IN PFN_PI_ASYNC_MODE_CYCLE_CALLBACK pfnCallback);

/**
  @brief  读取异步控制缓存的数据同步模式
  
  @param  hHandle               ECAT控制句柄
  @param  mode                  输出异步缓存工作模式，见ECAT_PI_ASYNC_MODE
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIASyncModeGet(
    IN ECAT_HANDLE          hHandle,
    IN uint32_t            *mode);

/**
  @brief  手动更新数据到板卡
  
  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_FAST_OUTPUT，PI_AREA_SHADOW_INPUT
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIASyncUpdateData(
    IN ECAT_HANDLE          hHandle,
    IN uint32_t             u32PiArea);

/**
  @brief  写镜像输出数据到指定的缓存
  
  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT
  @param  u32Offset             区域的偏移地址
  @param  u32DataLen            写入的数据长度
  @param  pvData                写入的数据
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIWrite(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32Offset,
    IN uint32_t         u32DataLen,
    IN const void       *pvData);

/**
  @brief  通过掩码的方式写镜像输出数据到指定的缓存
  
  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT
  @param  u32Offset             区域的偏移地址
  @param  u32DataLen            写入的数据长度
  @param  pvMask                写入的数据掩码
  @param  pvData                写入的数据
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIMaskWrite(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32Offset,
    IN uint32_t         u32DataLen,
    IN const void       *pvMask,
    IN const void       *pvData);

/**
  @brief  从指定的缓存读取镜像数据

  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_INPUT、PI_AREA_SHADOW_INPUT
                                  PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT、PI_AREA_SHADOW_OUTPUT
  @param  u32Offset             区域的偏移地址
  @param  u32DataLen            读取的数据长度
  @param[OUT]  pvData           读取的数据
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIRead(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32Offset,
    IN uint32_t         u32DataLen,
    OUT void           *pvData);

/**
  @brief  指定的缓存数据清零

  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_INPUT
                                  PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIClear(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea
);

/**
  @brief  获取镜像缓存的内存映射地址，可用于高速访问，非线程安全

  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，支持PI_AREA_LOCAL_OUTPUT、PI_AREA_LOCAL_INPUT
                                  PI_AREA_FAST_OUTPUT、PI_AREA_SHADOW_OUTPUT、PI_AREA_SHADOW_INPUT
  @param[OUT]  ppvData          返回PI镜像的地址
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPINMap(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    OUT void          **ppvData);

/**
  @brief  获取指定从站的镜像缓存的内存映射偏移及大小

  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_INPUT、PI_AREA_SHADOW_INPUT
                                  PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT、PI_AREA_SHADOW_OUTPUT
  @param  [out]u32Offset        区域的偏移地址
  @param  [out]u32DataLen       读取的数据长度
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIGetRange(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32SlaveId,
    IN uint32_t        *pu32Offset,
    IN uint32_t        *pu32DataLen);


/**
  @brief  指定从站配置看门狗

  @param  hHandle               ECAT控制句柄
  @param  Slave                 从站编号
  @param  Type                  看门狗类型，@see ECAT_WATCHDOG_TYPE
  @param  Mask                  掩码，可为空，等价于全为0xFF
  @param  Data                  数据
  @param  Size                  长度
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIWatchDogConfig(IN ECAT_HANDLE hHandle, IN uint32_t Slave, IN uint32_t Type,
    IN const uint8_t *Mask, IN const uint8_t *Data, IN uint32_t Size);

/**
  @brief  查看从站配置看门狗

  @param  hHandle               ECAT控制句柄
  @param  Slave                 从站编号
  @param  [out]Type             看门狗类型，@see ECAT_WATCHDOG_TYPE
  @param  [out]Timeout          看门狗超时值，毫秒为单位
  @param  [out]Mask             掩码
  @param  [out]Data             数据
  @param  [in/out]Size          输入缓冲区长度，输出读取到的数据长度
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIWatchDogQuery(IN ECAT_HANDLE hHandle, IN uint32_t Slave, OUT uint32_t* Type,
    OUT uint32_t *Timeout, OUT uint8_t *Mask, OUT uint8_t *Data, OUT uint32_t *Size);

/**
  @brief  启动看门狗

  @param  hHandle               ECAT控制句柄
  @param  Timeout               看门狗超时值，0: 禁用，非0: 超时(ms)
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIWatchDogEnable(ECAT_HANDLE hHandle, uint32_t TimeoutMs);

/**
  @brief  手动喂狗，以避免PI超时。调用以下函数会自动触发喂狗：EcatPIWrite、EcatPIOutputQueuePush、EcatRequestMasterState、EcatSlavePIForceOutputConfig

  @param  hHandle               ECAT控制句柄
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIWatchDogFeed(IN ECAT_HANDLE hHandle);


/**
  @brief  使能PI镜像缓存跟总线进行数据交换，等价于调用 EcatPIEnableSet(xx, 1)

  @param  hHandle               ECAT控制句柄
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIEnable(IN ECAT_HANDLE hHandle);

/**
  @brief  禁能PI镜像缓存跟总线进行数据交换，等价于调用 EcatPIEnableSet(xx, 0)

  @param  hHandle               ECAT控制句柄
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIDisable(IN ECAT_HANDLE hHandle);

/**
  @brief  设置PI镜像缓存跟总线进行数据交换是否使能，调用EcatBusRun后，复位并处于禁能状态

  @param  hHandle               ECAT控制句柄
  @param  bState                false(0)为禁能，true(1)为使能
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIEnableSet(IN ECAT_HANDLE hHandle, IN uint32_t bState);

/**
  @brief  获取PI镜像缓存跟总线进行数据交换是否使能

  @param  hHandle               ECAT控制句柄
  @param[out]  bState           false(0)为禁能，true(1)为使能
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIEnableGet(IN ECAT_HANDLE hHandle, OUT uint32_t *bState);

/**
  @brief  拷贝镜像输出缓存到输出队列的尾部，队列的有效个数加1，非线程安全
  
  @param  hHandle               ECAT控制句柄
  @param  bReqClearQueue        true: 快速输出当前帧,并在输出帧后清空之前的队列数据，false：无特殊操作
  @param  u32TimeOutMs          超时时间，以毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIOutputQueuePush(
    IN ECAT_HANDLE      hHandle,
    IN bool             bReqClearQueue,
    IN uint32_t         u32TimeOutMs);

/**
  @brief  返回PI输出队列的有效数的个数
  
  @param  hHandle               ECAT控制句柄
  @param[OUT]  pu32Size         当前有效数据的个数
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIOutputQueueSize(
    IN ECAT_HANDLE      hHandle,
    OUT uint32_t       *pu32Size);

/**
  @brief  拷贝输入队列的头部到镜像输入缓存，队列的有效个数减1，非线程安全
  
  @param  hHandle               ECAT控制句柄
  @param  bReqClearRemain       true: 获取队列中最新的数据，然后清空残留的队列数据，false：无特殊操作
  @param  u32TimeOutMs          超时时间，以毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIInputQueuePop(
    IN ECAT_HANDLE      hHandle,
    IN bool             bReqClearRemain,
    IN uint32_t         u32TimeOutMs);

EXPORT int32_t EcatPIInputQueuePop_US(
    IN ECAT_HANDLE      hHandle,
    IN bool             bReqClearRemain,
    IN uint32_t         u32TimeOutUs);

/**
  @brief  返回输入队列的有效数的个数
  
  @param      hHandle               ECAT控制句柄
  @param[out] pu32Size              返回输入队列的有效数的个数
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIInputQueueSize(
    IN ECAT_HANDLE      hHandle,
    OUT uint32_t        *pu32Size);

/**
  @brief  等待总线更新输入数据，非线程安全
  
  @param  hHandle               ECAT控制句柄
  @param  u32TimeOutMs          超时时间，以毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIInputWaitNotify(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32TimeOutMs);

/**
  @brief  从从站中读文件到本地文件系统
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  pszFileLocalName      本地文件系统的文件名
  @param  pszFileSlaveName      从站文件系统的文件名
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatFoERead(
    IN ECAT_HANDLE              hHandle,
    IN uint16_t                 u16SlaveId,
    IN const char              *pszFileLocalName,
    IN const char              *pszFileSlaveName);

/**
  @brief  从本地文件系统写文件到从站
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  pszFileLocalName      本地文件系统的文件名
  @param  pszFileSlaveName      从站文件系统的文件名
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatFoEWrite(
    IN ECAT_HANDLE              hHandle,
    IN uint16_t                 u16SlaveId,
    IN const char              *pszFileLocalName,
    IN const char              *pszFileSlaveName);

/**
  @brief  请求切换主站状态
  
  @param  hHandle               ECAT控制句柄
  @param  u8State               切换到主站的状态，@see EcatState
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatRequestMasterState(IN ECAT_HANDLE hHandle, IN uint8_t u8State);

/**
  @brief  获取当前主站请求的状态
  
  @param  hHandle               ECAT控制句柄
  @param[OUT]  pu8State         主站当前的状态，@see EcatState
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetMasterReqState(IN ECAT_HANDLE hHandle, OUT uint8_t *pu8State);

/**
  @brief  获取当前主站状态
  
  @param  hHandle               ECAT控制句柄
  @param[OUT]  pu8State         主站当前的状态，@see EcatState
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetMasterState(IN ECAT_HANDLE hHandle, OUT uint8_t *pu8State);

/**
  @brief  获得当前ENI配置下从站的数量
  
  @param  hHandle               ECAT控制句柄
  @param[OUT]  pu16Count        从站的个数
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveCount(IN ECAT_HANDLE hHandle, OUT uint16_t *pu16Count);

/**
  @brief  获取当前网络中活跃的从站数量。当有从站进行热插拔时，这个数量可能会和EcatGetSlaveCount不同
  
  @param  hHandle               ECAT控制句柄
  @param[OUT]  pu16Count        从站的个数
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetActiveSlaveCount(IN ECAT_HANDLE hHandle, OUT uint16_t *pu16Count);

/**
  @brief  判断当前主站和配置文件是否一致

  @param  hHandle               ECAT控制句柄
  @param  eniFile            eni
  @return  返回ECAT_S_OK(0)表示一致，非0表示不一致
*/
EXPORT int32_t EcatCheckRunningEni(
    IN ECAT_HANDLE              hHandle,
    IN const char*              eniFile);

/**
  @brief  检索从站是否处于活跃状态

  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @return  返回true表示活跃，返回false表示离线
*/
EXPORT bool EcatSlaveIsActive(IN ECAT_HANDLE hHandle, IN  uint16_t u16SlaveId);

/**
  @brief  检索从站是否处于可收发PDO数据的就绪状态，即可EcatGetSlaveState状态为8。
          此接口与EcatGetSlaveState相比效率更高

  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @return  返回true表示活跃，返回false表示离线
*/
EXPORT bool EcatSlaveIsReady(IN ECAT_HANDLE hHandle, IN  uint16_t u16SlaveId);

/**
  @brief  设置就绪状态丢失检测超时时间，调用EcatBusRun后会恢复默认值0

  @param  u32TimeoutMs          超时时间，毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatReadyLostTimeoutSet(IN ECAT_HANDLE hHandle, IN uint32_t u32TimeoutMs);

/**
  @brief  读取就绪状态丢失检测超时时间，调用EcatBusRun后会恢复默认值0

  @param  [out]pu32TimeoutMs    输出超时时间，毫秒为单位
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatReadyLostTimeoutGet(IN ECAT_HANDLE hHandle, IN uint32_t *pu32TimeoutMs);

/**
  @brief  获取指定从站的镜像缓存的内存映射偏移及大小

  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_INPUT、PI_AREA_SHADOW_INPUT
                                  PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT、PI_AREA_SHADOW_OUTPUT
  @param  [out]u32Offset        区域的偏移地址
  @param  [out]u32DataLen       读取的数据长度
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlavePIGetRange(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32SlaveId,
    IN uint32_t        *pu32Offset,
    IN uint32_t        *pu32DataLen);


/**
  @brief  写镜像输出数据到指定的从站缓存
  
  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT
  @param  u32SlaveIndex         从站索引
  @param  u32DataLen            写入的数据长度
  @param  pvData                写入的数据
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlavePIWrite(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32SlaveIndex,
    IN uint32_t         u32DataLen,
    IN const void       *pvData);

/**
  @brief  通过掩码的方式写镜像输出数据到指定的从站缓存
  
  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT
  @param  u32SlaveIndex         从站索引
  @param  u32DataLen            写入的数据长度
  @param  pvMask                写入的数据掩码
  @param  pvData                写入的数据
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlavePIMaskWrite(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32SlaveIndex,
    IN uint32_t         u32DataLen,
    IN const void       *pvMask,
    IN const void       *pvData);

/**
  @brief  从指定的从站缓存读取镜像数据

  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_INPUT、PI_AREA_SHADOW_INPUT
                                  PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT、PI_AREA_SHADOW_OUTPUT
  @param  u32SlaveIndex         从站索引
  @param  u32DataLen            读取的数据长度
  @param[OUT]  pvData           读取的数据
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlavePIRead(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32SlaveIndex,
    IN uint32_t         u32DataLen,
    OUT void           *pvData);

/**
  @brief  写镜像输出数据到指定的从站缓存
  
  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT
  @param  u32SlaveIndex         从站索引
  @param  u32Offset             写入的数据偏移
  @param  u32DataLen            写入的数据长度
  @param  pvData                写入的数据
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlavePIWriteEx(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32SlaveIndex,
    IN uint32_t         u32Offset,
    IN uint32_t         u32DataLen,
    IN const void       *pvData);

/**
  @brief  通过掩码的方式写镜像输出数据到指定的从站缓存
  
  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT
  @param  u32SlaveIndex         从站索引
  @param  u32Offset             写入的数据偏移
  @param  u32DataLen            写入的数据长度
  @param  pvMask                写入的数据掩码
  @param  pvData                写入的数据
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlavePIMaskWriteEx(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32SlaveIndex,
    IN uint32_t         u32Offset, 
    IN uint32_t         u32DataLen,
    IN const void       *pvMask,
    IN const void       *pvData);

/**
  @brief  从指定的从站缓存读取镜像数据

  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_INPUT、PI_AREA_SHADOW_INPUT
                                  PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT、PI_AREA_SHADOW_OUTPUT
  @param  u32SlaveIndex         从站索引
  @param  u32Offset             读取的数据偏移
  @param  u32DataLen            读取的数据长度
  @param[OUT]  pvData           读取的数据
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlavePIReadEx(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32SlaveIndex,
    IN uint32_t         u32Offset, 
    IN uint32_t         u32DataLen,
    OUT void           *pvData);

/**
  @brief  指定的从站缓存数据清零

  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_INPUT
                                  PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT
  @param  u32SlaveIndex         从站索引
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlavePIClear(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32SlaveIndex
);

/**
  @brief  获取从站的DL状态

  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  [OUT]pu16DLStatus     存储从站的DL状态
                                  bit     0: PDI Operational
                                  bit     1: PDI Watchdog Status
                                  bits 2..3: Reserved
                                  bit     4: Link on Port 0
                                  bit     5: Link on Port 1
                                  bit     6: Link on Port 2
                                  bit     7: Link on Port 3
                                  bit     8: Loop Port 0 (Channel A Loop Status)
                                  bit     9: Communication on Port 0 (Channel A Signal Detection)
                                  bit    10: Loop Port 1 (Channel B Loop Status)
                                  bit    11: Communication on Port 1 (Channel B Signal Detection)
                                  bit    12: Loop Port 2 (Channel C Loop Status)
                                  bit    13: Communication on Port 2 (Channel C Signal Detection)
                                  bit    14: Loop Port 3 (Channel D Loop Status)
                                  bit    15: Communication on Port 3 (Channel D Signal Detection)
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveDLStatus(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, OUT uint16_t* pu16DLStatus);

/**
  @brief  获取从站的AL状态码

  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  [OUT]pu16ALStatusCode 存储从站的AL状态码 @see ECAT_AL_STATUS_CODE
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveALStatusCode(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, OUT uint16_t* pu16ALStatusCode);

/**
  @brief  获得当前的详细信息
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pSlaveInfo       从站的信息
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveInfo(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, OUT ECAT_SLAVE_INFO *pSlaveInfo);

/**
  @brief  获取从站的状态
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pu8State         从站当前的状态，@see EcatState
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveState(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, OUT uint8_t *pu8State);

/**
  @brief  获取前N个从站的状态
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveNum           从站数量
  @param[OUT]  pu8State         从站当前的状态数组，@see EcatState
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveStateEx(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveNum, OUT uint8_t *pu8State);

/**
  @brief  获取从站的请求状态
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pu8State         从站当前的状态，@see EcatState
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveReqState(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, OUT uint8_t *pu8State);

/**
  @brief  获取前N个从站的请求状态
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveNum           从站数量
  @param[OUT]  pu8State         从站当前的状态数组，@see EcatState
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveReqStateEx(IN ECAT_HANDLE hHandle,
    IN uint16_t u16SlaveNum,
    OUT uint8_t *pu8State);

/**
  @brief  清除从站状态错误，如从站返回EcatStateErrorFlag错误码，则需要调用本API来清除错误
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1，0xFFFF表示清除所有从站的错误
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatClearSlaveStateError(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId);

/**
  @brief  设置自动清除状态错误是否使能，默认值，调用EcatBusRun后复位并处于使能状态

  @param  hHandle               ECAT控制句柄
  @param  State                 false(0)为禁能，true(1)为使能
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatAutoClearSlaveStateErrorEnableSet(IN ECAT_HANDLE hHandle, IN uint32_t bState);

/**
  @brief  获取自动清除状态错误是否使能

  @param  hHandle               ECAT控制句柄
  @param[out]  State            false(0)为禁能，true(1)为使能
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatAutoClearSlaveStateErrorEnableGet(IN ECAT_HANDLE hHandle, OUT uint32_t *bState);

/**
  @brief  获取从站的PI输入信息，主站消费，从站生产
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pu16BitSize      存储从站镜像的位大小
  @param[OUT]  pu16BitOffset    存储从站镜像的位偏移
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlavePIInputsInfo(
    IN ECAT_HANDLE          hHandle,
    IN uint16_t             u16SlaveId,
    OUT uint16_t           *pu16BitSize,
    OUT uint16_t           *pu16BitOffset);

/**
  @brief  获取从站的PI输出信息，主站生产，从站消费
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pu16BitSize      存储从站镜像的位大小
  @param[OUT]  pu16BitOffset    存储从站镜像的位偏移
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlavePIOutputsInfo(
    IN ECAT_HANDLE          hHandle,
    IN uint16_t             u16SlaveId,
    OUT uint16_t           *pu16BitSize,
    OUT uint16_t           *pu16BitOffset);

/**
  @brief  获取从站制造商ID
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pu32VendorID     存储从站的制造商ID
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveVendorID(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, OUT uint32_t *pu32VendorID);

/**
  @brief  获取从站的产品码
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pu32ProductCode  存储从站的产品码
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveProductCode(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, OUT uint32_t *pu32ProductCode);

/**
  @brief  获取从站的修订号
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pu32RevisionNo   存储从站的修订号
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveRevisionNo(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, OUT uint32_t *pu32RevisionNo);

/**
  @brief  获取从站的序列号
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pu32SerialNo     获取从站的序列号
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveSerialNo(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, OUT uint32_t *pu32SerialNo);

/**
  @brief  获取从站的别名
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pu32Alias        获取从站的别名
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveAlias(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, OUT uint32_t *pu32Alias);

/**
  @brief  获取从站的设备地址
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param[OUT]  pu32Alias        获取从站的设备地址
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatGetSlaveFixAddr(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId,OUT uint16_t *p16FixAddr);

/**
  @brief  写从站EEPROM
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  u16StartAdr           EEPROM偏移地址
  @param  u16DataLen            数据的字节长度
  @param  pvData                写入的数据，发送多字节时，须以小端排列
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlaveEepromWrite(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, IN uint16_t u16StartAdr, IN uint16_t u16DataLen, IN const void* pvData);

/**
  @brief  读从站EEPROM
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  u16StartAdr           EEPROM偏移地址
  @param  u16DataLen            数据的字节长度
  @param[OUT]  pvData           存储读取到的数据，发送多字节时，须以小端排列
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlaveEepromRead(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, IN uint16_t u16StartAdr, IN uint16_t u16DataLen, OUT void* pvData);

/**
  @brief  写从站Memory
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  u16StartAdr           Memory偏移地址
  @param  u16DataLen            数据的字节长度
  @param  pvData                写入的数据，发送多字节时，须以小端排列
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlaveMemoryWrite(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, IN uint16_t u16StartAdr, IN uint16_t u16DataLen, IN const void* pvData);

/**
  @brief  读从站Memory
  
  @param  hHandle               ECAT控制句柄
  @param  u16SlaveId            从站位置索引，范围：0~SlaveCount-1
  @param  u16StartAdr           Memory偏移地址
  @param  u16DataLen            数据的字节长度
  @param[OUT]  pvData           存储读取到的数据，发送多字节时，须以小端排列
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlaveMemoryRead(IN ECAT_HANDLE hHandle, IN uint16_t u16SlaveId, IN uint16_t u16StartAdr, IN uint16_t u16DataLen, OUT void* pvData);


/**
  @brief  将从站的物理地址转换为从站索引
  
  @param  hHandle               ECAT控制句柄
  @param  fixAddr               从站物理地址
  @param[OUT]  slaveIndex           从站位置索引，范围：0~SlaveCount-1
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatSlaveFixAddr2Index(IN ECAT_HANDLE hHandle, IN uint16_t fixAddr, OUT uint16_t* slaveIndex);

/**
 * @brief 获取固件编译信息
 * 
 * @param  hHandle              ECAT控制句柄
 * @param  outputStr            输出的字符串
 * @param  outputSize           存储空间的大小
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
 */
EXPORT int32_t EcatGetFirmwareCompileMsg(IN ECAT_HANDLE hHandle, OUT void *outputStr, IN OUT uint32_t *outputSize);

/**
 * @brief 启动抓包
 * 
 * @param  hHandle              ECAT控制句柄
 * @param  queueSize            循环队列大小
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
 */
EXPORT int32_t EcatWiresharkStartup(
    IN ECAT_HANDLE              hHandle,
    IN uint32_t                 queueSize);

/**
 * @brief 获取抓包数据
 * 
 * @param  hHandle              ECAT控制句柄
 * @param  pContainer           存储空间
 * @param  size                 存储空间的大小
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
 */
EXPORT int32_t EcatWiresharkGetData(
    IN ECAT_HANDLE              hHandle,
    IN void*                    pContainer,
    IN uint32_t*                pSize);

/**
 * @brief 停止抓包动作
 * 
 * @param  hHandle              ECAT控制句柄
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
 */
EXPORT int32_t EcatWiresharkStop(IN ECAT_HANDLE              hHandle);

/**
 * @brief 配置从站的强制输出功能
 * 
 * @param hHandle               ECAT控制句柄
 * @param slaveIdx              从站拓扑序号[0~N]
 * @param cmdType               0：禁用强制输出(默认值) 1：强制设置 2：带掩码设置
 * @param dataOfs               基于从站的偏移
 * @param dataSize              data的数据长度
 * @param data                  待设置的数据
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
 * 
 * eg: cmdType:1，ofs:0 size:2 data:0xFAFB, 表示把从站偏移0的位置开始，设置第0个字节为0xFA，1为0xFB
 * eg: cmdType:2，ofs:0 size:2 data:0xF3E1, 表示把从站偏移0的位置开始，将Byte0 = (Byte0 & ~0xF3) | (0xF3 & 0xE1)
 */
EXPORT int32_t EcatSlavePIForceOutputConfig(ECAT_HANDLE Handle, uint32_t slaveIdx, uint32_t cmdType, 
        uint32_t dataOfs, uint32_t dataSize, const uint8_t *data);

/**
 * @brief 查询从站的强制输出功能
 * 
 * @param hHandle               ECAT控制句柄
 * @param slaveIdx              从站拓扑序号[0~N]
 * @param cmdType               0：禁用强制输出(默认值) 1：强制设置 2：带掩码设置
 * @param dataOfs               基于从站的偏移
 * @param dataSize              输入为data空间的大小，输出的返回的data数据长度
 * @param data                  待存储数据的空间
 * @return 返回ECAT_S_OK(0)表示成功，非0表示失败
 */
EXPORT int32_t EcatSlavePIForceOutputQuery(ECAT_HANDLE Handle, uint32_t Slave, uint32_t* cmdType, 
        uint32_t *dataOfs, uint32_t *dataSize, uint8_t *data);

/*********************************************************************************/
//废弃

/**
  @brief  设置是否使用冗余管理功能，该接口已废弃，设置不产生任何效果

  @param  hHandle               ECAT控制句柄
  @param  bState                false为禁能，true为使能
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatRMEnable(IN ECAT_HANDLE hHandle, IN bool State);

/**
  @brief  设置是否使用冗余管理功能，该接口已废弃，设置不产生任何效果

  @param  hHandle               ECAT控制句柄
  @param  bState                false为禁能，true为使能
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatRMEnableSet(IN ECAT_HANDLE hHandle, IN uint32_t bState);

/**
  @brief  获取是否使用冗余管理功能，该接口已废弃，总是返回true

  @param  hHandle               ECAT控制句柄
  @param  bState                false为禁能，true为使能
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatRMEnableGet(IN ECAT_HANDLE hHandle, IN uint32_t *bState);

/**
  @brief  获取指定从站的镜像缓存的内存映射偏移及大小，该接口已废弃，请使用EcatSlavePIGetRange代替

  @param  hHandle               ECAT控制句柄
  @param  u32PiArea             镜像的区域索引，为以下值之一：PI_AREA_LOCAL_INPUT、PI_AREA_SHADOW_INPUT
                                  PI_AREA_LOCAL_OUTPUT、PI_AREA_FAST_OUTPUT、PI_AREA_SHADOW_OUTPUT
  @param  [out]u32Offset        区域的偏移地址
  @param  [out]u32DataLen       读取的数据长度
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIGetRange(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32PiArea,
    IN uint32_t         u32SlaveId,
    IN uint32_t        *pu32Offset,
    IN uint32_t        *pu32DataLen);


/**
  @brief  启动PI镜像看门狗

  @param  hHandle               ECAT控制句柄
  @param  u32TimeoutUs          看门狗超时时间
  @param  psTimeoutCmdLine      看门狗超时后，触发执行的命令，支持的命令如下
                                  stop_bus 
                                    usage:  stop_bus
                                    停止总线

                                  fill_shadow offset data [offset data ...]
                                    使用指定的数据来填充镜像影子输出缓存
                                    offset              指向缓存的第n个字节，0开始编号
                                    data                写入数据，固定采用2字节的16进制表示
                                    [offset data ...]   表示后面可以接任意个offset data的重复

                                    usage: 以下示例均表示将缓存的第20到24数据，填充为 0x00 0x01 0x02 0x03，其余数据不变
                                           fill_shadow 20 00010203
                                           fill_shadow 20 00 21 01 22 02 23 03
                                           fill_shadow 20 0001 22 0203

                                  modify_shadow offset mask data [offset mask data ...]
                                    使用指定的数据来修改镜像影子输出缓存
                                    offset              指向缓存的第n个字节，0开始编号
                                    mask                数据掩码，只有对应为1的位置可写入数据，固定采用2字节的16进制表示
                                    data                写入数据，固定采用2字节的16进制表示，执行操作如下
                                                          new = (old & ~mask) | (data & mask)
                                    [offset data ...]   表示后面可以接任意个offset data的重复

                                    usage: 以下示例均表示将缓存的第20到24数据，修改为 0x00 0x01 0x02 0x03，其余数据不变
                                           modify_shadow 20 ffffffff 00010203
                                           modify_shadow 20 ff 00 21 ff 01 22 ff 02 23 ff 03
                                           modify_shadow 20 ffff 0001 22 ffff 0203

  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIWatchDogStart(
    IN ECAT_HANDLE      hHandle,
    IN uint32_t         u32TimeoutUs,
    IN const char      *psTimeoutCmdLine);

/**
  @brief  停止PI镜像看门狗

  @param  hHandle               ECAT控制句柄
  @return  返回ECAT_S_OK(0)表示成功，非0表示失败
*/
EXPORT int32_t EcatPIWatchDogStop(IN ECAT_HANDLE hHandle); 



/*********************************************************************************/

#ifdef __cplusplus
}
#endif

#endif  // ZMC_PCI_ZECM_H_
/* end of file */
