namespace RedisCache.Options;

public class RedisOptions
{
    //redis链接
    public string ConnectionString { get; set; }
    //数据库
    public int DbNumber { get; set; }
    //写入阈值
    public int Threhold { get; set; }
    //是否启用轮询
    public bool IsPolling { get; set; }
    //轮询间隔
    public TimeSpan Interval { get; set; }

    public RedisOptions() { }

    public RedisOptions(string connectionString, int dbNumber, int threshold, bool isPolling, TimeSpan interval)
    {
        ConnectionString = connectionString;
        DbNumber = dbNumber;
        Threhold = threshold;
        IsPolling = isPolling;
        Interval = interval;
    }
}