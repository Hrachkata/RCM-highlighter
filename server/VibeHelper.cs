using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RcmServer
{
    // This is the vibe helper, helps with vibes and stuff
    public static class VibeHelper
    {
        public static List<CompletionItem> Completions = new List<CompletionItem>{
                new CompletionItem
                {
                    Label = "Duck",
                    Kind = CompletionItemKind.Snippet,
                    Detail = "🦆",
                    InsertText = "🦆",
                    Documentation = "🦆."
                },
                new CompletionItem
                {
                    Label = "Documentation",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "<Documentation>$1</Documentation>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "IsKey",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"IsKey=""true""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Field",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Field Name=""$1"" Template=""$2""/>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "FField",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Field Name=""$1"">
    <Component Name=""$2"" Template=""$3""/>
</Field>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Component",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Component Name=""$1"" Template=""$2""/>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "UserParams",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<User>
	<Parameter Name=""$1"">
		<Documentation>Required. ""$2""</Documentation>
	</Parameter>
</User>$0",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "UserParam",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Parameter Name=""$1"">
    <Documentation>Required. ""$2""</Documentation>
</Parameter>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Params",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Parameters>
    <Parameter Name=""application/json"" Value=""{{=$1}}"" Type=""Body"" />
</Parameters>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Param",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Parameter Name=""application/json"" Value=""{{=$1}}"" Type=""Body"" />",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Name",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Name=""$1""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Value",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Value=""$1""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Type",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Type=""$1""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Default",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Default=""$1""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Iterator",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Iterator>
    <Next Value=""$1""/>
</Iterator>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Resource",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Resource",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Read",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Read",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Create",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Resource",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Update",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Resource",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Update",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Resource",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Delete",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Resource",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Enumerable",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Enumerable=""true""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Template",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Template=""$1""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Enumerable",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Enumerable=""true""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "IsKey",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"IsKey=""true""",
                    InsertTextFormat = InsertTextFormat.Snippet
                }
            };
    }
}
