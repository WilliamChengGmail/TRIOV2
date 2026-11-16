namespace TRIOV2.DbIoSimulator.Core;

/// <summary>
/// 固定容量之高效能環形緩衝區 (供 60 秒即時 I/O 折線圖使用，無動態記憶體配置)
/// 製作者: Office of William
/// </summary>
public sealed class PerformanceRingBuffer
{
    private readonly float[] _buffer;
    private readonly int _capacity;
    private readonly object _lock = new();
    private int _head;
    private int _count;

    public PerformanceRingBuffer(int capacity = 60, float initialValue = 0f)
    {
        _capacity = capacity > 0 ? capacity : 60;
        _buffer = new float[_capacity];
        if (initialValue != 0f)
        {
            Array.Fill(_buffer, initialValue);
            _count = _capacity;
        }
    }

    public int Capacity => _capacity;

    /// <summary>
    /// 寫入一筆新數據 (覆蓋最舊一筆)
    /// </summary>
    public void Enqueue(float value)
    {
        lock (_lock)
        {
            _buffer[_head] = value;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity)
            {
                _count++;
            }
        }
    }

    /// <summary>
    /// 清空緩衝區並重設為 0
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer);
            _head = 0;
            _count = 0;
        }
    }

    /// <summary>
    /// 複製當前緩衝區中按時間由舊至新的數據至目標陣列中 (長度固定為 capacity)
    /// </summary>
    public void CopyTo(float[] destination)
    {
        if (destination == null || destination.Length < _capacity)
        {
            throw new ArgumentException($"目標陣列長度必須至少為 {_capacity}", nameof(destination));
        }

        lock (_lock)
        {
            if (_count < _capacity)
            {
                int zeros = _capacity - _count;
                Array.Clear(destination, 0, zeros);
                Array.Copy(_buffer, 0, destination, zeros, _count);
            }
            else
            {
                int tailCount = _capacity - _head;
                Array.Copy(_buffer, _head, destination, 0, tailCount);
                if (_head > 0)
                {
                    Array.Copy(_buffer, 0, destination, tailCount, _head);
                }
            }
        }
    }

    /// <summary>
    /// 取得緩衝區內目前的最大數值 (用於動態 Y 軸縮放)
    /// </summary>
    public float GetMax()
    {
        lock (_lock)
        {
            if (_count == 0) return 0f;
            float max = 0f;
            for (int i = 0; i < _count; i++)
            {
                if (_buffer[i] > max)
                {
                    max = _buffer[i];
                }
            }
            return max;
        }
    }
}

