using System.Globalization;
using System.Text;

namespace FunctionWorld.wit.exports.fluor.fun.v0_2_0;

public class HandlerImpl : IHandler
{
    public static IHandler.Response Handle(IHandler.Request request)
    {
        var requestId = HeaderValue(request.headers, "x-request-id");
        return request.method switch
        {
            "GET" => ListItems(request, requestId),
            "POST" => CreateItem(request, requestId),
            "PUT" => UpdateItem(request, requestId),
            "DELETE" => DeleteItem(request, requestId),
            _ => throw Failure(IHandler.ErrorCode.BAD_REQUEST, $"method {request.method} is not supported"),
        };
    }

    static IHandler.Response ListItems(IHandler.Request request, string? requestId)
    {
        var name = QueryParam(request.query, "name");
        var items = new List<string> { "notebook", "pencil" };
        if (name is not null)
        {
            if (!items.Contains(name))
            {
                throw Failure(IHandler.ErrorCode.NOT_FOUND, $"item {name} was not found");
            }
            items = [name];
        }

        var rendered = Json.Array(items.Select(item => Json.Object(("name", Json.String(item)))).ToArray());
        return Respond(200, requestId, Document(request, requestId, "items", rendered));
    }

    static IHandler.Response CreateItem(IHandler.Request request, string? requestId)
    {
        var name = JsonStringField(request.body, "name");
        if (name is null)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "body must be a JSON object with a string name");
        }

        return Respond(201, requestId, Document(request, requestId, "created", Json.Object(("name", Json.String(name)))));
    }

    static IHandler.Response UpdateItem(IHandler.Request request, string? requestId)
    {
        var id = QueryParam(request.query, "id");
        if (id is null)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "query parameter id is required");
        }
        if (!Json.TryParse(request.body, out var body) || body.Kind != JsonKind.Object)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "body must be a JSON object");
        }

        var updated = Json.Object(("id", Json.String(id)), ("body", body));
        return Respond(200, requestId, Document(request, requestId, "updated", updated));
    }

    static IHandler.Response DeleteItem(IHandler.Request request, string? requestId)
    {
        var id = QueryParam(request.query, "id");
        if (id is null)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "query parameter id is required");
        }

        return Respond(200, requestId, Document(request, requestId, "deleted", Json.String(id)));
    }

    static string Document(IHandler.Request request, string? requestId, string field, Json value)
    {
        var document = Json.Object(
            ("function", Json.String("{{project-name}}")),
            ("language", Json.String("csharp")),
            ("method", Json.String(request.method)),
            ("path", Json.String(request.path)),
            ("query", request.query is null ? Json.Null() : Json.String(request.query)),
            ("requestId", requestId is null ? Json.Null() : Json.String(requestId)),
            (field, value));
        return document.Write();
    }

    static IHandler.Response Respond(ushort status, string? requestId, string body)
    {
        var headers = new List<IHandler.Header>
        {
            new("content-type", "application/json"),
        };
        if (requestId is not null)
        {
            headers.Add(new IHandler.Header("x-request-id", requestId));
        }
        return new IHandler.Response(status, headers, body);
    }

    static global::FunctionWorld.WitException Failure(IHandler.ErrorCode code, string message)
    {
        return new global::FunctionWorld.WitException(new IHandler.Error(code, message), 0);
    }

    static string? HeaderValue(List<IHandler.Header> headers, string name)
    {
        foreach (var header in headers)
        {
            if (header.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return header.value;
            }
        }
        return null;
    }

    static string? QueryParam(string? query, string key)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }
        foreach (var part in query.Split('&'))
        {
            var separator = part.IndexOf('=');
            if (separator > 0 && FormDecode(part[..separator]) == key)
            {
                return FormDecode(part[(separator + 1)..]);
            }
        }
        return null;
    }

    static string FormDecode(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    static string? JsonStringField(string body, string field)
    {
        if (!Json.TryParse(body, out var value) || value.Kind != JsonKind.Object)
        {
            return null;
        }
        if (!value.TryField(field, out var fieldValue) || fieldValue.Kind != JsonKind.String)
        {
            return null;
        }
        return string.IsNullOrEmpty(fieldValue.Text) ? null : fieldValue.Text;
    }
}

enum JsonKind
{
    Null,
    Boolean,
    Number,
    String,
    Array,
    Object,
}

