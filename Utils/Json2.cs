using System;
using System.Text;
using System.Text.Json;

namespace RIoT2.Core.Utils
{
    /// <summary>
    /// This class will replace the Newtonsoft.Json library with System.Text.Json 
    /// </summary>
    public static class Json2
    {
        /// <summary>
        /// Converts a JSON string into a JavaScript object literal string.
        /// Works on .NET Standard 2.0 (install System.Text.Json via NuGet).
        /// </summary>
        /// <param name="json">A valid JSON string.</param>
        /// <param name="unquotePropertyNamesWhenSafe">When true, property names that are valid JS identifiers are not quoted.</param>
        /// <param name="prettyPrint">When true, formats with indentation and newlines.</param>
        /// <param name="indentSize">Number of spaces per indent level when prettyPrint is true.</param>
        /// <param name="useSingleQuotesForStrings">When true, uses single quotes for strings; otherwise double quotes.</param>
        /// <returns>JavaScript object literal text.</returns>
        public static string JsonToJsObjectLiteral(
            string json,
            bool unquotePropertyNamesWhenSafe = true,
            bool prettyPrint = true,
            int indentSize = 2,
            bool useSingleQuotesForStrings = true)
        {
            var options = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            JsonDocument doc = null;
            try
            {
                doc = JsonDocument.Parse(json, options);

                var sb = new StringBuilder(json.Length + 64);
                var ctx = new WriteContext(sb, prettyPrint, indentSize, useSingleQuotesForStrings, unquotePropertyNamesWhenSafe);
                WriteValue(doc.RootElement, ctx, 0);
                return sb.ToString();
            }
            finally
            {
                if (doc != null)
                    doc.Dispose();
            }
        }

        /// <summary>
        /// Safe try-version that won't throw. On error, returns false and sets error message.
        /// </summary>
        public static bool TryJsonToJsObjectLiteral(
            string json,
            out string js,
            out string error,
            bool unquotePropertyNamesWhenSafe = true,
            bool prettyPrint = true,
            int indentSize = 2,
            bool useSingleQuotesForStrings = true)
        {
            try
            {
                js = JsonToJsObjectLiteral(json, unquotePropertyNamesWhenSafe, prettyPrint, indentSize, useSingleQuotesForStrings);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                js = null;
                error = ex.Message;
                return false;
            }
        }

        private static void WriteValue(JsonElement el, WriteContext ctx, int level)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    ctx.Append('{');
                    int propCount = 0;

                    foreach (var prop in el.EnumerateObject())
                    {
                        if (propCount++ == 0)
                        {
                            if (ctx.Pretty) ctx.AppendLine();
                        }
                        else
                        {
                            ctx.Append(',');
                            if (ctx.Pretty) ctx.AppendLine();
                        }

                        if (ctx.Pretty) ctx.AppendIndent(level + 1);

                        WritePropertyName(prop.Name, ctx);
                        ctx.Append(ctx.Pretty ? ": " : ":");
                        WriteValue(prop.Value, ctx, level + 1);
                    }

                    if (propCount > 0 && ctx.Pretty)
                    {
                        ctx.AppendLine();
                        ctx.AppendIndent(level);
                    }

                    ctx.Append('}');
                    break;

                case JsonValueKind.Array:
                    ctx.Append('[');
                    int itemCount = 0;

                    foreach (var item in el.EnumerateArray())
                    {
                        if (itemCount++ == 0)
                        {
                            if (ctx.Pretty) ctx.AppendLine();
                        }
                        else
                        {
                            ctx.Append(',');
                            if (ctx.Pretty) ctx.AppendLine();
                        }

                        if (ctx.Pretty) ctx.AppendIndent(level + 1);
                        WriteValue(item, ctx, level + 1);
                    }

                    if (itemCount > 0 && ctx.Pretty)
                    {
                        ctx.AppendLine();
                        ctx.AppendIndent(level);
                    }

                    ctx.Append(']');
                    break;

                case JsonValueKind.String:
                    {
                        var s = el.GetString() ?? string.Empty;
                        ctx.Append(EscapeJsString(s, ctx.UseSingleQuotes));
                        break;
                    }

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    // Use raw text to preserve formatting (e.g., "1e-6", "0.0")
                    ctx.Append(el.GetRawText());
                    break;

                default:
                    ctx.Append(el.GetRawText());
                    break;
            }
        }

        private static void WritePropertyName(string name, WriteContext ctx)
        {
            if (ctx.UnquotePropertyNamesWhenSafe && IsValidJsIdentifier(name))
            {
                ctx.Append(name);
            }
            else
            {
                ctx.Append(EscapeJsString(name, ctx.UseSingleQuotes));
            }
        }

        /// <summary>
        /// Checks if a string is a safe JS identifier (ASCII-only heuristic).
        /// </summary>
        private static bool IsValidJsIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            char first = name[0];
            if (!(first == '$' || first == '_' || char.IsLetter(first))) return false;

            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!(c == '$' || c == '_' || char.IsLetterOrDigit(c)))
                    return false;
            }

            // ES5+ allows reserved words in object literal property names without quotes.
            return true;
        }

        /// <summary>
        /// Escapes a .NET string to a JavaScript string literal (single or double quotes), including surrounding quotes.
        /// Handles control chars, backslash, the quote char, and U+2028/U+2029.
        /// </summary>
        private static string EscapeJsString(string s, bool useSingleQuotes)
        {
            char quote = useSingleQuotes ? '\'' : '"';
            var sb = new StringBuilder(s.Length + 2);
            sb.Append(quote);

            for (int i = 0; i < s.Length; i++)
            {
                var ch = s[i];
                switch (ch)
                {
                    case '\\': sb.Append(@"\\"); break;
                    case '\b': sb.Append(@"\b"); break;
                    case '\f': sb.Append(@"\f"); break;
                    case '\n': sb.Append(@"\n"); break;
                    case '\r': sb.Append(@"\r"); break;
                    case '\t': sb.Append(@"\t"); break;
                    case '\u2028': sb.Append(@"\u2028"); break; // Line separator
                    case '\u2029': sb.Append(@"\u2029"); break; // Paragraph separator
                    default:
                        if (ch == quote)
                        {
                            sb.Append('\\').Append(quote);
                        }
                        else if (char.IsControl(ch))
                        {
                            sb.Append(@"\u").Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }

            sb.Append(quote);
            return sb.ToString();
        }

        private sealed class WriteContext
        {
            public WriteContext(StringBuilder sb, bool pretty, int indentSize, bool useSingleQuotes, bool unquotePropNames)
            {
                Sb = sb;
                Pretty = pretty;
                IndentSize = Math.Max(0, indentSize);
                UseSingleQuotes = useSingleQuotes;
                UnquotePropertyNamesWhenSafe = unquotePropNames;
            }

            public StringBuilder Sb { get; private set; }
            public bool Pretty { get; private set; }
            public int IndentSize { get; private set; }
            public bool UseSingleQuotes { get; private set; }
            public bool UnquotePropertyNamesWhenSafe { get; private set; }

            public void Append(char c) { Sb.Append(c); }
            public void Append(string s) { Sb.Append(s); }
            public void AppendLine() { Sb.AppendLine(); }

            public void AppendIndent(int level)
            {
                if (!Pretty || IndentSize == 0 || level <= 0) return;
                Sb.Append(' ', level * IndentSize);
            }
        }
    }
}
