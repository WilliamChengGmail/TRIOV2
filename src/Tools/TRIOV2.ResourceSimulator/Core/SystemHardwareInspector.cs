using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace TRIOV2.ResourceSimulator.Core;

/// <summary>
/// Windows 工作管理員風格之硬體規格與系統環境詳細資訊快照
/// 製作者: Office of William
/// </summary>
public sealed class HardwareSystemSnapshot
{
    // CPU
    public string CpuModel { get; init; } = "未知";
    public string CpuBaseSpeedText { get; init; } = "未知";
    public int CpuSockets { get; init; }
    public int CpuCores { get; init; }
    public int CpuLogicalProcessors { get; init; }
    public string VirtualizationText { get; init; } = "未知";
    public string L1CacheText { get; init; } = "未知";
    public string L2CacheText { get; init; } = "未知";
    public string L3CacheText { get; init; } = "未知";

    // Memory
    public string MemorySpecType { get; init; } = "未知";
    public string MemorySpeedText { get; init; } = "未知";
    public string MemorySlotsUsedText { get; init; } = "未知";
    public string MemoryFormFactor { get; init; } = "未知";
    public string MemoryPhysicalTotalText { get; init; } = "未知";
    public string MemoryHardwareReservedText { get; init; } = "未知";

    // NPU & GPU
    public IReadOnlyList<string> NpuDevices { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GpuDevices { get; init; } = Array.Empty<string>();

    // Operating System
    public string OsName { get; init; } = "未知";
    public string OsVersion { get; init; } = "未知";
    public string OsBuild { get; init; } = "未知";
    public string OsArchitecture { get; init; } = "未知";
    public string MachineName { get; init; } = "未知";
}

/// <summary>
/// 透過 Win32 API、DXGI COM 與 WMI 採集工作管理員等級之系統硬體與處理程序資訊
/// 製作者: Office of William
/// </summary>
public sealed class SystemHardwareInspector
{
    private readonly object _cacheLock = new();
    private HardwareSystemSnapshot? _cachedSnapshot;
    private Task<HardwareSystemSnapshot>? _warmupTask;

    #region Win32 API & DXGI Interop

    private const int RelationProcessorCore = 0;
    private const int RelationCache = 2;
    private const int RelationProcessorPackage = 3;
    private const uint DxgiAdapterFlagSoftware = 2;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetLogicalProcessorInformationEx(
        int relationshipType,
        IntPtr buffer,
        ref uint returnedLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll")]
    private static extern bool IsProcessorFeaturePresent(uint processorFeature);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("dxgi.dll", PreserveSig = true)]
    private static extern int CreateDXGIFactory1(in Guid riid, [MarshalAs(UnmanagedType.Interface)] out IDXGIFactory1? ppFactory);

    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory1
    {
        // IDXGIObject (slots 3..6)
        [PreserveSig] int SetPrivateData(in Guid name, uint dataSize, IntPtr data);
        [PreserveSig] int SetPrivateDataInterface(in Guid name, [MarshalAs(UnmanagedType.IUnknown)] object? unknown);
        [PreserveSig] int GetPrivateData(in Guid name, ref uint dataSize, IntPtr data);
        [PreserveSig] int GetParent(in Guid riid, out IntPtr parent);

        // IDXGIFactory (slots 7..11)
        [PreserveSig] int EnumAdapters(uint adapter, out IntPtr ppAdapter);
        [PreserveSig] int MakeWindowAssociation(IntPtr windowHandle, uint flags);
        [PreserveSig] int GetWindowAssociation(out IntPtr windowHandle);
        [PreserveSig] int CreateSwapChain([MarshalAs(UnmanagedType.IUnknown)] object device, IntPtr desc, out IntPtr swapChain);
        [PreserveSig] int CreateSoftwareAdapter(IntPtr module, out IntPtr adapter);

        // IDXGIFactory1 (slots 12..13)
        [PreserveSig] int EnumAdapters1(uint adapter, [MarshalAs(UnmanagedType.Interface)] out IDXGIAdapter1? ppAdapter);
        [PreserveSig][return: MarshalAs(UnmanagedType.Bool)] bool IsCurrent();
    }

