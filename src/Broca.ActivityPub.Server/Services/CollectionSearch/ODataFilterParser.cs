using System.Globalization;

namespace Broca.ActivityPub.Server.Services.CollectionSearch;

public static class ODataFilterParser
{
    public static FilterNode Parse(string filter)
    {
        var tokens = Tokenize(filter);
        var position = 0;
        var result = ParseOrExpression(tokens, ref position);

        if (position < tokens.Count)
            throw new FormatException($"Unexpected token '{tokens[position].Value}' at position {tokens[position].Position}");

        return result;
    }

    private static FilterNode ParseOrExpression(List<Token> tokens, ref int position)
    {
        var left = ParseAndExpression(tokens, ref position);

        while (position < tokens.Count && IsKeyword(tokens[position], "or"))
        {
            position++;
            var right = ParseAndExpression(tokens, ref position);
            left = new LogicalNode(left, LogicalOperator.Or, right);
        }

        return left;
    }

    private static FilterNode ParseAndExpression(List<Token> tokens, ref int position)
    {
        var left = ParseUnary(tokens, ref position);

        while (position < tokens.Count && IsKeyword(tokens[position], "and"))
        {
            position++;
            var right = ParseUnary(tokens, ref position);
            left = new LogicalNode(left, LogicalOperator.And, right);
        }

        return left;
    }

    private static FilterNode ParseUnary(List<Token> tokens, ref int position)
    {
        if (position < tokens.Count && IsKeyword(tokens[position], "not"))
        {
            position++;
            var inner = ParsePrimary(tokens, ref position);
            return new NotNode(inner);
        }

        return ParsePrimary(tokens, ref position);
    }

    private static FilterNode ParsePrimary(List<Token> tokens, ref int position)
    {
        if (position >= tokens.Count)
            throw new FormatException("Unexpected end of filter expression");

        // Parenthesized expression
        if (tokens[position].Type == TokenType.LeftParen)
        {
            position++;
            var node = ParseOrExpression(tokens, ref position);
            ExpectToken(tokens, ref position, TokenType.RightParen, ")");
            return node;
        }

        // Function call: contains(property, 'value')
        if (tokens[position].Type == TokenType.Identifier && IsFunction(tokens[position].Value))
        {
            return ParseFunction(tokens, ref position);
        }

        // Comparison: property op value
        return ParseComparison(tokens, ref position);
    }

    private static FilterNode ParseFunction(List<Token> tokens, ref int position)
    {
        var functionName = tokens[position].Value.ToLowerInvariant();
        position++;

        ExpectToken(tokens, ref position, TokenType.LeftParen, "(");

        if (position >= tokens.Count || tokens[position].Type != TokenType.Identifier)
            throw new FormatException($"Expected property name in {functionName}()");

        var property = tokens[position].Value;
        position++;

        ExpectToken(tokens, ref position, TokenType.Comma, ",");

        if (position >= tokens.Count)
            throw new FormatException($"Expected value in {functionName}()");

        var value = ParseLiteralValue(tokens, ref position)?.ToString()
                    ?? throw new FormatException($"Expected string value in {functionName}()");

        ExpectToken(tokens, ref position, TokenType.RightParen, ")");

        return new FunctionNode(functionName, property, value);
    }

    private static FilterNode ParseComparison(List<Token> tokens, ref int position)
    {
        if (position >= tokens.Count || tokens[position].Type != TokenType.Identifier)
            throw new FormatException("Expected property name");

        var property = tokens[position].Value;
        position++;

        if (position >= tokens.Count || tokens[position].Type != TokenType.Identifier)
            throw new FormatException($"Expected comparison operator after '{property}'");

        var op = ParseComparisonOperator(tokens[position].Value)
                 ?? throw new FormatException($"Unknown operator '{tokens[position].Value}'");
        position++;

        var value = ParseLiteralValue(tokens, ref position);

        return new ComparisonNode(property, op, value);
    }

    private static object? ParseLiteralValue(List<Token> tokens, ref int position)
    {
        if (position >= tokens.Count)
            throw new FormatException("Expected value");

