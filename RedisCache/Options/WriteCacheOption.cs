namespace RedisCache.Options;

public class WriteCacheOption
{
    //数据库
    public int WriteDbNumber { get; set; }
    //写入阈值
    public int Threhold { get; set; }
    //是否启用轮询
    public bool IsPolling { get; set; }
    //轮询间隔
    public TimeSpan Interval { get; set; }

    public WriteCacheOption() { }

    public WriteCacheOption( int writeDbNumber, int threshold, bool isPolling, TimeSpan interval)
    {
        WriteDbNumber = writeDbNumber;
        Threhold = threshold;
        IsPolling = isPolling;
        Interval = interval;
    }
}