    [ComImport]
    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter1
    {
        // IDXGIObject (slots 3..6)
        [PreserveSig] int SetPrivateData(in Guid name, uint dataSize, IntPtr data);
        [PreserveSig] int SetPrivateDataInterface(in Guid name, [MarshalAs(UnmanagedType.IUnknown)] object? unknown);
        [PreserveSig] int GetPrivateData(in Guid name, ref uint dataSize, IntPtr data);
        [PreserveSig] int GetParent(in Guid riid, out IntPtr parent);

        // IDXGIAdapter (slots 7..9)
        [PreserveSig] int EnumOutputs(uint output, out IntPtr ppOutput);
        [PreserveSig] int GetDesc(IntPtr desc);
        [PreserveSig] int CheckInterfaceSupport(in Guid interfaceName, out long umdVersion);

        // IDXGIAdapter1 (slot 10)
        [PreserveSig] int GetDesc1(out DXGI_ADAPTER_DESC1 desc);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }

    #endregion

    /// <summary>
    /// 在背景執行緒提前暖機查詢硬體規格，讓後續點擊「複製」時可零延遲取得結果
    /// </summary>
    public void WarmupAsync()
    {
        lock (_cacheLock)
        {
            if (_cachedSnapshot != null || _warmupTask != null) return;
            _warmupTask = Task.Run(CollectSnapshotSafe);
        }
    }

    /// <summary>
    /// 取得系統靜態硬體資訊快照 (支援快取與背景暖機等待)
    /// </summary>
    public HardwareSystemSnapshot GetHardwareSnapshot()
    {
        Task<HardwareSystemSnapshot>? pendingTask;
        lock (_cacheLock)
        {
            if (_cachedSnapshot != null) return _cachedSnapshot;
            pendingTask = _warmupTask ??= Task.Run(CollectSnapshotSafe);
        }

        return pendingTask.GetAwaiter().GetResult();
    }

    /// <summary>
    /// 即時取得目前 Windows 系統所有處理程序總數 (相當於工作管理員處理程序總數)
    /// </summary>
    public static int GetTotalProcessCount()
    {
        Process[] all = Process.GetProcesses();
        int count = all.Length;
        foreach (var p in all)
        {
            try { p.Dispose(); } catch { }
        }
        return count;
    }

    /// <summary>
    /// 產生對齊工作管理員資訊之格式化純文字報告區段
    /// </summary>
    public string BuildTaskManagerHardwareSection(int currentUserVisibleProcessCount)
    {
        HardwareSystemSnapshot hw = GetHardwareSnapshot();
        int totalProcesses = GetTotalProcessCount();

        var sb = new StringBuilder();
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("【系統硬體規格與工作管理員參考資訊】");

        sb.AppendLine("► CPU 處理器資訊：");
        sb.AppendLine($"  - 型號         : {hw.CpuModel}");
        sb.AppendLine($"  - 基本速度     : {hw.CpuBaseSpeedText}");
        sb.AppendLine($"  - 插槽         : {hw.CpuSockets}");
        sb.AppendLine($"  - 核心數目     : {hw.CpuCores}");
        sb.AppendLine($"  - 邏輯處理器   : {hw.CpuLogicalProcessors}");
        sb.AppendLine($"  - 模擬         : {hw.VirtualizationText}");
        sb.AppendLine($"  - L1 快取      : {hw.L1CacheText}");
        sb.AppendLine($"  - L2 快取      : {hw.L2CacheText}");
        sb.AppendLine($"  - L3 快取      : {hw.L3CacheText}");

        sb.AppendLine("► 記憶體 (RAM) 資訊：");
        sb.AppendLine($"  - 記憶體規格   : {hw.MemorySpecType}");
        sb.AppendLine($"  - 速度         : {hw.MemorySpeedText}");
        sb.AppendLine($"  - 已使用插槽   : {hw.MemorySlotsUsedText}");
        sb.AppendLine($"  - 尺寸         : {hw.MemoryFormFactor}");
        sb.AppendLine($"  - 硬體保留     : {hw.MemoryHardwareReservedText}");
        sb.AppendLine($"  - 已安裝容量   : {hw.MemoryPhysicalTotalText}");

        sb.AppendLine("► NPU 資訊：");
        if (hw.NpuDevices.Count == 0)
        {
            sb.AppendLine("  - 未偵測到獨立 NPU 裝置");
        }
        else
        {
            foreach (string npu in hw.NpuDevices)
            {
                sb.AppendLine($"  - {npu}");
            }
        }

        sb.AppendLine("► GPU 資訊：");
        if (hw.GpuDevices.Count == 0)
        {
            sb.AppendLine("  - 未偵測到 GPU 顯示介面卡");
        }
        else
        {
            foreach (string gpu in hw.GpuDevices)
            {
                sb.AppendLine($"  - {gpu}");
            }
        }

        sb.AppendLine("► 系統程序總數：");
        sb.AppendLine($"  - 當前所有處理程序總數 : {totalProcesses} 個 (當前登入帳戶可見: {currentUserVisibleProcessCount} 個)");

        TimeSpan uptime = TimeSpan.FromMilliseconds(Math.Max(0, Environment.TickCount64));
        string uptimeText = $"{(int)uptime.TotalDays} 天 {uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";

        sb.AppendLine("► 作業系統資訊：");
        sb.AppendLine($"  - 作業系統名稱 : {hw.OsName}");
        sb.AppendLine($"  - 版本         : {hw.OsVersion}");
        sb.AppendLine($"  - OS 組建      : {hw.OsBuild}");
        sb.AppendLine($"  - 系統類型     : {hw.OsArchitecture}");
        sb.AppendLine($"  - 裝置名稱     : {hw.MachineName}");
        sb.AppendLine($"  - 已開機時間   : {uptimeText}");

        return sb.ToString().TrimEnd('\r', '\n');
    }

