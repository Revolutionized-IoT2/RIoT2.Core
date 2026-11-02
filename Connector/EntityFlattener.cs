namespace RIoT2.Connector.Sdk;

/// <summary>
/// Flattens an entity value (what <c>ValueModel.GetAsObject()</c> returns for <c>ValueType.Entity</c>, nested
/// dictionaries) into dot-separated paths and their boolean or number leaves; text, nulls and arrays are skipped. Shared
/// by connectors so they map entities the same way (moved from the InfluxDB connector in Core 1.22.0).
/// </summary>
public static class EntityFlattener
{
    public static IEnumerable<(string Path, object Value)> Flatten(object? entity, string prefix = "")
    {
        if (entity is not IDictionary<string, object?> dictionary)
            yield break;
        foreach (var (key, value) in dictionary)
        {
            var path = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";
            if (value is IDictionary<string, object?>)
            {
                foreach (var nested in Flatten(value, path))
                    yield return nested;
            }
            else if (value is bool or int or long or double or float or decimal)
                yield return (path, value);
        }
    }
}
