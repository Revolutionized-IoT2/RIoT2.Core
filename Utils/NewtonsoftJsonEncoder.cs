using System.Text.Encodings.Web;

namespace RIoT2.Core.Utils
{
    /// <summary>
    /// Escapes what Newtonsoft's default <c>StringEscapeHandling</c> escaped and nothing else: control
    /// characters (<c>\b</c>, <c>\t</c>, <c>\n</c>, <c>\f</c>, <c>\r</c>, otherwise <c>\u00xx</c> in
    /// lowercase hex), <c>"</c>, <c>\</c>, U+0085, U+2028 and U+2029. Emoji, DEL and HTML characters are
    /// written as they are, so stored files and configuration hashes keep Newtonsoft's bytes.
    /// </summary>
    internal sealed class NewtonsoftJsonEncoder : JavaScriptEncoder
    {
        public static NewtonsoftJsonEncoder Instance { get; } = new NewtonsoftJsonEncoder();

        private NewtonsoftJsonEncoder() { }

        // A lone surrogate is escaped as one \uXXXX; a valid pair is never escaped.
        public override int MaxOutputCharactersPerInputCharacter => 6;

        public override bool WillEncode(int unicodeScalar) =>
            unicodeScalar < 0x20 || unicodeScalar == '"' || unicodeScalar == '\\'
            || unicodeScalar == 0x85 || unicodeScalar == 0x2028 || unicodeScalar == 0x2029
            || (unicodeScalar >= 0xD800 && unicodeScalar <= 0xDFFF);

        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
        {
            for (var i = 0; i < textLength; i++)
            {
                var c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < textLength && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                    continue;
                }
                if (WillEncode(c))
                    return i;
            }
            return -1;
        }

        public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
        {
            string text;
            switch (unicodeScalar)
            {
                case '"': text = "\\\""; break;
                case '\\': text = "\\\\"; break;
                case '\b': text = "\\b"; break;
                case '\t': text = "\\t"; break;
                case '\n': text = "\\n"; break;
                case '\f': text = "\\f"; break;
                case '\r': text = "\\r"; break;
                default:
                    if (WillEncode(unicodeScalar))
                        text = "\\u" + unicodeScalar.ToString("x4", System.Globalization.CultureInfo.InvariantCulture);
                    else
                        text = char.ConvertFromUtf32(unicodeScalar);
                    break;
            }

            if (text.Length > bufferLength)
            {
                numberOfCharactersWritten = 0;
                return false;
            }
            for (var i = 0; i < text.Length; i++)
                buffer[i] = text[i];
            numberOfCharactersWritten = text.Length;
            return true;
        }
    }
}