    private HardwareSystemSnapshot CollectSnapshotSafe()
    {
        var snap = CollectSnapshot();
        lock (_cacheLock)
        {
            _cachedSnapshot = snap;
        }
        return snap;
    }

    private static HardwareSystemSnapshot CollectSnapshot()
    {
        // 1. Win32 Topology & Caches
        var (win32Sockets, win32Cores, l1Bytes, l2Bytes, l3Bytes) = QueryProcessorTopologyAndCaches();

        // 2. WMI CPU & Hypervisor
        var cpuWmi = QueryCpuFromWmi();
        string cpuModel = !string.IsNullOrWhiteSpace(cpuWmi.Name)
            ? cpuWmi.Name
            : ReadCpuNameFromRegistry();

        string baseSpeed = cpuWmi.MaxClockMhz > 0
            ? $"{cpuWmi.MaxClockMhz / 1000.0:0.00} GHz"
            : ReadCpuSpeedFromRegistry();

        int sockets = win32Sockets > 0 ? win32Sockets : Math.Max(1, cpuWmi.Sockets);
        int cores = win32Cores > 0 ? win32Cores : Math.Max(1, cpuWmi.Cores);
        int logicalProcessors = Environment.ProcessorCount;

        // 3. 快取文字 (優先採用 Win32 API 實際區塊加總，與工作管理員 100% 一致)
        ulong finalL2 = l2Bytes > 0 ? l2Bytes : cpuWmi.L2BytesFallback;
        ulong finalL3 = l3Bytes > 0 ? l3Bytes : cpuWmi.L3BytesFallback;

        // 4. Memory WMI & Win32
        var memInfo = QueryMemoryInfo();

        // 5. NPU & GPU
        var npuList = QueryNpuDevices();
        var gpuList = QueryGpuDevices();

        // 6. Operating System
        var osInfo = QueryOperatingSystemInfo();

        return new HardwareSystemSnapshot
        {
            CpuModel = cpuModel,
            CpuBaseSpeedText = baseSpeed,
            CpuSockets = sockets,
            CpuCores = cores,
            CpuLogicalProcessors = logicalProcessors,
            VirtualizationText = cpuWmi.VirtualizationText,
            L1CacheText = FormatCacheSize(l1Bytes),
            L2CacheText = FormatCacheSize(finalL2),
            L3CacheText = FormatCacheSize(finalL3),
            MemorySpecType = memInfo.SpecType,
            MemorySpeedText = memInfo.SpeedText,
            MemorySlotsUsedText = memInfo.SlotsUsedText,
            MemoryFormFactor = memInfo.FormFactor,
            MemoryPhysicalTotalText = memInfo.PhysicalTotalText,
            MemoryHardwareReservedText = memInfo.HardwareReservedText,
            NpuDevices = npuList,
            GpuDevices = gpuList,
            OsName = osInfo.OsName,
            OsVersion = osInfo.OsVersion,
            OsBuild = osInfo.OsBuild,
            OsArchitecture = osInfo.OsArchitecture,
            MachineName = osInfo.MachineName
        };
    }

