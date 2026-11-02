using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FunctionWorld.wit.exports.fluor.fun.v0_2_0;

public class HandlerImpl : IHandler
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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

        var rendered = new JsonArray();
        foreach (var item in items)
        {
            rendered.Add((JsonNode)new JsonObject { ["name"] = item });
        }
        return Respond(200, requestId, Document(request, requestId, "items", rendered));
    }

    static IHandler.Response CreateItem(IHandler.Request request, string? requestId)
    {
        var name = JsonStringField(request.body, "name");
        if (name is null)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "body must be a JSON object with a string name");
        }

        return Respond(201, requestId, Document(request, requestId, "created", new JsonObject { ["name"] = name }));
    }

    static IHandler.Response UpdateItem(IHandler.Request request, string? requestId)
    {
        var id = QueryParam(request.query, "id");
        if (id is null)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "query parameter id is required");
        }
        if (ParseObject(request.body) is not JsonObject body)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "body must be a JSON object");
        }

        var updated = new JsonObject
        {
            ["id"] = id,
            ["body"] = body,
        };
        return Respond(200, requestId, Document(request, requestId, "updated", updated));
    }

    static IHandler.Response DeleteItem(IHandler.Request request, string? requestId)
    {
        var id = QueryParam(request.query, "id");
        if (id is null)
        {
            throw Failure(IHandler.ErrorCode.BAD_REQUEST, "query parameter id is required");
        }

        return Respond(200, requestId, Document(request, requestId, "deleted", JsonValue.Create(id)));
    }

    static string Document(IHandler.Request request, string? requestId, string field, JsonNode? value)
    {
        var document = new JsonObject
        {
            ["function"] = "{{project-name}}",
            ["language"] = "csharp",
            ["method"] = request.method,
            ["path"] = request.path,
            ["query"] = request.query is null ? null : request.query,
            ["requestId"] = requestId,
            [field] = value,
        };
        return document.ToJsonString(JsonOptions);
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

    static JsonObject? ParseObject(string body)
    {
        try
        {
            return JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string? JsonStringField(string body, string field)
    {
        if (ParseObject(body) is not JsonObject payload)
        {
            return null;
        }
        if (payload[field] is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
        {
            return null;
        }
        var text = value.GetValue<string>();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
