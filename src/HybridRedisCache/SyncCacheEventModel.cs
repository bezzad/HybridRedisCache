namespace HybridRedisCache;

internal class SyncCacheEventModel
{
    public string Value { get; set; }
    public TimeSpan ExpiryDate { get; set; }
    public string Key { get; set; }
    public string EventCreatorIdentifier { get; set; }
}