        var token = tokens[position];
        position++;

        return token.Type switch
        {
            TokenType.StringLiteral => token.Value,
            TokenType.Number => double.Parse(token.Value, CultureInfo.InvariantCulture),
            TokenType.Identifier when token.Value.Equals("true", StringComparison.OrdinalIgnoreCase) => true,
            TokenType.Identifier when token.Value.Equals("false", StringComparison.OrdinalIgnoreCase) => false,
            TokenType.Identifier when token.Value.Equals("null", StringComparison.OrdinalIgnoreCase) => null,
            TokenType.Identifier when DateTimeOffset.TryParse(token.Value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt) => dt,
            TokenType.Identifier => token.Value,
            _ => throw new FormatException($"Unexpected token type {token.Type} for value")
        };
    }

    private static ComparisonOperator? ParseComparisonOperator(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "eq" => ComparisonOperator.Equal,
            "ne" => ComparisonOperator.NotEqual,
            "gt" => ComparisonOperator.GreaterThan,
            "ge" => ComparisonOperator.GreaterThanOrEqual,
            "lt" => ComparisonOperator.LessThan,
            "le" => ComparisonOperator.LessThanOrEqual,
            _ => null
        };
    }

    private static bool IsKeyword(Token token, string keyword) =>
        token.Type == TokenType.Identifier &&
        token.Value.Equals(keyword, StringComparison.OrdinalIgnoreCase);

    private static bool IsFunction(string name) =>
        name.Equals("contains", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("startswith", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("endswith", StringComparison.OrdinalIgnoreCase);

    private static void ExpectToken(List<Token> tokens, ref int position, TokenType type, string expected)
    {
        if (position >= tokens.Count || tokens[position].Type != type)
        {
            var actual = position < tokens.Count ? tokens[position].Value : "end of expression";
            throw new FormatException($"Expected '{expected}' but found '{actual}'");
        }
        position++;
    }

    private static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < input.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(input[i]))
            {
                i++;
                continue;
            }

            var start = i;

            switch (input[i])
            {
                case '(':
                    tokens.Add(new Token(TokenType.LeftParen, "(", start));
                    i++;
                    break;

                case ')':
                    tokens.Add(new Token(TokenType.RightParen, ")", start));
                    i++;
                    break;

                case ',':
                    tokens.Add(new Token(TokenType.Comma, ",", start));
                    i++;
                    break;

                case '\'':
                    i++;
                    var strStart = i;
                    while (i < input.Length)
                    {
                        if (input[i] == '\'')
                        {
                            // Check for escaped single quote ('')
                            if (i + 1 < input.Length && input[i + 1] == '\'')
                            {
                                i += 2;
                                continue;
                            }
                            break;
                        }
                        i++;
                    }
                    if (i >= input.Length)
                        throw new FormatException("Unterminated string literal");
                    var strValue = input[strStart..i].Replace("''", "'");
                    tokens.Add(new Token(TokenType.StringLiteral, strValue, start));
                    i++;
                    break;

                default:
                    if (char.IsDigit(input[i]) || (input[i] == '-' && i + 1 < input.Length && char.IsDigit(input[i + 1])))
                    {
                        while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.' || input[i] == '-'))
                            i++;
                        tokens.Add(new Token(TokenType.Number, input[start..i], start));
                    }
                    else if (char.IsLetter(input[i]) || input[i] == '_')
                    {
                        while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_' || input[i] == '.' || input[i] == ':' || input[i] == '-' || input[i] == '+'))
                            i++;
                        tokens.Add(new Token(TokenType.Identifier, input[start..i], start));
                    }
                    else
                    {
                        throw new FormatException($"Unexpected character '{input[i]}' at position {i}");
                    }
                    break;
            }
        }

        return tokens;
    }

    private record Token(TokenType Type, string Value, int Position);

    private enum TokenType
    {
        Identifier,
        StringLiteral,
        Number,
        LeftParen,
        RightParen,
        Comma
    }
}
