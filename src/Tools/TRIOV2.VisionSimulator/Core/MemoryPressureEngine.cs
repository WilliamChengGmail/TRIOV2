namespace TRIOV2.VisionSimulator.Core;

/// <summary>
/// 記憶體與匯流排壓力引擎 (分塊架構，支援 50MB ~ 8192MB，無 2GB 陣列溢位限制，具備 OOM 熔斷保護)
/// 製作者: Office of William
/// </summary>
public sealed class MemoryPressureEngine : IDisposable
{
    private const int ChunkSizeMb = 128; // 每個 Chunk 128 MB (134,217,728 bytes)，完全避免 int 溢位與 CLR 2GB 陣列限制
    private const int ChunkSizeBytes = ChunkSizeMb * 1024 * 1024;

    private readonly List<byte[]> _chunks = new();
    private int _allocatedMb;
    private readonly object _bufferLock = new();
    private long _accessCounter;

    public int AllocatedMb => _allocatedMb;

    public MemoryPressureEngine(int initialMb = 270)
    {
        Allocate(initialMb);
    }

    /// <summary>
    /// 動態配置或調整記憶體大小 (支援超大記憶體，自動分塊與安全保護)
    /// </summary>
    public void Allocate(int megabytes)
    {
        lock (_bufferLock)
        {
            if (megabytes <= 0) megabytes = 50;
            if (megabytes > 8192) megabytes = 8192; // 上限 8GB

            if (_allocatedMb == megabytes && _chunks.Count > 0)
            {
                return;
            }

            try
            {
                var newChunks = new List<byte[]>();
                int remainingMb = megabytes;
                int actualAllocated = 0;

                while (remainingMb > 0)
                {
                    int currentChunkMb = Math.Min(remainingMb, ChunkSizeMb);
                    int currentChunkBytes = currentChunkMb * 1024 * 1024;

                    try
                    {
                        var chunk = new byte[currentChunkBytes];
                        // 首次寫入觸發 Windows 實體分頁提交 (Commit physical pages)
                        for (int i = 0; i < currentChunkBytes; i += 4096)
                        {
                            chunk[i] = (byte)(i & 0xFF);
                        }

                        newChunks.Add(chunk);
                        actualAllocated += currentChunkMb;
                        remainingMb -= currentChunkMb;
                    }
                    catch (OutOfMemoryException)
                    {
                        // 若系統記憶體已達物理極限，中斷後續分塊配置，保留已成功配置的記憶體
                        break;
                    }
                }

                _chunks.Clear();
                _chunks.AddRange(newChunks);
                _allocatedMb = actualAllocated;
            }
            catch (Exception)
            {
                // 保底防禦，避免任何例外導致程式閃退
            }
        }
    }

    /// <summary>
    /// 在 CPU 工作週期中進行跨快取行走訪 (Stride = 64 Bytes，逼迫 CPU 產生 L3 Cache Miss 並佔用記憶體頻寬)
    /// </summary>
    public void PerformMemorySweep(int iterations = 1000)
    {
        List<byte[]> chunks;
        lock (_bufferLock)
        {
            chunks = _chunks;
            if (chunks.Count == 0) return;
        }

        long seed = Interlocked.Increment(ref _accessCounter);
        int chunkIndex = (int)(Math.Abs(seed) % chunks.Count);
        if (chunkIndex >= chunks.Count) return;

        byte[] buffer = chunks[chunkIndex];
        int len = buffer.Length;
        if (len < 1024) return;

        int stride = 64; // x86 CPU Cache-Line 典型大小為 64 Bytes
        int startIndex = (int)((Math.Abs(seed * 1021)) % (len - 1024));

        int sum = 0;
        int count = Math.Min(iterations, (len - startIndex) / stride);
        for (int i = 0; i < count; i++)
        {
            int idx = startIndex + (i * stride);
            sum += buffer[idx];
            buffer[idx] = (byte)(sum & 0xFF); // 讀寫同時發生
        }
    }

    public void Dispose()
    {
        lock (_bufferLock)
        {
            _chunks.Clear();
            _allocatedMb = 0;
            GC.Collect();
        }
    }
}

