using System;
using System.Collections.Generic;
using System.Text;
using SlugBase;

namespace MotherMod
{
    internal static class FoxParamsMerge
    {
        internal struct Member
        {
            public string Key;
            public string RawKey;
            public string RawValue;
        }

        internal static List<Member> Members(string text)
        {
            var list = new List<Member>();
            int i = 0;
            SkipWs(text, ref i);
            Expect(text, ref i, '{');
            SkipWs(text, ref i);
            if (i < text.Length && text[i] == '}') return list;
            while (true)
            {
                SkipWs(text, ref i);
                int ks = i;
                SkipString(text, ref i);
                string rawKey = text.Substring(ks, i - ks);
                SkipWs(text, ref i);
                Expect(text, ref i, ':');
                SkipWs(text, ref i);
                int vs = i;
                SkipValue(text, ref i);
                list.Add(new Member { Key = DecodeKey(rawKey), RawKey = rawKey, RawValue = text.Substring(vs, i - vs) });
                SkipWs(text, ref i);
                if (i >= text.Length) throw new FormatException("unterminated object");
                char c = text[i++];
                if (c == ',') continue;
                if (c == '}') break;
                throw new FormatException("unexpected '" + c + "' at " + (i - 1));
            }
            return list;
        }

        internal static string Merge(List<Member> baseMembers, List<Member> variantMembers,
                                     List<string> unknown, out int overrides)
        {
            overrides = 0;
            var over = new Dictionary<string, Member>();
            if (variantMembers != null)
                foreach (var m in variantMembers)
                    if (!m.Key.StartsWith("_")) over[m.Key] = m;

            var baseKeys = new HashSet<string>();
            var sb = new StringBuilder(64 * 1024);
            sb.Append('{');
            bool first = true;
            foreach (var b in baseMembers)
            {
                if (b.Key.StartsWith("_")) continue;
                baseKeys.Add(b.Key);
                string raw = b.RawValue;
                string rawKey = b.RawKey;
                if (over.TryGetValue(b.Key, out var v))
                {
                    raw = v.RawValue;
                    overrides++;
                }
                Append(sb, ref first, rawKey, raw);
            }
            if (variantMembers != null)
                foreach (var m in variantMembers)
                {
                    if (m.Key.StartsWith("_") || baseKeys.Contains(m.Key)) continue;
                    unknown?.Add(m.Key);
                    Append(sb, ref first, m.RawKey, m.RawValue);
                }
            sb.Append('}');
            return sb.ToString();
        }

        internal static JsonObject ToObject(string variantId, string merged)
        {
            var sb = new StringBuilder(merged.Length + variantId.Length + 8);
            sb.Append("{\"").Append(variantId).Append("\": ").Append(merged).Append('}');
            return JsonAny.Parse(sb.ToString()).AsObject().GetObject(variantId);
        }

        private static void Append(StringBuilder sb, ref bool first, string rawKey, string rawValue)
        {
            if (!first) sb.Append(",\n");
            first = false;
            sb.Append(rawKey).Append(": ").Append(rawValue);
        }

        private static string DecodeKey(string rawKey)
        {
            if (rawKey.IndexOf('\\') < 0) return rawKey.Substring(1, rawKey.Length - 2);
            return JsonAny.Parse(rawKey).AsString();
        }

        private static bool IsWs(char c)
        {
            return c == ' ' || c == '\t' || c == '\r' || c == '\n';
        }

        private static void SkipWs(string t, ref int i)
        {
            while (i < t.Length && IsWs(t[i])) i++;
        }

        private static void Expect(string t, ref int i, char c)
        {
            if (i >= t.Length || t[i] != c)
                throw new FormatException("expected '" + c + "' at " + i);
            i++;
        }

        private static void SkipString(string t, ref int i)
        {
            Expect(t, ref i, '"');
            while (i < t.Length && t[i] != '"')
                i += t[i] == '\\' ? 2 : 1;
            if (i >= t.Length) throw new FormatException("unterminated string");
            i++;
        }

        private static void SkipValue(string t, ref int i)
        {
            if (i >= t.Length) throw new FormatException("missing value");
            char c = t[i];
            if (c == '"')
            {
                SkipString(t, ref i);
                return;
            }
            if (c == '{' || c == '[')
            {
                int depth = 0;
                while (i < t.Length)
                {
                    char d = t[i];
                    if (d == '"')
                    {
                        SkipString(t, ref i);
                        continue;
                    }
                    if (d == '{' || d == '[') depth++;
                    else if (d == '}' || d == ']') depth--;
                    i++;
                    if (depth == 0) return;
                }
                throw new FormatException("unterminated container");
            }
            int s = i;
            while (i < t.Length && !IsWs(t[i]) && t[i] != ',' && t[i] != '}' && t[i] != ']') i++;
            if (i == s) throw new FormatException("empty value at " + s);
        }
    }
}
