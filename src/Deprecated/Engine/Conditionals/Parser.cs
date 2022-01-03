// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Xml;
using System;
using Microsoft.Build.BuildEngine.Shared;

using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildEngine
{
    [Flags]
    internal enum ParserOptions
    {
        None                = 0x0,
        AllowProperties     = 0x1,
        AllowItemLists      = 0x2,
        AllowPropertiesAndItemLists = AllowProperties | AllowItemLists,
        AllowItemMetadata   = 0x4,
        AllowPropertiesAndItemMetadata = AllowProperties | AllowItemMetadata,
        AllowAll            = AllowProperties | AllowItemLists | AllowItemMetadata
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
    internal sealed class Parser
    {
        private Scanner lexer;
        private XmlAttribute conditionAttribute;
        private ParserOptions options;
        internal int errorPosition = 0; // useful for unit tests

#region REMOVE_COMPAT_WARNING

        private bool warnedForExpression = false;
        
        private BuildEventContext logBuildEventContext;
        /// <summary>
        ///  Location contextual information which are attached to logging events to 
        ///  say where they are in relation to the process, engine, project, target,task which is executing
        /// </summary>
        internal BuildEventContext LogBuildEventContext
        {
            get
            {
                return logBuildEventContext;
            }
            set
            {
                logBuildEventContext = value;
            }
        }
        private EngineLoggingServices loggingServices;
        /// <summary>
        /// Engine Logging Service reference where events will be logged to
        /// </summary>
        internal EngineLoggingServices LoggingServices
        {
            set
            {
                this.loggingServices = value;
            }
            
            get
            {
                return this.loggingServices;
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
        internal GenericExpressionNode Parse(string expression, XmlAttribute conditionAttributeRef, ParserOptions optionSettings)
        {
            // We currently have no support (and no scenarios) for disallowing property references
            // in Conditions.
            ErrorUtilities.VerifyThrow(0 != (optionSettings & ParserOptions.AllowProperties),
                "Properties should always be allowed.");

            this.conditionAttribute = conditionAttributeRef;
            this.options = optionSettings;

            lexer = new Scanner(expression, options);
            if (!lexer.Advance())
            {
                errorPosition = lexer.GetErrorPosition();
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, lexer.GetErrorResource(), expression, errorPosition, lexer.UnexpectedlyFound);
            }
            GenericExpressionNode node = Expr(expression);
            if (!lexer.IsNext(Token.TokenType.EndOfInput))
            {
                errorPosition = lexer.GetErrorPosition();
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, "UnexpectedTokenInCondition", expression, lexer.IsNextString(), errorPosition);
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
            if (!lexer.IsNext(Token.TokenType.EndOfInput))
            {
                node = ExprPrime(expression, node);
            }

            #region REMOVE_COMPAT_WARNING
            // Check for potential change in behavior
            if (LoggingServices != null && !warnedForExpression &&
                node.PotentialAndOrConflict())
            {
                // We only want to warn once even if there multiple () sub expressions
                warnedForExpression = true;
                // Try to figure out where this expression was located
                string projectFile = String.Empty;
                int lineNumber   = 0;
                int columnNumber = 0;
                if (this.conditionAttribute != null)
                {
                    projectFile = XmlUtilities.GetXmlNodeFile(this.conditionAttribute, String.Empty /* no project file if XML is purely in-memory */);
                    XmlSearcher.GetLineColumnByNode(this.conditionAttribute, out lineNumber, out columnNumber);
                }
                // Log a warning regarding the fact the expression may have been evaluated
                // incorrectly in earlier version of MSBuild
                LoggingServices.LogWarning(logBuildEventContext,new BuildEventFileInfo(projectFile, lineNumber, columnNumber), "ConditionMaybeEvaluatedIncorrectly", expression);
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
                return ExprPrime( expression, orNode );
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
                errorPosition = lexer.GetErrorPosition();
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, "UnexpectedTokenInCondition", expression, lexer.IsNextString(), errorPosition);
            }

            if (!lexer.IsNext(Token.TokenType.EndOfInput))
            {
                node = BooleanTermPrime(expression, node);
            }
            return node;
        }

        private GenericExpressionNode BooleanTermPrime(string expression, GenericExpressionNode lhs)
        {
            if (lexer.IsNext(Token.TokenType.EndOfInput))
            {
                return lhs;
            }
            else if (Same(expression, Token.TokenType.And))
            {
                GenericExpressionNode rhs = RelationalExpr(expression);
                if (rhs == null)
                {
                    errorPosition = lexer.GetErrorPosition();
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, "UnexpectedTokenInCondition", expression, lexer.IsNextString(), errorPosition);
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
                    errorPosition = lexer.GetErrorPosition();
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, "UnexpectedTokenInCondition", expression, lexer.IsNextString(), errorPosition);
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
            Token current = lexer.CurrentToken;
            if (Same(expression, Token.TokenType.Function))
            {
                if (!Same(expression, Token.TokenType.LeftParenthesis))
                {
                    errorPosition = lexer.GetErrorPosition();
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, "UnexpectedTokenInCondition", lexer.IsNextString(), errorPosition);
                    return null;
                }
                ArrayList arglist = new ArrayList();
                Arglist(expression, arglist);
                if (!Same(expression, Token.TokenType.RightParenthesis))
                {
                    errorPosition = lexer.GetErrorPosition();
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, "UnexpectedTokenInCondition", expression, lexer.IsNextString(), errorPosition);
                    return null;
                }
                return new FunctionCallExpressionNode( current.String, arglist);
            }
            else if (Same(expression, Token.TokenType.LeftParenthesis))
            {
                GenericExpressionNode child = Expr(expression);
                if (Same(expression, Token.TokenType.RightParenthesis))
                    return child;
                else
                {
                    errorPosition = lexer.GetErrorPosition();
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, "UnexpectedTokenInCondition", expression, lexer.IsNextString(), errorPosition);
                }
            }
            else if (Same(expression, Token.TokenType.Not))
            {
                OperatorExpressionNode notNode = new NotExpressionNode();
                GenericExpressionNode expr = Factor(expression);
                if (expr == null)
                {
                    errorPosition = lexer.GetErrorPosition();
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, "UnexpectedTokenInCondition", expression, lexer.IsNextString(), errorPosition);
                }
                notNode.LeftChild = expr;
                return notNode;
            }
            else
            {
                errorPosition = lexer.GetErrorPosition();
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, "UnexpectedTokenInCondition", expression, lexer.IsNextString(), errorPosition);
            }
            return null;
        }

        private void Arglist(string expression, ArrayList arglist)
        {
            if (!lexer.IsNext(Token.TokenType.RightParenthesis))
                Args(expression, arglist);
        }

        private void Args(string expression, ArrayList arglist)
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
            Token current = lexer.CurrentToken;
            if (Same(expression, Token.TokenType.String))
            {
                return new StringExpressionNode(current.String);
            }
            else if (Same(expression, Token.TokenType.Numeric))
            {
                return new NumericExpressionNode(current.String);
            }
            else if (Same(expression, Token.TokenType.Property))
            {
                return new StringExpressionNode(current.String);
            }
            else if (Same(expression, Token.TokenType.ItemMetadata))
            {
                return new StringExpressionNode(current.String);
            }
            else if (Same(expression, Token.TokenType.ItemList))
            {
                return new StringExpressionNode(current.String);
            }
            else
            {
                return null;
            }
        }

        private bool Same(string expression, Token.TokenType token)
        {
            if (lexer.IsNext(token))
            {
                if (!lexer.Advance())
                {
                    errorPosition = lexer.GetErrorPosition();
                    if (lexer.UnexpectedlyFound != null)
                    {
                        ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, lexer.GetErrorResource(), expression, errorPosition, lexer.UnexpectedlyFound);
                    }
                    else
                    {
                        ProjectErrorUtilities.VerifyThrowInvalidProject(false, this.conditionAttribute, lexer.GetErrorResource(), expression, errorPosition);
                    }
                }
                return true;
            }
            else
                return false;
        }
    }
}
