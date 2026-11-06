using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ZlgEtherCat.Native;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PiArea : uint { LocalOutput, LocalInput, FastOutput, ShadowOutput, ShadowInput }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PiAccessMode : uint { Direct, Cycle, Manual }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PiReinitMode : uint { Auto, ClearAll, PreserveAsync, PreserveSync, PreserveAll }

public sealed record DriverDetails(string DriverVersion, string ApiVersion, uint BoardCount);
public sealed record BoardDetails(string Name, string Version, string SerialNumber, uint Alias, uint ChannelCount, uint FpgaVersion);
public sealed record EniDetails(uint MainCycle, uint MailboxCycle, uint OutputBytes, uint InputBytes);
public sealed record PiRange(uint Offset, uint Length);
public sealed record SlaveDetails(uint VendorId, uint ProductCode, uint SerialNumber, uint Revision, uint Alias, uint InputBits, uint OutputBits);
public sealed record WatchdogDetails(uint Type, uint TimeoutMs, byte[] Mask, byte[] Data);
public sealed record ForcedOutputDetails(uint CommandType, uint Offset, byte[] Data);
public sealed record PulseConfiguration(uint Mode, uint TriggerMode, uint Direction, uint Frequency, uint DutyPercent);
public sealed record PwmConfiguration(uint Frequency, uint DutyPercent);
public sealed record SlaveDiagnostics(uint State, uint VendorId, uint ProductCode, uint Revision, uint SerialNumber, byte[] PortCounters, byte ProcessingUnitErrors, byte PdiErrors);
public sealed record MasterDiagnostics(uint State, ushort NetworkState, ushort MainSlaveCount, ushort RedundancySlaveCount, IReadOnlyList<SlaveDiagnostics> Slaves);
public sealed record LinkTopologyNode(string Name, uint VendorId, uint ProductCode, ushort Port, ushort PhysicalPosition, ushort IncrementAddress, ushort FixedAddress, ushort Alias, ushort RequestingId);
public sealed record LinkFault(byte Status, IReadOnlyList<LinkTopologyNode> Topology);