sealed class Json
{
    public JsonKind Kind { get; private init; }
    public bool Boolean { get; private init; }
    public string Text { get; private init; } = "";
    public List<Json> Items { get; private init; } = [];
    public List<(string Name, Json Value)> Fields { get; private init; } = [];

    public static Json Null() => new() { Kind = JsonKind.Null };
    public static Json Bool(bool value) => new() { Kind = JsonKind.Boolean, Boolean = value };
    public static Json Number(string lexeme) => new() { Kind = JsonKind.Number, Text = lexeme };
    public static Json String(string value) => new() { Kind = JsonKind.String, Text = value };
    public static Json Array(params Json[] items) => new() { Kind = JsonKind.Array, Items = [.. items] };
    public static Json Object(params (string Name, Json Value)[] fields) => new() { Kind = JsonKind.Object, Fields = [.. fields] };

    public bool TryField(string name, out Json value)
    {
        foreach (var field in Fields)
        {
            if (field.Name == name)
            {
                value = field.Value;
                return true;
            }
        }
        value = Null();
        return false;
    }

    public static bool TryParse(string text, out Json value)
    {
        var parser = new Parser(text);
        if (!parser.TryValue(out value) || !parser.AtEnd())
        {
            value = Null();
            return false;
        }
        return true;
    }

    public string Write()
    {
        var output = new StringBuilder();
        WriteTo(output);
        return output.ToString();
    }

    void WriteTo(StringBuilder output)
    {
        switch (Kind)
        {
            case JsonKind.Null:
                output.Append("null");
                break;
            case JsonKind.Boolean:
                output.Append(Boolean ? "true" : "false");
                break;
            case JsonKind.Number:
                output.Append(Text);
                break;
            case JsonKind.String:
                output.Append('"');
                foreach (var character in Text)
                {
                    switch (character)
                    {
                        case '"': output.Append("\\\""); break;
                        case '\\': output.Append("\\\\"); break;
                        case '\b': output.Append("\\b"); break;
                        case '\f': output.Append("\\f"); break;
                        case '\n': output.Append("\\n"); break;
                        case '\r': output.Append("\\r"); break;
                        case '\t': output.Append("\\t"); break;
                        default:
                            if (character < 0x20)
                            {
                                output.Append("\\u");
                                output.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                output.Append(character);
                            }
                            break;
                    }
                }
                output.Append('"');
                break;
            case JsonKind.Array:
                output.Append('[');
                for (var index = 0; index < Items.Count; index++)
                {
                    if (index > 0)
                    {
                        output.Append(',');
                    }
                    Items[index].WriteTo(output);
                }
                output.Append(']');
                break;
            case JsonKind.Object:
                output.Append('{');
                for (var index = 0; index < Fields.Count; index++)
                {
                    if (index > 0)
                    {
                        output.Append(',');
                    }
                    String(Fields[index].Name).WriteTo(output);
                    output.Append(':');
                    Fields[index].Value.WriteTo(output);
                }
                output.Append('}');
                break;
        }
    }

    sealed class Parser(string text)
    {
        int index;

        public bool AtEnd()
        {
            Skip();
            return index >= text.Length;
        }

        public bool TryValue(out Json value)
        {
            value = Null();
            Skip();
            if (index >= text.Length)
            {
                return false;
            }
            return text[index] switch
            {
                '{' => TryObject(out value),
                '[' => TryArray(out value),
                '"' => TryString(out var content) && Assign(Json.String(content), out value),
                't' => Take("true") && Assign(Json.Bool(true), out value),
                'f' => Take("false") && Assign(Json.Bool(false), out value),
                'n' => Take("null") && Assign(Json.Null(), out value),
                '-' or >= '0' and <= '9' => TryNumber(out value),
                _ => Fail(out value),
            };
        }

        bool TryObject(out Json value)
        {
            index++;
            var fields = new List<(string, Json)>();
            Skip();
            if (Take("}"))
            {
                return Assign(Json.Object([.. fields]), out value);
            }
            while (true)
            {
                if (!TryString(out var name) || !Take(":") || !TryValue(out var field))
                {
                    return Fail(out value);
                }
                fields.Add((name, field));
                Skip();
                if (Take("}"))
                {
                    return Assign(Json.Object([.. fields]), out value);
                }
                if (!Take(","))
                {
                    return Fail(out value);
                }
            }
        }

