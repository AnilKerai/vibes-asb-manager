namespace Vibes.ASBManager.Web.Models;

public class CreateSqlRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string SqlExpression { get; set; } = string.Empty;
    public string? SqlAction { get; set; }
}
