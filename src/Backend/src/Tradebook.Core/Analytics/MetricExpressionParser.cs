using System.Globalization;

namespace Tradebook.Core.Analytics;

internal static class MetricExpressionParser
{
    public static string Rewrite(string expression, Func<string, string> rewriteMeasure)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new FormatException("Metric expression is empty.");
        }

        var parser = new Parser(expression, rewriteMeasure);
        var result = parser.ParseExpression();
        parser.SkipWhitespace();
        if (!parser.AtEnd)
        {
            throw new FormatException($"Unexpected metric token at position {parser.Position}.");
        }

        return result;
    }

    private sealed class Parser(string source, Func<string, string> rewriteMeasure)
    {
        public int Position { get; private set; }
        public bool AtEnd => Position == source.Length;

        public string ParseExpression()
        {
            var left = ParseTerm();
            while (TryRead('+') || TryRead('-'))
            {
                var operation = source[Position - 1];
                var right = ParseTerm();
                left = $"({left} {operation} {right})";
            }

            return left;
        }

        public void SkipWhitespace()
        {
            while (!AtEnd && char.IsWhiteSpace(source[Position]))
            {
                Position++;
            }
        }

        private string ParseTerm()
        {
            var left = ParseUnary();
            while (TryRead('*') || TryRead('/'))
            {
                var operation = source[Position - 1];
                var right = ParseUnary();
                left = $"({left} {operation} {right})";
            }

            return left;
        }

        private string ParseUnary()
        {
            if (TryRead('+'))
            {
                return $"(+{ParseUnary()})";
            }

            if (TryRead('-'))
            {
                return $"(-{ParseUnary()})";
            }

            return ParsePrimary();
        }

        private string ParsePrimary()
        {
            SkipWhitespace();
            if (TryRead('('))
            {
                var expression = ParseExpression();
                Require(')');
                return $"({expression})";
            }

            if (!AtEnd && char.IsDigit(source[Position]))
            {
                return ReadNumber();
            }

            if (!AtEnd && (char.IsLetter(source[Position]) || source[Position] == '_'))
            {
                var identifier = ReadIdentifier();
                if (!identifier.Equals("NULLIF", StringComparison.OrdinalIgnoreCase))
                {
                    return rewriteMeasure(identifier);
                }

                Require('(');
                var value = ParseExpression();
                Require(',');
                var fallback = ParseExpression();
                Require(')');
                return $"NULLIF({value}, {fallback})";
            }

            throw new FormatException($"Expected a metric operand at position {Position}.");
        }

        private string ReadNumber()
        {
            var start = Position;
            while (!AtEnd && char.IsDigit(source[Position]))
            {
                Position++;
            }

            if (!AtEnd && source[Position] == '.')
            {
                Position++;
                var fractionalStart = Position;
                while (!AtEnd && char.IsDigit(source[Position]))
                {
                    Position++;
                }

                if (fractionalStart == Position)
                {
                    throw new FormatException($"Invalid numeric literal at position {start}.");
                }
            }

            var token = source[start..Position];
            if (
                !decimal.TryParse(
                    token,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out _
                )
            )
            {
                throw new FormatException($"Invalid numeric literal '{token}'.");
            }

            return token;
        }

        private string ReadIdentifier()
        {
            var start = Position++;
            while (!AtEnd && (char.IsLetterOrDigit(source[Position]) || source[Position] == '_'))
            {
                Position++;
            }

            return source[start..Position];
        }

        private bool TryRead(char expected)
        {
            SkipWhitespace();
            if (AtEnd || source[Position] != expected)
            {
                return false;
            }

            Position++;
            return true;
        }

        private void Require(char expected)
        {
            if (!TryRead(expected))
            {
                throw new FormatException($"Expected '{expected}' at position {Position}.");
            }
        }
    }
}
