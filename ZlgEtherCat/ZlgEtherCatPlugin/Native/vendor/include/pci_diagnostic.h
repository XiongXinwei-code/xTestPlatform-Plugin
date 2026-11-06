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
 * @brief PCIe-XX系列诊断功能相关API
 *
 * @internal
 * @par Modification history
 * - 1.00 2023-5-29  ZBL, 初次实现
 * @endinternal
 */
/*----------------------------------------------------------------------------*/
#ifndef ZMC_PCI_DIAGNOSTIC_H_
#define ZMC_PCI_DIAGNOSTIC_H_

#include "pci_zecm.h"

#ifdef __cplusplus
extern "C" {
#endif

/*******************************************************************************
  诊断接口
*******************************************************************************/
/*! @brief Network states of a master and constants relevant to network state.
    @ingroup master_state_control_master
    @see EcatGetMasterNetworkState() */
typedef enum tagEcatMasterNetworkState
{
    EcatMasterNetworkStateNormal            = 0x0000, /*!< @brief State when redundancy is not used. */
    EcatMasterNetworkStateRedundancyNormal  = 0x0001, /*!< @brief State when redundancy is used and In-port of a slave is connected to the main NIC. */
    EcatMasterNetworkStateRedundancyInverse = 0x0002, /*!< @brief State when redundancy is used and In-port of a slave is connected to the redundant NIC. */
    EcatMasterNetworkStateRedundancySafe    = 0x0003, /*!< @brief State when redundancy is used and there is a brake between slaves. */
    EcatMasterNetworkStateMask              = 0x00FF, /*!< @brief Bitmask for extracting the network state value. */
    EcatMasterNetworkStateFlagOpposite      = 0x0100  /*!< @brief Flag that indicates that EtherCAT&reg; frames reach the opposite card when redundancy is used. */
} EcatMasterNetworkState;

/* Diagnostic */

/*! @brief Slave's diagnostic states.
    @ingroup master_diagnostics */
typedef enum tagEcatSlaveDiagnosticState
{
    /*! @brief Slave doesn't respond to commands. */
    EcatSlaveDiagnosticStateOffLine             = 0x00000001,

    /*! @brief Slave's state is different as the set one. */
    EcatSlaveDiagnosticStateErrorEcatState      = 0x00000002,

    /*! @brief Slave is not configured. */
    EcatSlaveDiagnosticStateNotConfigured       = 0x00000004,

    /*! @brief Slave's configuration doesn't match the configuration for this
        slave found in master. */
    EcatSlaveDiagnosticStateWrongConfiguration  = 0x00000008,

    /*! @brief Error while Init Cmd */
    EcatSlaveDiagnosticStateInitCmdError        = 0x00000010,

    /*! @brief Error while Mailbox Init Cmd */
    EcatSlaveDiagnosticStateMailboxInitCmdError = 0x00000020
}EcatSlaveDiagnosticState;

/*! @brief Master's diagnostic states.
    @ingroup master_diagnostics */
typedef enum tagEcatMasterDiagnosticState
{
    /*! @brief Diagnostics completed successfully. */
    EcatMasterDiagnosticStateUpdated					= 0x00000001,
    /*! @brief Error while sending/receiving a frame. */
    EcatMasterDiagnosticStateSendReceiveError			= 0x00000002,
    /*! @brief Error while processing the received frame. */
    EcatMasterDiagnosticStateParseError					= 0x00000004,
    /*! @brief No connection between the NIC adapter and slaves. */
    EcatMasterDiagnosticStateLinkDown					= 0x00000008,
    /*! @brief Wrong configuration */
    EcatMasterDiagnosticStateWrongConfiguration			= 0x00000010,
    /*! @brief Slave-to-slave timeout */
    EcatMasterDiagnosticStateS2STimeout					= 0x00000020,
    /*! @brief Default data was set */
    EcatMasterDiagnosticStateDefaultDataWasSet			= 0x00000040,
    /*! @brief WatchDogTimeOut */
    EcatMasterDiagnosticStateWatchDogTimeOut			= 0x00000080,
    /*! @brief MasterIsLocked */
    EcatMasterDiagnosticMasterIsLocked					= 0x00000100,
	/*! @brief Internal DC synchronisation is established */
    EcatMasterDiagnosticDCInternalSyncEstablished		= 0x00000200,
	/*! @brief External DC synchronisation is established */
    EcatMasterDiagnosticDCExternalSyncEstablished		= 0x00000400,
	/*! @brief DC Propagation delay initialized */
    EcatMasterDiagnosticDCPropagationDelayInitialized	= 0x00000800,
	/*! @brief Outputs was not delivered completely	*/
	EcatMasterDiagnosticStateOutputsUpdate			    = 0x00001000,
}EcatMasterDiagnosticState;

#pragma pack(1)
/*! @brief Collection of Slave Identity information. */
typedef struct _ECAT_SLAVE_IDENTITY
{
    uint32_t    dwVendorId;         /*!< @brief Vendor ID from slave's EEPROM.*/
    uint32_t    dwProductCode;      /*!< @brief Product code from slave's EEPROM.*/
    uint32_t    dwRevisionNo;       /*!< @brief Revision number from slave's EEPROM.*/
    uint32_t    dwSerialNo;         /*!< @brief Serial number from slave's EEPROM.*/
} ECAT_SLAVE_IDENTITY;
#pragma pack()

#pragma pack(1)
/*! @brief Slave Diagnostic Information. */
typedef struct _ECAT_SLAVE_DIAGNOSTIC_INFO
{
    uint32_t            dwDiagState;                /*!< @brief Slave state. See #EcatSlaveDiagnosticState for more details. */
    ECAT_SLAVE_IDENTITY Identity;                   /*!< @brief Collection of Slave Identity information @ref ECAT_SLAVE_IDENTITY. */
    /* Port 0 (A)*/
    uint8_t             byRXErrorsPort0;            /*!< @brief RX Error counter of Port 0 (For additional information see
                                                    description of RX Error Counter register (0x0300:0x0307) in
                                                    slave controller hardware data sheet).*/
    uint8_t             byInvalidFramesPort0;       /*!< @brief Invalid frame counter of Port 0 (For additional information see
                                                    description of RX Error Counter register (0x0300:0x0307) in slave
                                                    controller hardware data sheet).*/
    uint8_t             byLostLinkErrorsPort0;      /*!< @brief Lost Link counter of Port 0 (For additional information see
                                                    description of RX Error Counter register (0x0310:0x0313) in slave
                                                    controller hardware data sheet).*/
    /* Port 1 (B)*/
    uint8_t             byRXErrorsPort1;            /*!< @brief RX Error counter of Port 1 (For additional information see
                                                    description of RX Error Counter register (0x0300:0x0307) in
                                                    slave controller hardware data sheet).*/
    uint8_t             byInvalidFramesPort1;       /*!< @brief Invalid frame counter of Port 1 (For additional information
                                                    see description of RX Error Counter register (0x0300:0x0307) in
                                                    slave controller hardware data sheet).*/
    uint8_t             byLostLinkErrorsPort1;      /*!< @brief Lost Link counter of Port 1 (For additional information see
                                                    description of RX Error Counter register (0x0310:0x0313) in slave
                                                    controller hardware data sheet).*/
    /* Port 2 (C)*/
    uint8_t             byRXErrorsPort2;            /*!< @brief RX Error counter of Port 2 (For additional information see
                                                    description of RX Error Counter register (0x0300:0x0307) in
                                                    slave controller hardware data sheet).*/
    uint8_t             byInvalidFramesPort2;       /*!< @brief Invalid frame counter of Port 2 (For additional information
                                                    see description of RX Error Counter register (0x0300:0x0307) in
                                                    slave controller hardware data sheet).*/
    uint8_t             byLostLinkErrorsPort2;      /*!< @brief Lost Link counter of Port 2 (For additional information see
                                                    description of RX Error Counter register (0x0310:0x0313) in slave
                                                    controller hardware data sheet).*/
    /* Port 3 (D)*/
    uint8_t             byRXErrorsPort3;            /*!< @brief RX Error counter of Port 3 (For additional information
                                                    see description of RX Error Counter register (0x0300:0x0307) in
                                                    slave controller hardware data sheet).*/
    uint8_t             byInvalidFramesPort3;       /*!< @brief Invalid frame counter of Port 3 (For additional information see
                                                    description of RX Error Counter register (0x0300:0x0307) in slave
                                                    controller hardware data sheet).*/
    uint8_t             byLostLinkErrorsPort3;      /*!< @brief Lost link counter of Port 3 (For additional information see
                                                    description of RX Error Counter register (0x0310:0x0313) in slave
                                                    controller hardware data sheet).*/

    /* Port 0 (A)*/
    uint8_t             byForwardedRXErrorsPort0;   /*!< @brief Forwarded RX Error counter of Port 0 (For additional information see
                                                    description of Forwarded RX Error Counter register (0x0308:0x030B) in slave
                                                    controller hardware data sheet).*/

    /* Port 1 (B)*/
    uint8_t             byForwardedRXErrorsPort1;   /*!< @brief Forwarded RX Error counter of Port 1 (For additional information see
                                                    description of Forwarded RX Error Counter register (0x0308:0x030B) in slave
                                                    controller hardware data sheet).*/
    /* Port 2 (C)*/
    uint8_t             byForwardedRXErrorsPort2;   /*!< @brief Forwarded RX Error counter of Port 2 (For additional information see
                                                    description of Forwarded RX Error Counter register (0x0308:0x030B) in slave
                                                    controller hardware data sheet).*/
    /* Port 3 (D)*/
    uint8_t             byForwardedRXErrorsPort3;   /*!< @brief Forwarded RX Error counter of Port 3 (For additional information see
                                                    description of Forwarded RX Error Counter register (0x0308:0x030B) in slave
                                                    controller hardware data sheet).*/

    /* Processing unit */
    uint8_t             byProcessingUnitErrors;     /*!< @brief Processing unit Error counter (For additional information see
                                                    description of Processing unit Error Counter register (0x030C) in slave
                                                    controller hardware data sheet).*/

    /* PDI */
    uint8_t             byPDIErrors;                /*!< @brief PDI Error counter (For additional information see
                                                    description of PDI Error Counter register (0x030D) in slave
                                                    controller hardware data sheet).*/

    uint8_t             Reserved[2];                /*!< Reserved for future use. */
} ECAT_SLAVE_DIAGNOSTIC_INFO;
#pragma pack()


/*! @brief Master Diagnostic Information.
@ingroup master_diagnostics
@see see EcatGetDiagnostics, EcatMasterNetworkState, EcatGetMasterNetworkState() */
#pragma pack(1)
typedef struct _ECAT_MASTER_DIAGNOSTIC_INFO
{
    uint32_t    dwDiagState;            /*!< @brief Master's diagnostic state. See #EcatMasterDiagnosticState for more details. */
    uint16_t    wNetworkState;          /*!< @brief Network states of a master. See #EcatMasterNetworkState for more details. */
    uint16_t    wMainSlaveCount;        /*!< @brief Count of slaves on the main cable (i.e. slaves connected on the main NIC). */
    uint16_t    wRedundancySlaveCount;  /*!< @brief Count of slaves on the redundancy cable (i.e. slaves on the redundancy NIC). */
    uint16_t    wValidLicense;          /*!< @brief Type of license. See #ECAT_LICENSE_TYPE. */
    uint8_t     byExpirationDate[32];   /*!< @brief "License expiration date" information. */
    int32_t     nEDLength;              /*!< @brief Actual(current) length(in bytes) of byExpirationDate field. */
    uint8_t     byLicensedTo[64];       /*!< @brief "Licensed to" information. */
    int32_t     nLTLength;              /*!< @brief Actual(current) length(in bytes) of "byLicensedTo" field. */

    uint8_t    Reserved[2];             /*!< @brief Reserved. */
} ECAT_MASTER_DIAGNOSTIC_INFO;
#pragma pack()


/** @brief 从站端口状态   */
typedef enum
{
    ECAT_NDIAG_SLAVE_PORT_STATUS_IN_OUT_REVERSE = 0,        // IN OUT端口反接
    ECAT_NDIAG_SLAVE_PORT_STATUS_IN_DISCONNECT,             // IN端口连接断开
} EcatNetDiagnosticsSlavePortStatus;

/** @brief 从站拓扑节点   */
typedef struct
{
    char                                    byName[24];     // 从站名称
    uint32_t                                dwVendorId;     // 厂商ID
    uint32_t                                dwProductCode;  // 产品ID
    uint16_t                                wPort;          // 从站的端口，0-3：表示有效的端口，0xFFFF表示无效
    uint16_t                                wPhyPos;        // 从站在当前分支下的硬件接线顺序，从1开始
    uint16_t                                wIncAddr;       // 从站在当前拓扑中的顺序自增地址
    uint16_t                                wFixAddr;       // 从站在ENI中的物理地址，0xFFFF表示无效
    uint16_t                                wAlias;         // 当前从站的别名值，ESC寄存器0x0012的值
    uint16_t                                wRequestingID;  // 当前从站的请求ID，ESC寄存器0x0134的值
    uint16_t                                dwRes[2];       // 保留，未定义，固定为0
}EcatNetDiagnosticsSlaveTopologyNode;

/** @brief 从站拓扑   */
typedef struct
{
    uint8_t                                 byStatus;       // 下标0的从站的状态，见 #EcatNetDiagnosticsSlavePortStatus
    uint8_t                                 res[3];
    uint32_t                                dwTopologyNum;  // 拓扑的有效数量
    EcatNetDiagnosticsSlaveTopologyNode     tTopology[10];  // 从站的当前拓扑，它是一颗树结构
                                                            // 下标0指向诊断出现错误的从站
                                                            // 下标1-9，表示其上1-9级父结点，最多输出10级
}EcatNetDiagnosticsSlaveTopology;


/*! @brief Retrieves diagnostic information for master and/or slaves.

    @ingroup master_diagnostics
    @param hHandle EtherCAT handle (for Master handle refer to #EcatOpen function).
    @param pMasterDiagInfo Pointer to structure that receives the master's diagnostic information.
    @param wSlaveCount Number of slaves to retrieve diagnostic information.
    @param pwSlaveIds Pointer to array containing the Id's of slaves to retrieve diagnostic info.
    @param pSlaveDiagInfo Pointer to array that receives slaves' diagnostic information.
    @return #ECAT_S_OK,#ECAT_E_INVALID_POINTER.\n
            For more information about result
            see EcatGetErrorMessage(), EcatGetLastError().
    @see EcatGetMasterState, EcatGetSlaveCount
*/
EXPORT int32_t EcatGetDiagnostics(
    IN ECAT_HANDLE                   hHandle,
    OUT ECAT_MASTER_DIAGNOSTIC_INFO* pMasterDiagInfo,
    IN uint16_t                      wSlaveCount,
    IN const uint16_t *              pwSlaveIds,
    OUT ECAT_SLAVE_DIAGNOSTIC_INFO*  pSlaveDiagInfo);

/*! @brief Acquires diagnostic states for master and/or slaves.

    @ingroup master_diagnostics
    @param hHandle EtherCAT handle (for Master handle refer to #EcatOpen function).
    @param pdwMasterDiagState Pointer to variable that receives the master's diagnostic state.
    See also #EcatMasterDiagnosticState.
    @param wSlaveCount Number of slaves to retrieve diagnostic information.
    @param pwSlaveIds Pointer to array containing the Id's of slaves to retrieve diagnostic states.
    @param pdwSlaveDiagState Pointer to array that receives slaves' diagnostic states.
    @param pSIIContent Pointer to array that receives the SII contents of
           each slave mentioned in pwSlaveId array. Each element of
           the pSIIContent array contains Vendor ID, Product Code, Revision
           Number and Serial Number (see #ECAT_SII_CONST_CONTENT_SHORT).
    @return #ECAT_S_OK,#ECAT_E_INVALID_POINTER.\n
            For more information about result
            see EcatGetErrorMessage(), EcatGetLastError().
    @see EcatGetMasterState, EcatGetSlaveCount
*/
EXPORT int32_t EcatGetSlaveDiagnosticStates(
    IN ECAT_HANDLE              hHandle,
    OUT uint32_t*               pdwMasterDiagState,
    IN uint16_t                 wSlaveCount,
    IN const uint16_t*          pwSlaveIds,
    OUT uint32_t*               pdwSlaveDiagState,
    OUT ECAT_SLAVE_IDENTITY*    pSIIContent);

/*! @brief Retrieves the last error message.

    This function retrieves severity, location and message string for
    the last error occurred.
    This function copies not more than dwMaxBuffLen characters from
    the message string to the array pointed to by pszMessageBuff.
    Thus, if there is no null-terminator among the first dwMaxBuffLen
    characters of the message string, the result will not be null-terminated.
    Sequential calls to this function return remaining part of the message
    string until all characters of the message are returned.
    New messages that came while retrieving current message remain
    in an internal buffer and can be obtained later.
    Normally a caller of this function should supply a large enough
    message buffer in order to get a complete message string.
    Alternatively user can call the function sequentially,
    reading as many characters as the size of message buffer allows,
    until pdwRealMessageLen becomes less or equal to dwMaxBuffLen(that means
    that the last fragment is obtained).

    @ingroup master_err_handling
    @param hHandle EtherCAT handle (for Master handle refer to #EcatOpen function).
    @param pwSeverity Pointer to variable that receives severity of error message.
    @param pwLocation Pointer to variable that receives location of error message.
    @param pszMessageBuff Pointer to the buffer that receives the message string.
    @param dwMaxBuffLen Maximum number of characters the buffer can hold.
    @param pdwRealMessageLen Pointer to variable that receives the real length
           of the remaining message string(excluding the terminating '\\0' character).

    @return #ECAT_S_OK,\n
            #ECAT_E_INVALID_POINTER,\n
            #ECAT_E_INVALIDARG,\n
            #ECAT_S_NOITEMS.\n
            For more information about result
            see EcatGetErrorMessage(), EcatGetLastError().
    @see EcatGetErrorMessage,
         EcatSetTraceCategories,EcatGetTraceCategories
*/
EXPORT ECAT_RESULT EcatGetLastError(
    IN  ECAT_HANDLE hHandle,
    OUT char*       pszMessageBuff,
    IN  uint32_t    dwSize,
    OUT uint32_t  * pdwRealSize);

/**
 * @brief EcatNetDiagnosticsGetSlaveLinkStatus
 * 网络诊断并返回连接有问题的从站拓扑
 *
 * @param {ECAT_HANDLE} [IN]hHandle  主站句柄，见 #EcatOpen。
 * @param {EcatNetDiagnosticsSlaveInfo *} [out]pErrorInfos  存储错误的从站信息。
 * @param {uint16_t } [in,out]pwErrorInfoNum  输入作为pErrorInfos的最大数量，输出作为错误的数量，如果为0，则表示从站均正常。
 *
 * @return 返回RET_OK表示成功，没有异常，否则接收调用失败。
 */
EXPORT int32_t EcatNetDiagnosticsGetSlaveLinkStatus(
    IN ECAT_HANDLE                              hHandle,
    IN OUT EcatNetDiagnosticsSlaveTopology     *pErrorTopologys,
    IN OUT uint16_t                            *pwErrorTopologysNum);

#ifdef __cplusplus
}
#endif

#endif  // ZMC_PCI_DIAGNOSTIC_H_
/* end of file */
