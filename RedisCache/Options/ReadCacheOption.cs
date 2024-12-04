namespace RedisCache.Options;

public class ReadCacheOption
{
    public int ReadDbNumber { get; set; }
    public int Capacity { get; set; }
    public int ExpiryTime { get; set; }
    public ReadCacheOption() { }
    public ReadCacheOption(int capacity, int expiryTime, int readDbNumber)
    {
        Capacity = capacity;
        ExpiryTime = expiryTime;
        ReadDbNumber = readDbNumber;
    }
}
