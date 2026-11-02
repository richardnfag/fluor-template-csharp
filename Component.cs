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

        var rendered = string.Join(",", items.Select(item => "{\"name\":" + JsonString(item) + "}"));
        return Respond(200, requestId, Meta(request, requestId) + ",\"items\":[" + rendered + "]}");
    }

    static IHandler.Response CreateItem(IHandler.Request request, string? requestId)
    {
        var name = JsonStringField(request.body, "name");
        if (name is null)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "body must be a JSON object with a string name");
        }

        return Respond(201, requestId, Meta(request, requestId) + ",\"created\":{\"name\":" + JsonString(name) + "}}");
    }

    static IHandler.Response UpdateItem(IHandler.Request request, string? requestId)
    {
        var id = QueryParam(request.query, "id");
        if (id is null)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "query parameter id is required");
        }
        if (!request.body.TrimStart().StartsWith('{'))
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "body must be a JSON object");
        }

        return Respond(200, requestId, Meta(request, requestId) + ",\"updated\":{\"id\":" + JsonString(id) + ",\"body\":" + JsonString(request.body.Trim()) + "}}");
    }

    static IHandler.Response DeleteItem(IHandler.Request request, string? requestId)
    {
        var id = QueryParam(request.query, "id");
        if (id is null)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "query parameter id is required");
        }

        return Respond(200, requestId, Meta(request, requestId) + ",\"deleted\":" + JsonString(id) + "}");
    }

    static string Meta(IHandler.Request request, string? requestId)
    {
        return "{\"function\":" + JsonString("{{project-name}}")
            + ",\"language\":\"csharp\",\"method\":" + JsonString(request.method)
            + ",\"path\":" + JsonString(request.path)
            + ",\"query\":" + JsonOptional(request.query)
            + ",\"requestId\":" + JsonOptional(requestId);
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
            if (separator > 0 && part[..separator] == key)
            {
                return part[(separator + 1)..];
            }
        }
        return null;
    }

    static string? JsonStringField(string body, string field)
    {
        var needle = "\"" + field + "\"";
        var start = body.IndexOf(needle, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }
        var rest = body[(start + needle.Length)..].TrimStart();
        if (!rest.StartsWith(':'))
        {
            return null;
        }
        rest = rest[1..].TrimStart();
        if (!rest.StartsWith('"'))
        {
            return null;
        }
        rest = rest[1..];
        var end = rest.IndexOf('"');
        return end < 0 ? null : rest[..end];
    }

    static string JsonString(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }

    static string JsonOptional(string? value)
    {
        return value is null ? "null" : JsonString(value);
    }
}
