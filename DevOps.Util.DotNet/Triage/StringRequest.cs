using System;
using System.Collections.Generic;
using System.Text;

namespace DevOps.Util.Triage
{
    public enum StringRequestKind
    {
        Contains,
        Equals,
    }

    public readonly struct StringRequest
    {
        public string Text { get; }
        public StringRequestKind Kind { get; }

        public StringRequest(string text, StringRequestKind kind)
        {
            Text = text;
            Kind = kind;
        }

        public string GetQueryValue()
        {
            var prefix = Kind == StringRequestKind.Equals ? "=" : "";
            if (Text.Contains(" "))
            {
                return prefix + '"' + Text + '"';
            }

            return prefix + Text;
        }

        public static StringRequest Parse(string data, StringRequestKind defaultKind)
        {
            var kind = defaultKind;
            if (data.Length > 0 && data[0] == '=')
            {
                kind = StringRequestKind.Equals;
                data = data.Substring(1);
            }

            data = data.Trim('"');
            if (string.IsNullOrEmpty(data))
            {
                throw new Exception("Must have text");
            }

            return new StringRequest(data, kind);
        }
    }
}