    private static (int Sockets, int Cores, ulong L1Bytes, ulong L2Bytes, ulong L3Bytes) QueryProcessorTopologyAndCaches()
    {
        int sockets = CountLogicalRelationship(RelationProcessorPackage);
        int cores = CountLogicalRelationship(RelationProcessorCore);
        ulong l1 = 0, l2 = 0, l3 = 0;

        try
        {
            uint len = 0;
            GetLogicalProcessorInformationEx(RelationCache, IntPtr.Zero, ref len);
            if (len > 0)
            {
                IntPtr buffer = Marshal.AllocHGlobal((int)len);
                try
                {
                    if (GetLogicalProcessorInformationEx(RelationCache, buffer, ref len))
                    {
                        long offset = 0;
                        while (offset + 16 <= len)
                        {
                            IntPtr ptr = IntPtr.Add(buffer, (int)offset);
                            int rel = Marshal.ReadInt32(ptr, 0);
                            int size = Marshal.ReadInt32(ptr, 4);
                            if (size <= 0) break;

                            if (rel == RelationCache)
                            {
                                // CACHE_RELATIONSHIP: Offset 8 = Level (byte), Offset 12 = CacheSize (DWORD)
                                byte level = Marshal.ReadByte(ptr, 8);
                                uint cacheSize = (uint)Marshal.ReadInt32(ptr, 12);
                                switch (level)
                                {
                                    case 1: l1 += cacheSize; break;
                                    case 2: l2 += cacheSize; break;
                                    case 3: l3 += cacheSize; break;
                                }
                            }
                            offset += size;
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        catch
        {
        }

        return (sockets, cores, l1, l2, l3);
    }

    private static int CountLogicalRelationship(int relationshipType)
    {
        int count = 0;
        try
        {
            uint len = 0;
            GetLogicalProcessorInformationEx(relationshipType, IntPtr.Zero, ref len);
            if (len == 0) return 0;

            IntPtr buffer = Marshal.AllocHGlobal((int)len);
            try
            {
                if (GetLogicalProcessorInformationEx(relationshipType, buffer, ref len))
                {
                    long offset = 0;
                    while (offset + 8 <= len)
                    {
                        IntPtr ptr = IntPtr.Add(buffer, (int)offset);
                        int rel = Marshal.ReadInt32(ptr, 0);
                        int size = Marshal.ReadInt32(ptr, 4);
                        if (size <= 0) break;

                        if (rel == relationshipType) count++;
                        offset += size;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
        }
        return count;
    }

    private static (string Name, uint MaxClockMhz, int Sockets, int Cores, string VirtualizationText, ulong L2BytesFallback, ulong L3BytesFallback) QueryCpuFromWmi()
    {
        string name = string.Empty;
        uint maxClock = 0;
        int procCount = 0;
        int totalCores = 0;
        bool virtFirmwareEnabled = false;
        ulong l2Bytes = 0;
        ulong l3Bytes = 0;
        bool hypervisorPresent = false;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, MaxClockSpeed, NumberOfCores, VirtualizationFirmwareEnabled, L2CacheSize, L3CacheSize FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                procCount++;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = (obj["Name"]?.ToString() ?? string.Empty).Trim();
                }

                uint clk = SafeToUInt32(obj["MaxClockSpeed"]);
                if (clk > maxClock) maxClock = clk;

                totalCores += (int)SafeToUInt32(obj["NumberOfCores"]);
                if (obj["VirtualizationFirmwareEnabled"] is bool vf && vf) virtFirmwareEnabled = true;

                l2Bytes += (ulong)SafeToUInt32(obj["L2CacheSize"]) * 1024UL;
                l3Bytes += (ulong)SafeToUInt32(obj["L3CacheSize"]) * 1024UL;
            }
        }
        catch
        {
        }

        try
        {
            using var csSearcher = new ManagementObjectSearcher("SELECT NumberOfProcessors, HypervisorPresent FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in csSearcher.Get())
            {
                int csSockets = (int)SafeToUInt32(obj["NumberOfProcessors"]);
                if (csSockets > 0) procCount = csSockets;
                if (obj["HypervisorPresent"] is bool hp && hp) hypervisorPresent = true;
            }
        }
        catch
        {
        }

        // 壓縮連續多餘空白 (例如部分 Intel 型號字串帶有多個連續空白)
        if (!string.IsNullOrWhiteSpace(name))
        {
            name = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        string virtText;
        if (virtFirmwareEnabled || IsProcessorFeaturePresent(21))
        {
            virtText = hypervisorPresent ? "已啟用 (Hypervisor 運行中)" : "已啟用";
        }
        else if (hypervisorPresent)
        {
            virtText = "已啟用 (虛擬環境 / Hypervisor)";
        }
        else
        {
            virtText = "已停用";
        }

        return (name, maxClock, procCount, totalCores, virtText, l2Bytes, l3Bytes);
    }

    private static (string SpecType, string SpeedText, string SlotsUsedText, string FormFactor, string PhysicalTotalText, string HardwareReservedText) QueryMemoryInfo()
    {
        int totalSlots = 0;
        int usedSlots = 0;
        ulong physicalSumBytes = 0;
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var speeds = new HashSet<uint>();
        var formFactors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var arraySearcher = new ManagementObjectSearcher("SELECT MemoryDevices, Use FROM Win32_PhysicalMemoryArray");
            foreach (ManagementObject obj in arraySearcher.Get())
            {
                // Use == 3 代表 System Memory；部分平台回傳 0 或無值時仍納入統計
                uint use = SafeToUInt32(obj["Use"]);
                if (use == 0 || use == 3)
                {
                    totalSlots += (int)SafeToUInt32(obj["MemoryDevices"]);
                }
            }
        }
        catch
        {
        }

        try
        {
            using var dimmSearcher = new ManagementObjectSearcher(
                "SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, MemoryType, FormFactor FROM Win32_PhysicalMemory");
            foreach (ManagementObject obj in dimmSearcher.Get())
            {
                usedSlots++;
                physicalSumBytes += SafeToUInt64(obj["Capacity"]);

                uint smbiosType = SafeToUInt32(obj["SMBIOSMemoryType"]);
                if (smbiosType == 0) smbiosType = SafeToUInt32(obj["MemoryType"]);
                string typeName = MapSmbiosMemoryType(smbiosType);
                if (!string.IsNullOrEmpty(typeName)) types.Add(typeName);

                uint clock = SafeToUInt32(obj["ConfiguredClockSpeed"]);
                if (clock == 0) clock = SafeToUInt32(obj["Speed"]);
                if (clock > 0) speeds.Add(clock);

                string ff = MapMemoryFormFactor(SafeToUInt32(obj["FormFactor"]));
                if (!string.IsNullOrEmpty(ff)) formFactors.Add(ff);
            }
        }
        catch
        {
        }

        // 取得系統實際安裝與 OS 可見實體記憶體以精確計算「硬體保留」
        ulong osVisibleBytes = 0;
        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref memStatus))
        {
            osVisibleBytes = memStatus.ullTotalPhys;
        }

        if (physicalSumBytes == 0 && GetPhysicallyInstalledSystemMemory(out ulong physKb))
        {
            physicalSumBytes = physKb * 1024UL;
        }

        ulong hardwareReservedBytes = physicalSumBytes > osVisibleBytes
            ? physicalSumBytes - osVisibleBytes
            : 0UL;

        string specText = types.Count > 0 ? string.Join(" / ", types) : "未知";
        string speedText = speeds.Count > 0 ? $"{string.Join(" / ", speeds)} MT/s" : "未知";
        string slotsText = totalSlots >= usedSlots && totalSlots > 0
            ? $"{usedSlots} / {totalSlots}"
            : usedSlots > 0
                ? $"{usedSlots} / {usedSlots}"
                : "未知";
        string formFactorText = formFactors.Count > 0 ? string.Join(" / ", formFactors) : "未知";
        string physTotalText = physicalSumBytes > 0
            ? $"{physicalSumBytes / (1024.0 * 1024.0 * 1024.0):0.00} GB (OS 可用 {osVisibleBytes / (1024.0 * 1024.0 * 1024.0):0.00} GB)"
            : $"{osVisibleBytes / (1024.0 * 1024.0 * 1024.0):0.00} GB";

        string reservedText = FormatBytesToMbOrKb(hardwareReservedBytes);

        return (specText, speedText, slotsText, formFactorText, physTotalText, reservedText);
    }

    private static IReadOnlyList<string> QueryNpuDevices()
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPClass, Manufacturer, Status, Description FROM Win32_PnPEntity " +
                "WHERE PNPClass = 'ComputeAccelerator' OR Name LIKE '%NPU%' OR Name LIKE '%AI Boost%' OR Name LIKE '%Neural%' OR Description LIKE '%NPU%'");

            foreach (ManagementObject obj in searcher.Get())
            {
                string name = (obj["Name"]?.ToString() ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;

                string mfg = (obj["Manufacturer"]?.ToString() ?? string.Empty).Trim();
                string status = (obj["Status"]?.ToString() ?? string.Empty).Trim();
                string pnpClass = (obj["PNPClass"]?.ToString() ?? string.Empty).Trim();

                var detailParts = new List<string>();
                if (!string.IsNullOrEmpty(pnpClass)) detailParts.Add($"類別: {pnpClass}");
                if (!string.IsNullOrEmpty(mfg)) detailParts.Add($"製造商: {mfg}");
                if (!string.IsNullOrEmpty(status)) detailParts.Add($"狀態: {status}");

                string suffix = detailParts.Count > 0 ? $" | {string.Join(" | ", detailParts)}" : string.Empty;
                list.Add($"{name}{suffix}");
            }
        }
        catch
        {
        }

        return list;
    }

    private static IReadOnlyList<string> QueryGpuDevices()
    {
        var results = new List<string>();
        var wmiMap = QueryWmiVideoControllers();
        var dxgiAdapters = QueryDxgiAdapters();

        if (dxgiAdapters.Count > 0)
        {
            var matchedWmiNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNamesCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int gpuIndex = 0;

            for (int i = 0; i < dxgiAdapters.Count; i++)
            {
                var adapter = dxgiAdapters[i];
                seenNamesCount.TryGetValue(adapter.Description, out int currentCount);

                // 若 WMI 已知該型號，避免 RDP / 間接顯示導致 DXGI 重複列舉同一張實體卡
                if (wmiMap.TryGetValue(adapter.Description, out var wmiMatch) &&
                    currentCount >= wmiMatch.Count)
                {
                    continue;
                }

                seenNamesCount[adapter.Description] = currentCount + 1;
                string dedText = FormatBytesToGbOrMb(adapter.DedicatedVideoMemory);
                string sharedText = FormatBytesToGbOrMb(adapter.SharedSystemMemory);

                string driverInfo = string.Empty;
                if (wmiMatch.Count > 0)
                {
                    matchedWmiNames.Add(adapter.Description);
                    if (!string.IsNullOrWhiteSpace(wmiMatch.DriverVersion))
                    {
                        driverInfo = $" | 驅動版本: {wmiMatch.DriverVersion}";
                    }
                }

                results.Add($"[GPU {gpuIndex++}] {adapter.Description} | 專屬記憶體: {dedText} | 共用記憶體: {sharedText}{driverInfo}");
            }

            // 補充 WMI 有列出但非硬體 DXGI 的遠端/虛擬配接器
            foreach (var kv in wmiMap)
            {
                if (matchedWmiNames.Contains(kv.Key)) continue;
                string vram = kv.Value.AdapterRamBytes > 0 ? FormatBytesToGbOrMb(kv.Value.AdapterRamBytes) : "系統共用/虛擬";
                string drv = !string.IsNullOrWhiteSpace(kv.Value.DriverVersion) ? $" | 驅動版本: {kv.Value.DriverVersion}" : string.Empty;
                results.Add($"[虛擬/遠端顯示] {kv.Key} | 記憶體: {vram}{drv}");
            }
        }
        else
        {
            int idx = 0;
            foreach (var kv in wmiMap)
            {
                string vram = kv.Value.AdapterRamBytes > 0 ? FormatBytesToGbOrMb(kv.Value.AdapterRamBytes) : "未知/共用";
                string drv = !string.IsNullOrWhiteSpace(kv.Value.DriverVersion) ? $" | 驅動版本: {kv.Value.DriverVersion}" : string.Empty;
                results.Add($"[GPU {idx++}] {kv.Key} | 記憶體: {vram}{drv}");
            }
        }

        return results;
    }

    private static List<(string Description, ulong DedicatedVideoMemory, ulong SharedSystemMemory)> QueryDxgiAdapters()
    {
        var hardwareAdapters = new List<(string Description, ulong DedicatedVideoMemory, ulong SharedSystemMemory)>();
        var softwareAdapters = new List<(string Description, ulong DedicatedVideoMemory, ulong SharedSystemMemory)>();
        var seenLuids = new HashSet<long>();

        try
        {
            Guid factoryGuid = typeof(IDXGIFactory1).GUID;
            int hr = CreateDXGIFactory1(in factoryGuid, out IDXGIFactory1? factory);
            if (hr != 0 || factory == null) return hardwareAdapters;

            try
            {
                uint index = 0;
                while (factory.EnumAdapters1(index, out IDXGIAdapter1? adapter) == 0 && adapter != null)
                {
                    try
                    {
                        if (adapter.GetDesc1(out DXGI_ADAPTER_DESC1 desc) == 0 &&
                            seenLuids.Add(desc.AdapterLuid))
                        {
                            string name = (desc.Description ?? string.Empty).Trim();
                            ulong dedMem = desc.DedicatedVideoMemory.ToUInt64();
                            ulong sharedMem = desc.SharedSystemMemory.ToUInt64();

                            if ((desc.Flags & DxgiAdapterFlagSoftware) != 0)
                            {
                                softwareAdapters.Add((name, dedMem, sharedMem));
                            }
                            else
                            {
                                hardwareAdapters.Add((name, dedMem, sharedMem));
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(adapter);
                    }
                    index++;
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
        }
        catch
        {
        }

        return hardwareAdapters.Count > 0 ? hardwareAdapters : softwareAdapters;
    }

    private static Dictionary<string, (int Count, ulong AdapterRamBytes, string DriverVersion)> QueryWmiVideoControllers()
    {
        var dict = new Dictionary<string, (int Count, ulong AdapterRamBytes, string DriverVersion)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                string name = (obj["Name"]?.ToString() ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name)) continue;

                ulong ram = SafeToUInt64(obj["AdapterRAM"]);
                string drv = (obj["DriverVersion"]?.ToString() ?? string.Empty).Trim();
                if (dict.TryGetValue(name, out var existing))
                {
                    dict[name] = (
                        existing.Count + 1,
                        Math.Max(existing.AdapterRamBytes, ram),
                        string.IsNullOrWhiteSpace(existing.DriverVersion) ? drv : existing.DriverVersion);
                }
                else
                {
                    dict[name] = (1, ram, drv);
                }
            }
        }
        catch
        {
        }
        return dict;
    }

    private static (string OsName, string OsVersion, string OsBuild, string OsArchitecture, string MachineName) QueryOperatingSystemInfo()
    {
        string osName = string.Empty;
        string wmiVersion = string.Empty;
        string wmiArch = string.Empty;
        string buildNumber = string.Empty;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Caption, Version, OSArchitecture, BuildNumber FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                osName = (obj["Caption"]?.ToString() ?? string.Empty).Trim();
                wmiVersion = (obj["Version"]?.ToString() ?? string.Empty).Trim();
                wmiArch = (obj["OSArchitecture"]?.ToString() ?? string.Empty).Trim();
                buildNumber = (obj["BuildNumber"]?.ToString() ?? string.Empty).Trim();
                break;
            }
        }
        catch
        {
        }

        string displayVersion = string.Empty;
        string regBuild = string.Empty;
        string regUbr = string.Empty;
        string regProductName = string.Empty;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                displayVersion = (key.GetValue("DisplayVersion")?.ToString()
                    ?? key.GetValue("ReleaseId")?.ToString()
                    ?? string.Empty).Trim();
                regBuild = (key.GetValue("CurrentBuildNumber")?.ToString()
                    ?? key.GetValue("CurrentBuild")?.ToString()
                    ?? string.Empty).Trim();
                regUbr = (key.GetValue("UBR")?.ToString() ?? string.Empty).Trim();
                regProductName = (key.GetValue("ProductName")?.ToString() ?? string.Empty).Trim();
            }
        }
        catch
        {
        }

