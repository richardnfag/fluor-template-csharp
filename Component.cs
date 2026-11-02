namespace FunctionWorld.wit.exports.fluor.fun.v0_2_0;

public class HandlerImpl : IHandler
{
    public static IHandler.Response Handle(IHandler.Request _request)
    {
        var body = "{\"message\":\"Hello from {{project-name}}\"}";

        return new IHandler.Response(
            200,
            new List<IHandler.Header>
            {
                new("content-type", "application/json"),
            },
            body
        );
    }
}
