using Broca.ActivityPub.Server.Services.CollectionSearch;

namespace Broca.ActivityPub.UnitTests;

public class ODataFilterParserTests
{
    [Fact]
    public void Parse_SimpleEquality_ReturnsComparisonNode()
    {
        var result = ODataFilterParser.Parse("type eq 'Note'");

        var node = Assert.IsType<ComparisonNode>(result);
        Assert.Equal("type", node.Property);
        Assert.Equal(ComparisonOperator.Equal, node.Operator);
        Assert.Equal("Note", node.Value);
    }

    [Fact]
    public void Parse_NotEqual_ReturnsCorrectOperator()
    {
        var result = ODataFilterParser.Parse("type ne 'Article'");

        var node = Assert.IsType<ComparisonNode>(result);
        Assert.Equal(ComparisonOperator.NotEqual, node.Operator);
    }

    [Theory]
    [InlineData("gt", ComparisonOperator.GreaterThan)]
    [InlineData("ge", ComparisonOperator.GreaterThanOrEqual)]
    [InlineData("lt", ComparisonOperator.LessThan)]
    [InlineData("le", ComparisonOperator.LessThanOrEqual)]
    public void Parse_ComparisonOperators_ReturnCorrectOperator(string opStr, ComparisonOperator expected)
    {
        var result = ODataFilterParser.Parse($"published {opStr} '2025-01-01'");

        var node = Assert.IsType<ComparisonNode>(result);
        Assert.Equal(expected, node.Operator);
    }

    [Fact]
    public void Parse_AndExpression_ReturnsLogicalNode()
    {
        var result = ODataFilterParser.Parse("type eq 'Note' and isReply eq false");

        var node = Assert.IsType<LogicalNode>(result);
        Assert.Equal(LogicalOperator.And, node.Operator);
        Assert.IsType<ComparisonNode>(node.Left);
        Assert.IsType<ComparisonNode>(node.Right);
    }

    [Fact]
    public void Parse_OrExpression_ReturnsLogicalNode()
    {
        var result = ODataFilterParser.Parse("type eq 'Note' or type eq 'Article'");

        var node = Assert.IsType<LogicalNode>(result);
        Assert.Equal(LogicalOperator.Or, node.Operator);
    }

    [Fact]
    public void Parse_NotExpression_ReturnsNotNode()
    {
        var result = ODataFilterParser.Parse("not isReply eq true");

        var node = Assert.IsType<NotNode>(result);
        Assert.IsType<ComparisonNode>(node.Inner);
    }

    [Fact]
    public void Parse_ContainsFunction_ReturnsFunctionNode()
    {
        var result = ODataFilterParser.Parse("contains(content, 'hello')");

        var node = Assert.IsType<FunctionNode>(result);
        Assert.Equal("contains", node.FunctionName);
        Assert.Equal("content", node.Property);
        Assert.Equal("hello", node.Value);
    }

    [Fact]
    public void Parse_StartsWithFunction_ReturnsFunctionNode()
    {
        var result = ODataFilterParser.Parse("startswith(name, 'Al')");

        var node = Assert.IsType<FunctionNode>(result);
        Assert.Equal("startswith", node.FunctionName);
        Assert.Equal("name", node.Property);
        Assert.Equal("Al", node.Value);
    }

    [Fact]
    public void Parse_EndsWithFunction_ReturnsFunctionNode()
    {
        var result = ODataFilterParser.Parse("endswith(name, 'ice')");

        var node = Assert.IsType<FunctionNode>(result);
        Assert.Equal("endswith", node.FunctionName);
    }

    [Fact]
    public void Parse_Parentheses_OverridePrecedence()
    {
        var result = ODataFilterParser.Parse("(type eq 'Note' or type eq 'Article') and isReply eq false");

        var node = Assert.IsType<LogicalNode>(result);
        Assert.Equal(LogicalOperator.And, node.Operator);
        Assert.IsType<LogicalNode>(node.Left);
        Assert.IsType<ComparisonNode>(node.Right);

        var inner = Assert.IsType<LogicalNode>(node.Left);
        Assert.Equal(LogicalOperator.Or, inner.Operator);
    }

    [Fact]
    public void Parse_BooleanValue_ParsedCorrectly()
    {
        var result = ODataFilterParser.Parse("hasAttachment eq true");

        var node = Assert.IsType<ComparisonNode>(result);
        Assert.Equal(true, node.Value);
    }

    [Fact]
    public void Parse_NullValue_ParsedCorrectly()
    {
        var result = ODataFilterParser.Parse("inReplyTo eq null");

        var node = Assert.IsType<ComparisonNode>(result);
        Assert.Null(node.Value);
    }

    [Fact]
    public void Parse_NumericValue_ParsedCorrectly()
    {
        var result = ODataFilterParser.Parse("count gt 5");

        var node = Assert.IsType<ComparisonNode>(result);
        Assert.Equal(5.0, node.Value);
    }

    [Fact]
    public void Parse_EscapedSingleQuote_HandledCorrectly()
    {
        var result = ODataFilterParser.Parse("content eq 'it''s a test'");

        var node = Assert.IsType<ComparisonNode>(result);
        Assert.Equal("it's a test", node.Value);
    }

    [Fact]
    public void Parse_ComplexAndOrCombination_ParsedCorrectly()
    {
        var result = ODataFilterParser.Parse(
            "type eq 'Note' and contains(content, 'hello') or type eq 'Article'");

        // 'and' binds tighter than 'or', so: (type eq Note AND contains) OR (type eq Article)
        var node = Assert.IsType<LogicalNode>(result);
        Assert.Equal(LogicalOperator.Or, node.Operator);
        Assert.IsType<LogicalNode>(node.Left);
        Assert.IsType<ComparisonNode>(node.Right);
    }

    [Fact]
    public void Parse_InvalidExpression_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ODataFilterParser.Parse("type invalid 'Note'"));
    }

    [Fact]
    public void Parse_UnterminatedString_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ODataFilterParser.Parse("type eq 'unterminated"));
    }

    [Fact]
    public void Parse_EmptyParens_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ODataFilterParser.Parse("()"));
    }
}
