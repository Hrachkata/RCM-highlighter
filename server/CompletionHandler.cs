using Acornima;
using Acornima.Ast;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RcmServer
{
    public class CompletionHandler : IJsonRpcRequestHandler<CompletionParams, CompletionList>
    {
        private Cache _cache;

        public CompletionHandler(Cache cache)
        {
            _cache = cache;
        }

        // TODO Do you want some shitty microoptimisation with caching the AST, updating the cache only on actual change between the script elements? Same for the XML?
        // TODO Do you want some shitty microoptimisation with async handling of the ast and creation of the completion array?
        // The answer is yes on a second thought, why not :^)
        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            // JS Script territory
            if (request.Position.Line > _cache.ScriptLine)
            {
                var jsModuleDic = _cache.moduleJSCache;
                
                if (jsModuleDic.Count == 0)
                {
                    return null;
                }
                
                var parserConfig = new ParserOptions
                {
                    EcmaVersion = EcmaVersion.ES6,
                    Tolerant = true
                };

                // Acornima parser
                var parser = new Parser( parserConfig );
                var result = new HashSet<CompletionItem>();

                foreach ((string js, int startLine) tuple in jsModuleDic.Values)
                {
                    try
                    {
                        var ast = parser.ParseScript(tuple.js);
                        var autocompleteInfo = JsAstAutocompleteExtractor.Extract(ast);
                        handleJsCompletionLists(autocompleteInfo, result);
                    }
                    catch (System.Exception)
                    {
                        break;
                    }
                }

                return Task.FromResult( new CompletionList(result) );
            }

            var items = new List<CompletionItem>(VibeHelper.Completions);

            var currentLine = _cache.GetLine(request.Position.Line);

            currentLine = currentLine.Replace("\"", "-");

            var templateLiteralRegex = new Regex(@"(Template.*?=.*?)(.).*(-)");

            var match = templateLiteralRegex.Match(currentLine);

            if (match.Groups.Count != 4)
            {
                return Task.FromResult(new CompletionList(items));
            }

            var positionFirstQuote = templateLiteralRegex.Match(currentLine).Groups[2].Index + 2;
            var positionSecondQuote = templateLiteralRegex.Match(currentLine).Groups[3].Index + 2;

            if (positionFirstQuote < request.Position.Character && positionSecondQuote > request.Position.Character)
            {
                return Task.FromResult(new CompletionList(items));
            }

            var fieldRegex = new Regex("Component|Field|Fields");
            var fieldMatch = fieldRegex.Match(currentLine);

            if (fieldMatch.Success)
            {
                items.Clear();

                foreach (var item in _cache.GetFields())
                {
                    items.Add(new CompletionItem { 
                        Label = item,
                        Kind = CompletionItemKind.Reference,
                        InsertText = item
                    });
                }

                return Task.FromResult(new CompletionList(items));
            }

            var resourceRegex = new Regex("Resource");
            var resourceMatch = resourceRegex.Match(currentLine);

            if (resourceMatch.Success)
            {
                foreach (var item in _cache.GetResources())
                {
                    items.Add(new CompletionItem
                    {
                        Label = item,
                        Kind = CompletionItemKind.Reference,
                        InsertText = item
                    });
                }

                return Task.FromResult(new CompletionList(items));
            }

            return Task.FromResult(new CompletionList(items));
        }


        private void handleJsCompletionLists(JsAutocompleteInfo info, HashSet<CompletionItem> result)
        {
            // TODO Much code repetition - improve
            foreach (var func in info.FunctionNames)
            {
                var funcCompletionItem = new CompletionItem()
                {
                    Label = func,
                    Kind = CompletionItemKind.Function,
                    InsertText = func
                };

                result.Add(funcCompletionItem);
            }

            foreach (var variable in info.VariableNames)
            {
                var varCompletionItem = new CompletionItem()
                {
                    Label = variable,
                    Kind = CompletionItemKind.Variable,
                    InsertText = variable
                };

                result.Add(varCompletionItem);
            }

            foreach (var param in info.ParameterNames)
            {
                var paramCompletionItem = new CompletionItem()
                {
                    Label = param,
                    Kind = CompletionItemKind.TypeParameter,
                    InsertText = param
                };

                result.Add(paramCompletionItem);
            }

            foreach (var prop in info.PropertyNames)
            {
                var propCompletionItem = new CompletionItem()
                {
                    Label = prop,
                    Kind = CompletionItemKind.Property,
                    InsertText = prop
                };

                result.Add(propCompletionItem);
            }
        }
    }

    public sealed class JsAutocompleteInfo
    {
        public HashSet<string> FunctionNames { get; } = new();
        public HashSet<string> VariableNames { get; } = new();
        public HashSet<string> ParameterNames { get; } = new();
        public HashSet<string> PropertyNames { get; } = new();
        public HashSet<string> RequireModules { get; } = new();
    }

    // TODO Chat GPT written from now on, kinda different than JS acorn, you can improve the handling a lot, but it works for now
    public static class JsAstAutocompleteExtractor
    {
        public static JsAutocompleteInfo Extract(Node root)
        {
            var result = new JsAutocompleteInfo();
            Walk(root, result);
            return result;
        }

        private static void Walk(Node node, JsAutocompleteInfo result)
        {
            switch (node)
            {
                // --------------------------------------------------------------------
                // Function declarations (function foo() {})
                // --------------------------------------------------------------------
                case FunctionDeclaration fn:
                    if (fn.Id != null)
                        result.FunctionNames.Add(fn.Id.Name);

                    foreach (var param in fn.Params)
                        ExtractPatternIdentifiers(param, result.ParameterNames);

                    break;

                // --------------------------------------------------------------------
                // Require modules handling require('underscore') for example
                // --------------------------------------------------------------------
                case CallExpression exp:
                    if (exp.Callee is Identifier requireId &&
                        requireId.Name == "require" &&
                        exp.Arguments.Count == 1 &&
                        exp.Arguments[0] is Literal literal &&
                        literal.Value is string s)
                    {
                        result.RequireModules.Add(s);
                    }
                    break;

                // --------------------------------------------------------------------
                // Variable declarations (var x = ..., let y = ..., const z = ...)
                // --------------------------------------------------------------------
                case VariableDeclaration varDecl:
                    foreach (var decl in varDecl.Declarations)
                        ExtractPatternIdentifiers(decl.Id, result.VariableNames);
                    break;

                // --------------------------------------------------------------------
                // Functions assigned via expressions: obj.prop = function() { ... }
                // --------------------------------------------------------------------
                case AssignmentExpression assign:
                    // LHS: must be MemberExpression
                    if (assign.Left is MemberExpression member)
                    {
                        var fullName = ResolveMemberName(member);

                        // obj.prop = function(...) { }
                        if (assign.Right is FunctionExpression fnExpr)
                        {
                            result.FunctionNames.Add(fullName);

                            foreach (var p in fnExpr.Params)
                                ExtractPatternIdentifiers(p, result.ParameterNames);
                        }
                        else if (assign.Right is ArrowFunctionExpression arrowFn)
                        {
                            result.FunctionNames.Add(fullName);

                            foreach (var p in arrowFn.Params)
                                ExtractPatternIdentifiers(p, result.ParameterNames);
                        }

                        // obj.prop = { key: value, key2: value2 }
                        if (assign.Right is ObjectExpression objExpr)
                        {
                            foreach (var prop in objExpr.Properties)
                            {
                                if (prop is Property p && p.Key is Identifier id)
                                {
                                    result.PropertyNames.Add($"{fullName}.{id.Name}");
                                }
                            }
                        }
                    }
                    break;

                // --------------------------------------------------------------------
                // Function expressions and arrow functions in general
                // --------------------------------------------------------------------
                case FunctionExpression fnExp:
                    foreach (var p in fnExp.Params)
                        ExtractPatternIdentifiers(p, result.ParameterNames);
                    break;

                case ArrowFunctionExpression arrow:
                    foreach (var p in arrow.Params)
                        ExtractPatternIdentifiers(p, result.ParameterNames);
                    break;
            }

            // Recursively walk children
            foreach (var child in node.ChildNodes)
                Walk(child, result);
        }

        // Extract identifiers out of patterns
        private static void ExtractPatternIdentifiers(Node pattern, HashSet<string> output)
        {
            switch (pattern)
            {
                case Identifier id:
                    output.Add(id.Name);
                    break;

                case ArrayPattern arr:
                    foreach (var elem in arr.Elements)
                        if (elem != null)
                            ExtractPatternIdentifiers(elem, output);
                    break;

                case ObjectPattern obj:
                    foreach (var prop in obj.Properties)
                        foreach (var descendant in prop.ChildNodes)
                            ExtractPatternIdentifiers(descendant, output);
                    break;

                case AssignmentPattern assign:
                    ExtractPatternIdentifiers(assign.Left, output);
                    break;
            }
        }

        // Resolve MemberExpression chain: e.g. Test.getDataType
        private static string ResolveMemberName(MemberExpression member)
        {
            var parts = new List<string>();

            while (member != null)
            {
                if (member.Property is Identifier id)
                    parts.Add(id.Name);

                if (member.Object is Identifier parentId)
                {
                    parts.Add(parentId.Name);
                    break;
                }

                member = member.Object as MemberExpression;
            }

            parts.Reverse();
            return string.Join(".", parts);
        }
    }
}