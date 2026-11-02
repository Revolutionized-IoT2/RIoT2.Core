using RIoT2.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace RIoT2.Core.Plugins
{
    /// <summary>
    /// Describes and binds device options classes: the properties marked with <see cref="DeviceParameterAttribute"/>
    /// (plan M12 step 4). <see cref="Abstracts.DeviceBase{TOptions}"/> binds before <c>ConfigureDevice</c>, and
    /// <see cref="DeviceTemplates"/> puts the description into the template.
    /// </summary>
    public static class DeviceOptions
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<Parameter>> Cache = new ConcurrentDictionary<Type, IReadOnlyList<Parameter>>();

        /// <summary>The parameter definitions of an options class, in declaration order.</summary>
        /// <exception cref="InvalidOperationException">The class is not a valid options class: a duplicate key, a
        /// property without a public setter, a type that can't be read from text, or a default that doesn't convert.</exception>
        public static IReadOnlyList<DeviceParameterDefinition> Describe(Type optionsType) =>
            Parameters(optionsType).Select(p => Copy(p.Definition)).ToList();

        public static IReadOnlyList<DeviceParameterDefinition> Describe<TOptions>() where TOptions : class, new() =>
            Describe(typeof(TOptions));

        /// <summary>
        /// Binds device parameters to a new options instance. Keys match case-insensitively. A missing or empty
        /// value takes the attribute's <see cref="DeviceParameterAttribute.Default"/>, or else keeps the property's
        /// initial value; a required one without a default is a problem.
        /// </summary>
        /// <exception cref="DeviceOptionsException">Listing every missing or invalid parameter.</exception>
        public static TOptions Bind<TOptions>(IDictionary<string, string>? parameters) where TOptions : class, new() =>
            (TOptions)Bind(typeof(TOptions), parameters, null);

        internal static object Bind(Type optionsType, IDictionary<string, string>? parameters, Action<string>? read)
        {
            var list = Parameters(optionsType);
            var options = Activator.CreateInstance(optionsType)!;
            var problems = new List<string>();
            foreach (var parameter in list)
            {
                var key = parameter.Definition.Key;
                read?.Invoke(key);
                var raw = Find(parameters, key);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    if (parameter.Attribute.Default != null)
                        raw = parameter.Attribute.Default;
                    else
                    {
                        if (parameter.Definition.Required)
                            problems.Add($"{key} is required");
                        continue;
                    }
                }

                if (TryConvert(parameter.ValueType, raw!, out var value))
                    parameter.Property.SetValue(options, value);
                else if (parameter.Definition.Secret)
                    problems.Add($"{key} is not a valid {Describe(parameter.Definition.Type)}");
                else
                    problems.Add($"{key}: '{raw}' is not a valid {Describe(parameter.Definition.Type)}");
            }

            if (problems.Count > 0)
                throw new DeviceOptionsException(problems);
            return options;
        }

        /// <summary>
        /// The value stored under <paramref name="key"/>: an exact match, or else one that differs only in case,
        /// because the configuration download camel-cases dictionary keys (divergence C1). Null when absent.
        /// </summary>
        internal static string? Find(IDictionary<string, string>? parameters, string? key)
        {
            if (parameters == null || key == null)
                return null;
            if (parameters.TryGetValue(key, out var exact))
                return exact;
            foreach (var pair in parameters)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
            return null;
        }

        private static IReadOnlyList<Parameter> Parameters(Type optionsType)
        {
            if (optionsType == null)
                throw new ArgumentNullException(nameof(optionsType));
            return Cache.GetOrAdd(optionsType, Build);
        }

        private static IReadOnlyList<Parameter> Build(Type optionsType)
        {
            if (optionsType.IsAbstract || optionsType.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException($"{optionsType.FullName} needs a public parameterless constructor to be a device options class.");

            var initial = Activator.CreateInstance(optionsType);
            var parameters = new List<Parameter>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.MetadataToken))
            {
                var attribute = property.GetCustomAttribute<DeviceParameterAttribute>(inherit: true);
                if (attribute == null)
                    continue;

                var where = $"{optionsType.FullName}.{property.Name}";
                if (property.SetMethod == null || !property.SetMethod.IsPublic || property.GetMethod == null)
                    throw new InvalidOperationException($"{where} needs a public getter and setter to be a device parameter.");
                if (!keys.Add(attribute.Key))
                    throw new InvalidOperationException($"{where}: the device parameter key '{attribute.Key}' is used twice.");

                var valueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (valueType != typeof(string) && !TypeDescriptor.GetConverter(valueType).CanConvertFrom(typeof(string)))
                    throw new InvalidOperationException($"{where}: a {valueType.Name} can't be read from a device parameter.");
                if (attribute.Default != null && !TryConvert(valueType, attribute.Default, out _))
                    throw new InvalidOperationException($"{where}: the default '{attribute.Default}' isn't a valid {valueType.Name}.");

                parameters.Add(new Parameter
                {
                    Property = property,
                    Attribute = attribute,
                    ValueType = valueType,
                    Definition = new DeviceParameterDefinition
                    {
                        Key = attribute.Key,
                        Type = TypeOf(valueType),
                        Required = attribute.Required,
                        Secret = attribute.Secret,
                        Default = attribute.Default ?? (attribute.Required ? null : Format(property.GetValue(initial))),
                        Description = attribute.Description
                    }
                });
            }
            return parameters;
        }

        private static bool TryConvert(Type type, string raw, out object? value)
        {
            value = null;
            if (type == typeof(string))
            {
                value = raw;
                return true;
            }
            if (type == typeof(bool))
            {
                if (!bool.TryParse(raw.Trim(), out var flag))
                    return false;
                value = flag;
                return true;
            }
            try
            {
                value = TypeDescriptor.GetConverter(type).ConvertFromInvariantString(raw.Trim());
                return value != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string? Format(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string text:
                    return text.Length == 0 ? null : text;
                case bool flag:
                    return flag ? "true" : "false";
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return TypeDescriptor.GetConverter(value.GetType()).ConvertToInvariantString(value);
            }
        }

        private static DeviceParameterType TypeOf(Type type)
        {
            if (type == typeof(bool))
                return DeviceParameterType.Boolean;
            if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
                type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
                return DeviceParameterType.Integer;
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return DeviceParameterType.Number;
            return DeviceParameterType.Text;
        }

        private static string Describe(DeviceParameterType type)
        {
            switch (type)
            {
                case DeviceParameterType.Integer: return "whole number";
                case DeviceParameterType.Number: return "number";
                case DeviceParameterType.Boolean: return "boolean (true or false)";
                default: return "value";
            }
        }

        private static DeviceParameterDefinition Copy(DeviceParameterDefinition definition) => new DeviceParameterDefinition
        {
            Key = definition.Key,
            Type = definition.Type,
            Required = definition.Required,
            Secret = definition.Secret,
            Default = definition.Default,
            Description = definition.Description
        };

        private sealed class Parameter
        {
            public PropertyInfo Property { get; set; } = null!;
            public DeviceParameterAttribute Attribute { get; set; } = null!;
            public Type ValueType { get; set; } = null!;
            public DeviceParameterDefinition Definition { get; set; } = null!;
        }
    }
}