/// <summary>
/// 仅提供同步 SDK 调用。调用方必须串行协调共享通道、给等待设置期限，并跟踪真实调用退出。
/// 此接口不拥有句柄生命周期，不自动重试、不自动恢复、不自动执行设备操作。
/// </summary>
public interface IZlgNativeApi
{
    DriverDetails GetDriverInfo();
    BoardDetails EnumerateBoard(uint index);
    uint GetQueueCapacity(uint boardIndex, uint channel);
    IntPtr Open(string boardInterface, uint channel);
    void Close(IntPtr handle);
    void SetTimeout(IntPtr handle, uint timeoutMs);
    void SetBoardTimeout(IntPtr boardHandle, uint timeoutMs);
    BoardDetails GetBoardInfo(IntPtr handle);
    IntPtr GetBoardHandle(IntPtr handle);
    void StartBus(IntPtr handle, string eniPath);
    void StopBus(IntPtr handle);
    void ResetChannel(IntPtr handle);
    uint GetBusStatus(IntPtr handle);
    bool IsBusRunning(IntPtr handle);
    EniDetails GetEniInfo(IntPtr handle);
    void CheckRunningEni(IntPtr handle, string eniPath);
    void SetPiReinitMode(IntPtr handle, PiReinitMode mode);
    PiReinitMode GetPiReinitMode(IntPtr handle);
    void RequestMasterState(IntPtr handle, byte state);
    byte GetMasterState(IntPtr handle);
    ushort GetSlaveCount(IntPtr handle, bool activeOnly = false);
    bool IsSlaveActive(IntPtr handle, ushort slave);
    bool IsSlaveReady(IntPtr handle, ushort slave);
    ushort GetSlaveFixedAddress(IntPtr handle, ushort slave);
    SlaveDetails GetSlaveInfo(IntPtr handle, ushort slave);
    byte GetSlaveState(IntPtr handle, ushort slave);
    ushort GetSlaveAlStatus(IntPtr handle, ushort slave);
    ushort GetSlaveDlStatus(IntPtr handle, ushort slave);
    PiRange GetSlavePiRange(IntPtr handle, PiArea area, uint slave);
    void ClearSlaveError(IntPtr handle, ushort slave);
    void SetAutoClearSlaveError(IntPtr handle, bool enabled);
    bool GetAutoClearSlaveError(IntPtr handle);
    void SetReadyLostTimeout(IntPtr handle, uint timeoutMs);
    uint GetReadyLostTimeout(IntPtr handle);
    void EnablePi(IntPtr handle, bool enabled);
    bool IsPiEnabled(IntPtr handle);
    void SetPiAccessMode(IntPtr handle, PiAccessMode mode);
    PiAccessMode GetPiAccessMode(IntPtr handle);
    void UpdateAsyncPi(IntPtr handle, PiArea area);
    byte[] ReadPi(IntPtr handle, PiArea area, uint offset, int length);
    void WritePi(IntPtr handle, PiArea area, uint offset, byte[] data);
    void MaskWritePi(IntPtr handle, PiArea area, uint offset, byte[] mask, byte[] data);
    void ClearPi(IntPtr handle, PiArea area);
    bool PopInput(IntPtr handle, bool latest, uint timeoutMs);
    void PushOutput(IntPtr handle, bool clearOldQueue, uint timeoutMs);
    bool WaitInputNotification(IntPtr handle, uint timeoutMs);
    uint GetInputQueueSize(IntPtr handle);
    uint GetOutputQueueSize(IntPtr handle);
    byte[] ReadSdo(IntPtr handle, ushort slave, ushort index, byte subIndex, int capacity, uint timeoutMs, bool completeAccess = false);
    void WriteSdo(IntPtr handle, ushort slave, ushort index, byte subIndex, byte[] data, uint timeoutMs, bool completeAccess = false);
    void ConfigureWatchdog(IntPtr handle, uint slave, byte[] mask, byte[] data);
    WatchdogDetails ReadWatchdog(IntPtr handle, uint slave, int capacity);
    void EnableWatchdog(IntPtr handle, uint timeoutMs);
    void FeedWatchdog(IntPtr handle);
    void ConfigureForcedOutput(IntPtr handle, uint slave, uint commandType, uint offset, byte[] data);
    ForcedOutputDetails ReadForcedOutput(IntPtr handle, uint slave, int capacity);
    MasterDiagnostics ReadDiagnostics(IntPtr handle, ushort[] slaves);
    IReadOnlyList<LinkFault> ReadLinkFaults(IntPtr handle, int capacity);
    string ReadLastError(IntPtr handle, int capacity);
    uint ReadFirstLinkDown(IntPtr handle);
    uint ReadBoardDi(IntPtr boardHandle);
    uint ReadBoardDo(IntPtr boardHandle);
    void WriteBoardDo(IntPtr boardHandle, uint value);
    void ConfigurePulse(IntPtr boardHandle, uint channel, PulseConfiguration configuration);
    PulseConfiguration ReadPulseConfiguration(IntPtr boardHandle, uint channel);
    void OutputPulse(IntPtr boardHandle, uint channel, uint count);
    void StopPulse(IntPtr boardHandle, uint channel);
    void OutputPwm(IntPtr boardHandle, uint channel, uint frequency, uint dutyPercent);
    PwmConfiguration ReadPwmConfiguration(IntPtr boardHandle, uint channel);
    void StopPwm(IntPtr boardHandle, uint channel);
    void EnableLatch(IntPtr boardHandle, bool enabled);
    void ConfigureLatch(IntPtr boardHandle, uint portMap, uint mode, bool continuous);
    void StartLatch(IntPtr boardHandle, bool continuous, uint count);
    void StopLatch(IntPtr boardHandle);
    void ClearLatch(IntPtr boardHandle, bool resetCounter);
    ulong ReadLatch(IntPtr boardHandle, uint channel, bool continuous, uint sampleIndex);
    uint ReadLatchState(IntPtr boardHandle, uint channel);
    void ReadFoe(IntPtr handle, ushort slave, string localPath, string remotePath);
    void WriteFoe(IntPtr handle, ushort slave, string localPath, string remotePath);
    byte[] ReadSlaveMemory(IntPtr handle, ushort slave, ushort address, ushort length, bool eeprom);
    void WriteSlaveMemory(IntPtr handle, ushort slave, ushort address, byte[] data, bool eeprom);
    void ResetBoard(IntPtr boardHandle);
    void ReadBoardFile(IntPtr boardHandle, string localPath, string remotePath);
    void WriteBoardFile(IntPtr boardHandle, string localPath, string remotePath);
    void UpdateBoard(IntPtr boardHandle, string localPath);
    void StartCapture(IntPtr handle, uint queueSize);
    byte[] ReadCapture(IntPtr handle, int capacity);
    void StopCapture(IntPtr handle);
}
