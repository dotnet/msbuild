// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    [Flags]
    internal enum ParserOptions
    {
        None = 0x0,
        AllowProperties = 0x1,
        AllowItemLists = 0x2,
        AllowPropertiesAndItemLists = AllowProperties | AllowItemLists,
        AllowBuiltInMetadata = 0x4,
        AllowCustomMetadata = 0x8,
        AllowItemMetadata = AllowBuiltInMetadata | AllowCustomMetadata,
        AllowPropertiesAndItemMetadata = AllowProperties | AllowItemMetadata,
        AllowPropertiesAndCustomMetadata = AllowProperties | AllowCustomMetadata,
        AllowAll = AllowProperties | AllowItemLists | AllowItemMetadata
    };

    /// <summary>
    /// Parses MSBuild condition expressions.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Parse"/> to obtain a <see cref="ParsedCondition"/> and evaluate it
    /// through <see cref="ConditionEvaluator.EvaluateParsedCondition"/> so private state is initialized and released.
    /// Report warnings from successful parsing separately with <see cref="LogWarnings"/>.
    /// </remarks>
    internal sealed class Parser
    {
        private Scanner _lexer;
        private ParserOptions _options;
        private ElementLocation _elementLocation;
        private bool _hasMixedAndOr;
        internal int errorPosition = 0; // useful for unit tests

        /// <summary>
        /// Number of evaluation-state slots assigned to expandable string nodes in this condition.
        /// </summary>
        internal int StringNodeStateCount { get; private set; }

        internal Parser()
        {
            // nothing to see here, move along.
        }

        /// <summary>
        /// Parses a condition and records compatibility warnings.
        /// </summary>
        /// <remarks>
        /// The <paramref name="loggingContext"/> is used only to report warnings before a parse failure.
        /// After successful parsing, call <see cref="LogWarnings"/> to report them.
        /// </remarks>
        internal ParsedCondition Parse(
            string expression,
            ParserOptions optionSettings,
            ElementLocation elementLocation,
            LoggingContext loggingContext = null)
        {
            // We currently have no support (and no scenarios) for disallowing property references
            // in Conditions.
            Assumed.True((optionSettings & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

            _options = optionSettings;
            _elementLocation = elementLocation;
            StringNodeStateCount = 0;
            _hasMixedAndOr = false;

            try
            {
                _lexer = new Scanner(expression, _options);
                if (!_lexer.Advance())
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, _lexer.GetErrorResource(), expression, errorPosition, _lexer.UnexpectedlyFound);
                }
                GenericExpressionNode node = Expr(expression);
                if (!_lexer.IsNext(Token.TokenType.EndOfInput))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }
                return new ParsedCondition(node, new NodeStateLayout(StringNodeStateCount));
            }
            catch (InvalidProjectFileException)
            {
                // Failed parses cannot be published, so their diagnostics belong to this caller.
                LogWarnings(expression, loggingContext);
                throw;
            }
        }

        /// <summary>
        /// Reports compatibility warnings recorded by the most recent parse.
        /// </summary>
        internal void LogWarnings(string expression, LoggingContext loggingContext)
        {
            if (_hasMixedAndOr && loggingContext is not null)
            {
                loggingContext.LoggingService.LogWarning(
                    loggingContext.BuildEventContext, DiagnosticSubcategory.None, _elementLocation,
                    "ConditionMaybeEvaluatedIncorrectly", expression);
            }
        }

        //
        // Top node of grammar
        //    See grammar for how the following methods relate to each
        //    other.
        //
        private GenericExpressionNode Expr(string expression)
        {
            // Track mixes like "A Or B And C" so we can warn users
            // to make the grouping explicit with parentheses.
            bool hasAnd = false;
            bool hasOr = false;
            GenericExpressionNode node = BooleanTerm(expression, ref hasAnd);
            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                node = ExprPrime(expression, node, ref hasAnd, ref hasOr);
            }

            _hasMixedAndOr |= hasAnd && hasOr;

            return node;
        }

        private GenericExpressionNode ExprPrime(string expression, GenericExpressionNode lhs, ref bool hasAnd, ref bool hasOr)
        {
            if (Same(expression, Token.TokenType.EndOfInput))
            {
                return lhs;
            }
            else if (Same(expression, Token.TokenType.Or))
            {
                hasOr = true;
                OperatorExpressionNode orNode = new OrExpressionNode();
                GenericExpressionNode rhs = BooleanTerm(expression, ref hasAnd);
                orNode.LeftChild = lhs;
                orNode.RightChild = rhs;
                return ExprPrime(expression, orNode, ref hasAnd, ref hasOr);
            }
            else
            {
                // I think this is ok.  ExprPrime always shows up at
                // the rightmost side of the grammar rhs, the EndOfInput case
                // takes care of things
                return lhs;
            }
        }

        private GenericExpressionNode BooleanTerm(string expression, ref bool hasAnd)
        {
            GenericExpressionNode node = RelationalExpr(expression);
            if (node == null)
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
            }

            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                node = BooleanTermPrime(expression, node, ref hasAnd);
            }
            return node;
        }

        private GenericExpressionNode BooleanTermPrime(string expression, GenericExpressionNode lhs, ref bool hasAnd)
        {
            if (_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                return lhs;
            }
            else if (Same(expression, Token.TokenType.And))
            {
                hasAnd = true;
                GenericExpressionNode rhs = RelationalExpr(expression);
                if (rhs == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }

                OperatorExpressionNode andNode = new AndExpressionNode();
                andNode.LeftChild = lhs;
                andNode.RightChild = rhs;
                return BooleanTermPrime(expression, andNode, ref hasAnd);
            }
            else
            {
                // Should this be error case?
                return lhs;
            }
        }

        private GenericExpressionNode RelationalExpr(string expression)
        {
            {
                GenericExpressionNode lhs = Factor(expression);
                if (lhs == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }

                OperatorExpressionNode node = RelationalOperation(expression);
                if (node == null)
                {
                    return lhs;
                }
                GenericExpressionNode rhs = Factor(expression);
                node.LeftChild = lhs;
                node.RightChild = rhs;
                return node;
            }
        }

        private OperatorExpressionNode RelationalOperation(string expression)
        {
            OperatorExpressionNode node = null;
            if (Same(expression, Token.TokenType.LessThan))
            {
                node = new LessThanExpressionNode();
            }
            else if (Same(expression, Token.TokenType.GreaterThan))
            {
                node = new GreaterThanExpressionNode();
            }
            else if (Same(expression, Token.TokenType.LessThanOrEqualTo))
            {
                node = new LessThanOrEqualExpressionNode();
            }
            else if (Same(expression, Token.TokenType.GreaterThanOrEqualTo))
            {
                node = new GreaterThanOrEqualExpressionNode();
            }
            else if (Same(expression, Token.TokenType.EqualTo))
            {
                node = new EqualExpressionNode();
            }
            else if (Same(expression, Token.TokenType.NotEqualTo))
            {
                node = new NotEqualExpressionNode();
            }
            return node;
        }

        private GenericExpressionNode Factor(string expression)
        {
            // Checks for TokenTypes String, Numeric, Property, ItemMetadata, and ItemList.
            GenericExpressionNode arg = this.Arg(expression);

            // If it's one of those, return it.
            if (arg != null)
            {
                return arg;
            }

            // If it's not one of those, check for other TokenTypes.
            Token current = _lexer.CurrentToken;
            if (Same(expression, Token.TokenType.Function))
            {
                if (!Same(expression, Token.TokenType.LeftParenthesis))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", _lexer.IsNextString(), errorPosition);
                    return null;
                }
                var arglist = new List<GenericExpressionNode>();
                Arglist(expression, arglist);
                if (!Same(expression, Token.TokenType.RightParenthesis))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                    return null;
                }
                return new FunctionCallExpressionNode(current.String, arglist);
            }
            else if (Same(expression, Token.TokenType.LeftParenthesis))
            {
                GenericExpressionNode child = Expr(expression);
                if (Same(expression, Token.TokenType.RightParenthesis))
                {
                    return child;
                }
                else
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }
            }
            else if (Same(expression, Token.TokenType.Not))
            {
                OperatorExpressionNode notNode = new NotExpressionNode();
                GenericExpressionNode expr = Factor(expression);
                if (expr == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }
                notNode.LeftChild = expr;
                return notNode;
            }
            else
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
            }
            return null;
        }

        private void Arglist(string expression, List<GenericExpressionNode> arglist)
        {
            if (!_lexer.IsNext(Token.TokenType.RightParenthesis))
            {
                Args(expression, arglist);
            }
        }

        private void Args(string expression, List<GenericExpressionNode> arglist)
        {
            GenericExpressionNode arg = Arg(expression);
            arglist.Add(arg);
            if (Same(expression, Token.TokenType.Comma))
            {
                Args(expression, arglist);
            }
        }

        private GenericExpressionNode Arg(string expression)
        {
            Token current = _lexer.CurrentToken;
            if (Same(expression, Token.TokenType.String))
            {
                return CreateStringNode(current.String, current.Expandable);
            }
            else if (Same(expression, Token.TokenType.Numeric))
            {
                return new NumericExpressionNode(current.String);
            }
            else if (Same(expression, Token.TokenType.Property))
            {
                return CreateStringNode(current.String, expandable: true);
            }
            else if (Same(expression, Token.TokenType.ItemMetadata))
            {
                return CreateStringNode(current.String, expandable: true);
            }
            else if (Same(expression, Token.TokenType.ItemList))
            {
                return CreateStringNode(current.String, expandable: true);
            }
            else
            {
                return null;
            }
        }

        // Shared trees assign state slots only to expandable nodes; literals need no state (-1).
        private StringExpressionNode CreateStringNode(string value, bool expandable) =>
            new StringExpressionNode(value, expandable, expandable ? StringNodeStateCount++ : -1);

        private bool Same(string expression, Token.TokenType token)
        {
            if (_lexer.IsNext(token))
            {
                if (!_lexer.Advance())
                {
                    errorPosition = _lexer.GetErrorPosition();
                    if (_lexer.UnexpectedlyFound != null)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, _lexer.GetErrorResource(), expression, errorPosition, _lexer.UnexpectedlyFound);
                    }
                    else
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, _lexer.GetErrorResource(), expression, errorPosition);
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
