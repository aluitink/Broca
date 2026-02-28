namespace Broca.ActivityPub.Server.Services.CollectionSearch;

public abstract record FilterNode;

public record ComparisonNode(string Property, ComparisonOperator Operator, object? Value) : FilterNode;

public record LogicalNode(FilterNode Left, LogicalOperator Operator, FilterNode Right) : FilterNode;

public record NotNode(FilterNode Inner) : FilterNode;

public record FunctionNode(string FunctionName, string Property, string Value) : FilterNode;

public enum ComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

public enum LogicalOperator
{
    And,
    Or
}
