using Serilog.Core;
using Serilog.Events;

namespace PackTracker.Logging.Destructuring;

public sealed class HttpRequestDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue? result)
    {
        result = null;

        var type = value.GetType();

        if (!string.Equals(type.Name, "HttpRequestMessage", StringComparison.Ordinal))
            return false;

        var methodProp = type.GetProperty("Method");
        var requestUriProp = type.GetProperty("RequestUri");
        var versionProp = type.GetProperty("Version");
        var headersProp = type.GetProperty("Headers");

        var method = methodProp?.GetValue(value)?.ToString();
        var requestUri = requestUriProp?.GetValue(value)?.ToString();
        var version = versionProp?.GetValue(value)?.ToString();
        var headers = headersProp?.GetValue(value)?.ToString();

        result = new StructureValue(
        [
            new LogEventProperty("Method", new ScalarValue(method)),
            new LogEventProperty("RequestUri", new ScalarValue(requestUri)),
            new LogEventProperty("Version", new ScalarValue(version)),
            new LogEventProperty("Headers", new ScalarValue(headers))
        ], "HttpRequestMessage");

        return true;
    }
}