// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;

namespace Microsoft.Build.Evaluation
{
    using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;

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
    /// This class implements the grammar for complex conditionals.
    ///
    /// The usage is:
    ///    Parser p = new Parser(CultureInfo);
    ///    ExpressionTree t = p.Parse(expression, XmlNode);
    ///
    /// The expression tree can then be evaluated and re-evaluated as needed.
    /// </summary>
    /// <remarks>
    /// UNDONE: When we copied over the conditionals code, we didn't copy over the unit tests for scanner, parser, and expression tree.
    /// </remarks>
    internal sealed class Parser
    {
        private Scanner _lexer;
        private ParserOptions _options;
        private ElementLocation _elementLocation;
        internal int errorPosition = 0; // useful for unit tests

        #region REMOVE_COMPAT_WARNING

        private bool _warnedForExpression = false;

        private BuildEventContext _logBuildEventContext;
        /// <summary>
        ///  Location contextual information which are attached to logging events to 
        ///  say where they are in relation to the process, engine, project, target,task which is executing
        /// </summary>
        internal BuildEventContext LogBuildEventContext
        {
            get
            {
                return _logBuildEventContext;
            }
            set
            {
                _logBuildEventContext = value;
            }
        }
        private ILoggingService _loggingServices;
        /// <summary>
        /// Engine Logging Service reference where events will be logged to
        /// </summary>
        internal ILoggingService LoggingServices
        {
            set
            {
                _loggingServices = value;
            }

            get
            {
                return _loggingServices;
            }
        }
        #endregion

        internal Parser()
        {
            // nothing to see here, move along.
        }

        //
        // Main entry point for parser.
        // You pass in the expression you want to parse, and you get an
        // ExpressionTree out the back end.
        //
        internal GenericExpressionNode Parse(string expression, ParserOptions optionSettings, ElementLocation elementLocation)
        {
            // We currently have no support (and no scenarios) for disallowing property references
            // in Conditions.
            ErrorUtilities.VerifyThrow(0 != (optionSettings & ParserOptions.AllowProperties),
                "Properties should always be allowed.");

            _options = optionSettings;
            _elementLocation = elementLocation;

            _lexer = new Scanner(expression, _options);
            if (!_lexer.Advance())
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, elementLocation, _lexer.GetErrorResource(), expression, errorPosition, _lexer.UnexpectedlyFound);
            }
            GenericExpressionNode node = Expr(expression);
            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
            }
            return node;
        }

        //
        // Top node of grammar
        //    See grammar for how the following methods relate to each
        //    other.
        //
        private GenericExpressionNode Expr(string expression)
        {
            GenericExpressionNode node = BooleanTerm(expression);
            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                node = ExprPrime(expression, node);
            }

            #region REMOVE_COMPAT_WARNING
            // Check for potential change in behavior
            if (LoggingServices != null && !_warnedForExpression &&
                node.PotentialAndOrConflict())
            {
                // We only want to warn once even if there multiple () sub expressions
                _warnedForExpression = true;

                // Log a warning regarding the fact the expression may have been evaluated
                // incorrectly in earlier version of MSBuild
                LoggingServices.LogWarning(_logBuildEventContext, null, new BuildEventFileInfo(_elementLocation), "ConditionMaybeEvaluatedIncorrectly", expression);
            }
            #endregion

            return node;
        }

        private GenericExpressionNode ExprPrime(string expression, GenericExpressionNode lhs)
        {
            if (Same(expression, Token.TokenType.EndOfInput))
            {
                return lhs;
            }
            else if (Same(expression, Token.TokenType.Or))
            {
                OperatorExpressionNode orNode = new OrExpressionNode();
                GenericExpressionNode rhs = BooleanTerm(expression);
                orNode.LeftChild = lhs;
                orNode.RightChild = rhs;
                return ExprPrime(expression, orNode);
            }
            else
            {
                // I think this is ok.  ExprPrime always shows up at
                // the rightmost side of the grammar rhs, the EndOfInput case
                // takes care of things
                return lhs;
            }
        }

        private GenericExpressionNode BooleanTerm(string expression)
        {
            GenericExpressionNode node = RelationalExpr(expression);
            if (node == null)
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, _elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
            }

            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                node = BooleanTermPrime(expression, node);
            }
            return node;
        }

        private GenericExpressionNode BooleanTermPrime(string expression, GenericExpressionNode lhs)
        {
            if (_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                return lhs;
            }
            else if (Same(expression, Token.TokenType.And))
            {
                GenericExpressionNode rhs = RelationalExpr(expression);
                if (rhs == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, _elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }

                OperatorExpressionNode andNode = new AndExpressionNode();
                andNode.LeftChild = lhs;
                andNode.RightChild = rhs;
                return BooleanTermPrime(expression, andNode);
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
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, _elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
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
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, _elementLocation, "UnexpectedTokenInCondition", _lexer.IsNextString(), errorPosition);
                    return null;
                }
                var arglist = new List<GenericExpressionNode>();
                Arglist(expression, arglist);
                if (!Same(expression, Token.TokenType.RightParenthesis))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, _elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                    return null;
                }
                return new FunctionCallExpressionNode(current.String, arglist);
            }
            else if (Same(expression, Token.TokenType.LeftParenthesis))
            {
                GenericExpressionNode child = Expr(expression);
                if (Same(expression, Token.TokenType.RightParenthesis))
                    return child;
                else
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, _elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }
            }
            else if (Same(expression, Token.TokenType.Not))
            {
                OperatorExpressionNode notNode = new NotExpressionNode();
                GenericExpressionNode expr = Factor(expression);
                if (expr == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, _elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }
                notNode.LeftChild = expr;
                return notNode;
            }
            else
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, _elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
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
                return new StringExpressionNode(current.String, current.Expandable);
            }
            else if (Same(expression, Token.TokenType.Numeric))
            {
                return new NumericExpressionNode(current.String);
            }
            else if (Same(expression, Token.TokenType.Property))
            {
                return new StringExpressionNode(current.String, true /* requires expansion */);
            }
            else if (Same(expression, Token.TokenType.ItemMetadata))
            {
                return new StringExpressionNode(current.String, true /* requires expansion */);
            }
            else if (Same(expression, Token.TokenType.ItemList))
            {
                return new StringExpressionNode(current.String, true /* requires expansion */);
            }
            else
            {
                return null;
            }
        }

        private bool Same(string expression, Token.TokenType token)
        {
            if (_lexer.IsNext(token))
            {
                if (!_lexer.Advance())
                {
                    errorPosition = _lexer.GetErrorPosition();
                    if (_lexer.UnexpectedlyFound != null)
                    {
                        ProjectErrorUtilities.VerifyThrowInvalidProject(false, _elementLocation, _lexer.GetErrorResource(), expression, errorPosition, _lexer.UnexpectedlyFound);
                    }
                    else
                    {
                        ProjectErrorUtilities.VerifyThrowInvalidProject(false, _elementLocation, _lexer.GetErrorResource(), expression, errorPosition);
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