        bool TryArray(out Json value)
        {
            index++;
            var items = new List<Json>();
            Skip();
            if (Take("]"))
            {
                return Assign(Json.Array([.. items]), out value);
            }
            while (true)
            {
                if (!TryValue(out var item))
                {
                    return Fail(out value);
                }
                items.Add(item);
                Skip();
                if (Take("]"))
                {
                    return Assign(Json.Array([.. items]), out value);
                }
                if (!Take(","))
                {
                    return Fail(out value);
                }
            }
        }

        bool TryString(out string value)
        {
            value = "";
            Skip();
            if (index >= text.Length || text[index] != '"')
            {
                return false;
            }
            index++;
            var content = new StringBuilder();
            while (index < text.Length)
            {
                var character = text[index++];
                if (character == '"')
                {
                    value = content.ToString();
                    return true;
                }
                if (character != '\\')
                {
                    if (character < 0x20)
                    {
                        return false;
                    }
                    content.Append(character);
                    continue;
                }
                if (index >= text.Length)
                {
                    return false;
                }
                switch (text[index++])
                {
                    case '"': content.Append('"'); break;
                    case '\\': content.Append('\\'); break;
                    case '/': content.Append('/'); break;
                    case 'b': content.Append('\b'); break;
                    case 'f': content.Append('\f'); break;
                    case 'n': content.Append('\n'); break;
                    case 'r': content.Append('\r'); break;
                    case 't': content.Append('\t'); break;
                    case 'u':
                        if (!TryHex(out var unit))
                        {
                            return false;
                        }
                        if (char.IsHighSurrogate((char)unit) && index + 1 < text.Length && text[index] == '\\' && text[index + 1] == 'u')
                        {
                            index += 2;
                            if (!TryHex(out var low) || !char.IsLowSurrogate((char)low))
                            {
                                return false;
                            }
                            content.Append(char.ConvertFromUtf32(char.ConvertToUtf32((char)unit, (char)low)));
                        }
                        else if (char.IsSurrogate((char)unit))
                        {
                            return false;
                        }
                        else
                        {
                            content.Append((char)unit);
                        }
                        break;
                    default:
                        return false;
                }
            }
            return false;
        }

        bool TryNumber(out Json value)
        {
            var start = index;
            if (text[index] == '-')
            {
                index++;
            }
            if (index >= text.Length || !char.IsAsciiDigit(text[index]))
            {
                return Fail(out value);
            }
            if (text[index] == '0')
            {
                index++;
            }
            else
            {
                while (index < text.Length && char.IsAsciiDigit(text[index]))
                {
                    index++;
                }
            }
            if (index < text.Length && text[index] == '.')
            {
                index++;
                var fraction = index;
                while (index < text.Length && char.IsAsciiDigit(text[index]))
                {
                    index++;
                }
                if (fraction == index)
                {
                    return Fail(out value);
                }
            }
            if (index < text.Length && text[index] is 'e' or 'E')
            {
                index++;
                if (index < text.Length && text[index] is '+' or '-')
                {
                    index++;
                }
                var exponent = index;
                while (index < text.Length && char.IsAsciiDigit(text[index]))
                {
                    index++;
                }
                if (exponent == index)
                {
                    return Fail(out value);
                }
            }
            return Assign(Json.Number(text[start..index]), out value);
        }

        bool TryHex(out int value)
        {
            value = 0;
            if (index + 4 > text.Length)
            {
                return false;
            }
            for (var offset = 0; offset < 4; offset++)
            {
                var character = text[index++];
                var digit = character switch
                {
                    >= '0' and <= '9' => character - '0',
                    >= 'a' and <= 'f' => character - 'a' + 10,
                    >= 'A' and <= 'F' => character - 'A' + 10,
                    _ => -1,
                };
                if (digit < 0)
                {
                    return false;
                }
                value = (value << 4) + digit;
            }
            return true;
        }

        bool Take(string token)
        {
            Skip();
            if (!text.AsSpan(index).StartsWith(token))
            {
                return false;
            }
            index += token.Length;
            return true;
        }

        void Skip()
        {
            while (index < text.Length && text[index] is ' ' or '\t' or '\n' or '\r')
            {
                index++;
            }
        }

        static bool Assign(Json parsed, out Json value)
        {
            value = parsed;
            return true;
        }

        static bool Fail(out Json value)
        {
            value = Null();
            return false;
        }
    }
}
