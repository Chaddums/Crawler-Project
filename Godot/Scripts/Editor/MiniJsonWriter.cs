using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JunkbotArena
{
    /// <summary>
    /// Minimal JSON serializer. Companion to MiniJson (deserializer).
    /// Produces formatted, human-readable JSON from dictionaries and lists.
    /// </summary>
    public static class MiniJsonWriter
    {
        public static string Serialize(object obj, bool pretty = true)
        {
            var sb = new StringBuilder();
            WriteValue(sb, obj, pretty ? 0 : -1);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, int indent)
        {
            if (value == null)
            {
                sb.Append("null");
            }
            else if (value is Dictionary<string, object> dict)
            {
                WriteObject(sb, dict, indent);
            }
            else if (value is List<object> list)
            {
                WriteArray(sb, list, indent);
            }
            else if (value is string s)
            {
                WriteString(sb, s);
            }
            else if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
            }
            else if (value is double d)
            {
                // Avoid trailing zeros for whole numbers
                sb.Append(d == (long)d
                    ? ((long)d).ToString(CultureInfo.InvariantCulture)
                    : d.ToString("G", CultureInfo.InvariantCulture));
            }
            else if (value is float f)
            {
                sb.Append(f == (long)f
                    ? ((long)f).ToString(CultureInfo.InvariantCulture)
                    : f.ToString("G", CultureInfo.InvariantCulture));
            }
            else if (value is int i)
            {
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
            }
            else if (value is long l)
            {
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                // Fallback: treat as string
                WriteString(sb, value.ToString());
            }
        }

        private static void WriteObject(StringBuilder sb, Dictionary<string, object> dict, int indent)
        {
            bool pretty = indent >= 0;
            sb.Append('{');

            bool first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(',');
                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    Indent(sb, indent + 1);
                }

                WriteString(sb, kvp.Key);
                sb.Append(pretty ? ": " : ":");
                WriteValue(sb, kvp.Value, pretty ? indent + 1 : -1);
            }

            if (pretty && dict.Count > 0)
            {
                sb.Append('\n');
                Indent(sb, indent);
            }
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, List<object> list, int indent)
        {
            bool pretty = indent >= 0;
            sb.Append('[');

            // Use inline format for simple arrays (all primitives)
            bool allPrimitive = true;
            foreach (var item in list)
            {
                if (item is Dictionary<string, object> || item is List<object>)
                {
                    allPrimitive = false;
                    break;
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');

                if (pretty && !allPrimitive)
                {
                    sb.Append('\n');
                    Indent(sb, indent + 1);
                }
                else if (i > 0)
                {
                    sb.Append(' ');
                }

                WriteValue(sb, list[i], pretty ? indent + 1 : -1);
            }

            if (pretty && !allPrimitive && list.Count > 0)
            {
                sb.Append('\n');
                Indent(sb, indent);
            }
            sb.Append(']');
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append($"\\u{(int)c:X4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private static void Indent(StringBuilder sb, int level)
        {
            for (int i = 0; i < level; i++)
                sb.Append("  ");
        }
    }
}