        string finalOsName = !string.IsNullOrWhiteSpace(osName)
            ? string.Join(' ', osName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            : !string.IsNullOrWhiteSpace(regProductName)
                ? regProductName
                : RuntimeInformation.OSDescription;

        string baseBuild = !string.IsNullOrWhiteSpace(regBuild)
            ? regBuild
            : !string.IsNullOrWhiteSpace(buildNumber)
                ? buildNumber
                : Environment.OSVersion.Version.Build.ToString();

        string finalBuild = !string.IsNullOrWhiteSpace(regUbr)
            ? $"{baseBuild}.{regUbr}"
            : !string.IsNullOrWhiteSpace(wmiVersion)
                ? wmiVersion
                : baseBuild;

        string finalVersion = !string.IsNullOrWhiteSpace(displayVersion)
            ? displayVersion
            : !string.IsNullOrWhiteSpace(wmiVersion)
                ? wmiVersion
                : Environment.OSVersion.VersionString;

        string runtimeArch = RuntimeInformation.OSArchitecture.ToString();
        string finalArch = !string.IsNullOrWhiteSpace(wmiArch)
            ? $"{wmiArch} ({runtimeArch})"
            : runtimeArch;

        string machineName = !string.IsNullOrWhiteSpace(Environment.MachineName)
            ? Environment.MachineName
            : "未知";

        return (finalOsName, finalVersion, finalBuild, finalArch, machineName);
    }

