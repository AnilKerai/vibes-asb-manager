namespace Vibes.ASBManager.Web.Models
{
    public sealed class AddConnectionRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }
}
