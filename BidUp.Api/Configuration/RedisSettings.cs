namespace BidUp.Api.Configuration;

public class RedisSettings
{
	public string ConnectionString { get; set; } = "localhost:6379";
	public bool Enabled { get; set; } = true;
}