    private static string ReadCpuNameFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            string? val = key?.GetValue("ProcessorNameString")?.ToString();
            if (!string.IsNullOrWhiteSpace(val))
            {
                return string.Join(' ', val.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
        }
        catch { }
        return "未知";
    }

    private static string ReadCpuSpeedFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key?.GetValue("~MHz") is int mhz && mhz > 0)
            {
                return $"{mhz / 1000.0:0.00} GHz";
            }
        }
        catch { }
        return "未知";
    }

    private static string MapSmbiosMemoryType(uint code) => code switch
    {
        1 => "Other",
        2 => "Unknown",
        3 => "DRAM",
        17 => "SDRAM",
        20 => "DDR",
        21 => "DDR2",
        22 => "DDR2 FB-DIMM",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => code > 0 ? $"SMBIOS Type ({code})" : "未知"
    };

    private static string MapMemoryFormFactor(uint code) => code switch
    {
        8 => "DIMM",
        9 => "SIP",
        10 => "DIP",
        11 => "ZIP",
        12 => "SODIMM",
        13 => "RIMM",
        14 => "SODIMM",
        15 => "SRIMM",
        16 => "FB-DIMM",
        _ => code > 0 ? $"FormFactor ({code})" : "未知"
    };

    private static string FormatCacheSize(ulong bytes)
    {
        if (bytes == 0) return "0 KB (無/未回報)";
        if (bytes >= 1024UL * 1024UL)
        {
            return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        }
        return $"{bytes / 1024.0:0} KB";
    }

    private static string FormatBytesToMbOrKb(ulong bytes)
    {
        if (bytes == 0) return "0.0 MB";
        if (bytes >= 1024UL * 1024UL)
        {
            return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        }
        return $"{bytes / (1024.0 * 1024.0):0.00} MB ({bytes / 1024.0:0} KB)";
    }

    private static string FormatBytesToGbOrMb(ulong bytes)
    {
        if (bytes == 0) return "0 MB";
        if (bytes >= 1024UL * 1024UL * 1024UL)
        {
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.0} GB";
        }
        return $"{bytes / (1024.0 * 1024.0):0} MB";
    }

    private static uint SafeToUInt32(object? value)
    {
        if (value == null) return 0;
        try { return Convert.ToUInt32(value); } catch { return 0; }
    }

    private static ulong SafeToUInt64(object? value)
    {
        if (value == null) return 0;
        try { return Convert.ToUInt64(value); } catch { return 0; }
    }
}
