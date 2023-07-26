// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Css.Parser.Parser;
using Microsoft.Css.Parser.Tokens;
using Microsoft.Css.Parser.TreeItems;
using Microsoft.Css.Parser.TreeItems.AtDirectives;
using Microsoft.Css.Parser.TreeItems.Selectors;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class RewriteCss : Task
    {
        // Public for testing.
        public const string ImportNotAllowedErrorMessage =
            "{0}({1},{2}): @import rules are not supported within scoped CSS files because the loading order would be undefined. " +
            "@import may only be placed in non-scoped CSS files.";
        private const string DeepCombinatorText = "::deep";

        private static readonly TimeSpan s_regexTimeout = TimeSpan.FromSeconds(1);
        private static readonly Regex s_deepCombinatorRegex = new($@"^{DeepCombinatorText}\s*", RegexOptions.None, s_regexTimeout);

        [Required]
        public ITaskItem[] FilesToTransform { get; set; }

        public bool SkipIfOutputIsNewer { get; set; } = true;

        public override bool Execute()
        {
            var allDiagnostics = new ConcurrentQueue<ErrorMessage>();

            System.Threading.Tasks.Parallel.For(0, FilesToTransform.Length, i =>
            {
                var input = FilesToTransform[i];
                var inputFile = input.GetMetadata("FullPath");
                var outputFile = input.GetMetadata("OutputFile");
                var cssScope = input.GetMetadata("CssScope");

                if (SkipIfOutputIsNewer && File.Exists(outputFile) && File.GetLastWriteTimeUtc(inputFile) < File.GetLastWriteTimeUtc(outputFile))
                {
                    Log.LogMessage(MessageImportance.Low, $"Skipping scope transformation for '{input.ItemSpec}' because '{outputFile}' is newer than '{input.ItemSpec}'.");
                    return;
                }

                // Create the directory for the output file in case it doesn't exist.
                // It's easier to do it here than on MSBuild.
                Directory.CreateDirectory(Path.GetDirectoryName(outputFile));

                var inputText = File.ReadAllText(inputFile);
                var sourceFile = new SourceFile(inputText);

                var rewrittenCss = AddScopeToSelectors(inputFile, inputText, cssScope, out var errors);
                if (errors.Any())
                {
                    foreach (var error in errors)
                    {
                        Log.LogError(error.Message, error.MessageArgs);
                    }
                }
                else
                {
                    File.WriteAllText(outputFile, rewrittenCss);
                }
            });

            return !Log.HasLoggedErrors;
        }

        // Public for testing.
        public static string AddScopeToSelectors(string filePath, string text, string cssScope, out IEnumerable<ErrorMessage> errors)
            => AddScopeToSelectors(filePath, new SourceFile(text), cssScope, out errors);

        private static string AddScopeToSelectors(string filePath, in SourceFile sourceFile, string cssScope, out IEnumerable<ErrorMessage> errors)
        {
            var cssParser = new DefaultParserFactory().CreateParser();
            var stylesheet = cssParser.Parse(sourceFile.Text, insertComments: false);

            var resultBuilder = new StringBuilder();
            var previousInsertionPosition = 0;
            var foundErrors = new List<ErrorMessage>();

            var ensureNoImportsVisitor = new EnsureNoImports(filePath, sourceFile, stylesheet, foundErrors);
            ensureNoImportsVisitor.Visit();

            var scopeInsertionPositionsVisitor = new FindScopeInsertionEdits(stylesheet);
            scopeInsertionPositionsVisitor.Visit();
            foreach (var edit in scopeInsertionPositionsVisitor.Edits)
            {
                resultBuilder.Append(sourceFile.Text.Substring(previousInsertionPosition, edit.Position - previousInsertionPosition));
                previousInsertionPosition = edit.Position;

                switch (edit)
                {
                    case InsertSelectorScopeEdit _:
                        resultBuilder.AppendFormat(CultureInfo.InvariantCulture, "[{0}]", cssScope);
                        break;
                    case InsertKeyframesNameScopeEdit _:
                        resultBuilder.AppendFormat(CultureInfo.InvariantCulture, "-{0}", cssScope);
                        break;
                    case DeleteContentEdit deleteContentEdit:
                        previousInsertionPosition += deleteContentEdit.DeleteLength;
                        break;
                    default:
                        throw new NotImplementedException($"Unknown edit type: '{edit}'");
                }
            }

            resultBuilder.Append(sourceFile.Text.Substring(previousInsertionPosition));

            errors = foundErrors;
            return resultBuilder.ToString();
        }

        private static bool TryFindKeyframesIdentifier(AtDirective atDirective, out ParseItem identifier)
        {
            var keyword = atDirective.Keyword;
            if (string.Equals(keyword?.Text, "keyframes", StringComparison.OrdinalIgnoreCase))
            {
                var nextSiblingText = keyword.NextSibling?.Text;
                if (!string.IsNullOrEmpty(nextSiblingText))
                {
                    identifier = keyword.NextSibling;
                    return true;
                }
            }

            identifier = null;
            return false;
        }

        private class FindScopeInsertionEdits : Visitor
        {
            public List<CssEdit> Edits { get; } = new List<CssEdit>();

            private readonly HashSet<string> _keyframeIdentifiers;

            public FindScopeInsertionEdits(ComplexItem root) : base(root)
            {
                // Before we start, we need to know the full set of keyframe names declared in this document
                var keyframesIdentifiersVisitor = new FindKeyframesIdentifiersVisitor(root);
                keyframesIdentifiersVisitor.Visit();
                _keyframeIdentifiers = keyframesIdentifiersVisitor.KeyframesIdentifiers
                    .Select(x => x.Text)
                    .ToHashSet(StringComparer.Ordinal); // Keyframe names are case-sensitive
            }

            protected override void VisitSelector(Selector selector)
            {
                // For a ruleset like ".first child, .second { ... }", we'll see two selectors:
                //   ".first child," containing two simple selectors: ".first" and "child"
                //   ".second", containing one simple selector: ".second"
                // Our goal is to insert immediately after the final simple selector within each selector

                // If there's a deep combinator among the sequence of simple selectors, we consider that to signal
                // the end of the set of simple selectors for us to look at, plus we strip it out
                var allSimpleSelectors = selector.Children.OfType<SimpleSelector>();
                var firstDeepCombinator = allSimpleSelectors.FirstOrDefault(s => s_deepCombinatorRegex.IsMatch(s.Text));

                var lastSimpleSelector = allSimpleSelectors.TakeWhile(s => s != firstDeepCombinator).LastOrDefault();
                if (lastSimpleSelector != null)
                {
                    Edits.Add(new InsertSelectorScopeEdit { Position = FindPositionToInsertInSelector(lastSimpleSelector) });
                }
                else if (firstDeepCombinator != null)
                {
                    // For a leading deep combinator, we want to insert the scope attribute at the start
                    // Otherwise the result would be a CSS rule that isn't scoped at all
                    Edits.Add(new InsertSelectorScopeEdit { Position = firstDeepCombinator.Start });
                }

                // Also remove the deep combinator if we matched one
                if (firstDeepCombinator != null)
                {
                    Edits.Add(new DeleteContentEdit { Position = firstDeepCombinator.Start, DeleteLength = DeepCombinatorText.Length });
                }
            }

            private int FindPositionToInsertInSelector(SimpleSelector lastSimpleSelector)
            {
                var children = lastSimpleSelector.Children;
                for (var i  = 0; i < children.Count; i++)
                {
                    switch (children[i])
                    {
                        // Selectors like "a > ::deep b" get parsed as [[a][>]][::deep][b], and we want to
                        // insert right after the "a". So if we're processing a SimpleSelector like [[a][>]],
                        // consider the ">" to signal the "insert before" position.
                        case TokenItem t when IsTrailingCombinator(t.TokenType):

                        // Similarly selectors like "a::before" get parsed as [[a][::before]], and we want to
                        // insert right after the "a".  So if we're processing a SimpleSelector like [[a][::before]],
                        // consider the pseudoelement to signal the "insert before" position.
                        case PseudoElementSelector:
                        case PseudoElementFunctionSelector:
                        case PseudoClassSelector s when IsSingleColonPseudoElement(s):
                            // Insert after the previous token if there is one, otherwise before the whole thing
                            return i > 0 ? children[i - 1].AfterEnd : lastSimpleSelector.Start;
                    }
                }

                // Since we didn't find any children that signal the insert-before position,
                // insert after the whole thing
                return lastSimpleSelector.AfterEnd;
            }

            private static bool IsSingleColonPseudoElement(PseudoClassSelector selector)
            {
                // See https://developer.mozilla.org/en-US/docs/Web/CSS/Pseudo-elements
                // Normally, pseudoelements require a double-colon prefix. However the following "original set"
                // of pseudoelements also support single-colon prefixes for back-compatibility with older versions
                // of the W3C spec. Our CSS parser sees them as pseudoselectors rather than pseudoelements, so
                // we have to special-case them. The single-colon option doesn't exist for other more modern
                // pseudoelements.
                var selectorText = selector.Text;
                return string.Equals(selectorText, ":after", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(selectorText, ":before", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(selectorText, ":first-letter", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(selectorText, ":first-line", StringComparison.OrdinalIgnoreCase);
            }

            private static bool IsTrailingCombinator(CssTokenType tokenType)
            {
                switch (tokenType)
                {
                    case CssTokenType.Plus:
                    case CssTokenType.Tilde:
                    case CssTokenType.Greater:
                        return true;
                    default:
                        return false;
                }
            }

            protected override void VisitAtDirective(AtDirective item)
            {
                // Whenever we see "@keyframes something { ... }", we want to insert right after "something"
                if (TryFindKeyframesIdentifier(item, out var identifier))
                {
                    Edits.Add(new InsertKeyframesNameScopeEdit { Position = identifier.AfterEnd });
                }
                else
                {
                    VisitDefault(item);
                }
            }

            protected override void VisitDeclaration(Declaration item)
            {
                switch (item.PropertyNameText)
                {
                    case "animation":
                    case "animation-name":
                        // The first two tokens are <propertyname> and <colon> (otherwise we wouldn't be here).
                        // After that, any of the subsequent tokens might be the animation name.
                        // Unfortunately the rules for determining which token is the animation name are very
                        // complex - https://developer.mozilla.org/en-US/docs/Web/CSS/animation#Syntax
                        // Fortunately we only want to rewrite animation names that are explicitly declared in
                        // the same document (we don't want to add scopes to references to global keyframes)
                        // so it's sufficient just to match known animation names.
                        var animationNameTokens = item.Children.Skip(2).OfType<TokenItem>()
                            .Where(x => x.TokenType == CssTokenType.Identifier && _keyframeIdentifiers.Contains(x.Text));
                        foreach (var token in animationNameTokens)
                        {
                            Edits.Add(new InsertKeyframesNameScopeEdit { Position = token.AfterEnd });
                        }
                        break;
                    default:
                        // We don't need to do anything else with other declaration types
                        break;
                }
            }
        }

        private class FindKeyframesIdentifiersVisitor : Visitor
        {
            public FindKeyframesIdentifiersVisitor(ComplexItem root) : base(root)
            {
            }

            public List<ParseItem> KeyframesIdentifiers { get; } = new List<ParseItem>();

            protected override void VisitAtDirective(AtDirective item)
            {
                if (TryFindKeyframesIdentifier(item, out var identifier))
                {
                    KeyframesIdentifiers.Add(identifier);
                }
                else
                {
                    VisitDefault(item);
                }
            }
        }

        private class EnsureNoImports : Visitor
        {
            private readonly string _filePath;
            private readonly SourceFile _sourceFile;
            private readonly List<ErrorMessage> _diagnostics;

            public EnsureNoImports(string filePath, in SourceFile sourceFile, ComplexItem root, List<ErrorMessage> diagnostics) : base(root)
            {
                _filePath = filePath;
                _sourceFile = sourceFile;
                _diagnostics = diagnostics;
            }

            protected override void VisitAtDirective(AtDirective item)
            {
                if (item.Children.Count >= 2
                    && item.Children[0] is TokenItem firstChild
                    && firstChild.TokenType == CssTokenType.At
                    && item.Children[1] is TokenItem secondChild
                    && string.Equals(secondChild.Text, "import", StringComparison.OrdinalIgnoreCase))
                {
                    var location = _sourceFile.GetLocation(item.Start);
                    _diagnostics.Add(new(ImportNotAllowedErrorMessage, _filePath, location.Line, location.Character));
                }

                base.VisitAtDirective(item);
            }
        }

        private class Visitor
        {
            private readonly ComplexItem _root;

            public Visitor(ComplexItem root)
            {
                _root = root ?? throw new ArgumentNullException(nameof(root));
            }

            public void Visit()
            {
                VisitDefault(_root);
            }

            protected virtual void VisitSelector(Selector item)
            {
                VisitDefault(item);
            }

            protected virtual void VisitAtDirective(AtDirective item)
            {
                VisitDefault(item);
            }

            protected virtual void VisitDeclaration(Declaration item)
            {
                VisitDefault(item);
            }

            protected virtual void VisitDefault(ParseItem item)
            {
                if (item is ComplexItem complexItem)
                {
                    VisitDescendants(complexItem);
                }
            }

            private void VisitDescendants(ComplexItem container)
            {
                foreach (var child in container.Children)
                {
                    switch (child)
                    {
                        case Selector selector:
                            VisitSelector(selector);
                            break;
                        case AtDirective atDirective:
                            VisitAtDirective(atDirective);
                            break;
                        case Declaration declaration:
                            VisitDeclaration(declaration);
                            break;
                        default:
                            VisitDefault(child);
                            break;
                    }
                }
            }
        }

        private abstract class CssEdit
        {
            public int Position { get; set; }
        }

        private class InsertSelectorScopeEdit : CssEdit
        {
        }

        private class InsertKeyframesNameScopeEdit : CssEdit
        {
        }

        private class DeleteContentEdit : CssEdit
        {
            public int DeleteLength { get; set; }
        }

        private class SourceFile
        {
            private List<int> _lineStartIndices;

            public string Text { get; }

            public SourceFile(string text)
            {
                Text = text;
            }

            public SourceLocation GetLocation(int charIndex)
            {
                if (charIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(charIndex), charIndex, message: null);
                }

                _lineStartIndices ??= GetLineStartIndices(Text);

                var index = _lineStartIndices.BinarySearch(charIndex);
                var line = index < 0 ? -index - 1 : index + 1;
                var lastLineStart = _lineStartIndices[line - 1];
                var character = charIndex - lastLineStart + 1;
                return new(line, character);
            }

            private static List<int> GetLineStartIndices(string text)
            {
                var result = new List<int>() { 0 };
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\n')
                    {
                        result.Add(i + 1);
                    }
                }
                return result;
            }
        }

        private readonly struct SourceLocation
        {
            public int Line { get; }
            public int Character { get; }

            public SourceLocation(int line, int character)
            {
                Line = line;
                Character = character;
            }
        }

        // Public for testing.
        public readonly struct ErrorMessage
        {
            public string Message { get; }

            public object[] MessageArgs { get; }

            public ErrorMessage(string message, params object[] messageArgs)
            {
                Message = message;
                MessageArgs = messageArgs;
            }

            public override string ToString() => string.Format(Message, MessageArgs);
        }
    }
}
