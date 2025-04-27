using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using Newtonsoft.Json;

public static class DebugUtils
{
    // Maximum depth to recurse into nested objects.
    private const int MaxDepth = 1;
    /// <summary>
    /// Logs the JSON representation of an object.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="label">A label to identify the logged output.</param>
    public static void LogObjectJson(object obj, string label = "Object")
    {
        try
        {
            object jsonRepresentation = GetObjectRepresentation(obj, 0);
            string json = JsonConvert.SerializeObject(jsonRepresentation, Formatting.Indented);
            MelonLogger.Msg($"{label} JSON:\n{json}");
        }
        catch (Exception ex)
        {
            MelonLogger.Msg($"Error serializing {label}: {ex.Message}");
        }
    }

    /// <summary>
    /// Recursively builds an object representation for JSON serialization.
    /// </summary>
    /// <param name="obj">The object to represent.</param>
    /// <param name="depth">The current recursion depth.</param>
    /// <returns>A dictionary, list, or primitive that represents the object.</returns>
    private static object? GetObjectRepresentation(object? obj, int depth)
    {
        if (obj == null)
            return null;

        // If the object is already a "simple" value, just return it.
        Type type = obj.GetType();
        if (IsSimpleType(type))
            return obj;

        // Prevent going deeper than allowed
        if (depth >= MaxDepth)
            return obj.ToString();

        // If obj is enumerable (but not a string), process as a list.
        if (obj is IEnumerable enumerable && !(obj is string))
        {
            List<object?> items = new List<object?>();
            foreach (var item in enumerable)
                items.Add(GetObjectRepresentation(item, depth + 1));
            return items;
        }

        // Create a dictionary to hold field/property names and values.
        Dictionary<string, object?> result = new Dictionary<string, object?>();

        // Get fields
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            object? fieldValue = field.GetValue(obj);
            result[field.Name] = GetObjectRepresentation(fieldValue, depth + 1);
        }

        // Get properties
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.GetIndexParameters().Length > 0 || !prop.CanRead)
                continue;
            object? propValue = null;
            try
            {
                propValue = prop.GetValue(obj, null);
            }
            catch (Exception ex)
            {
                // In case a property getter fails, log the error message.
                propValue = $"<Error: {ex.Message}>";
            }
            result[prop.Name] = GetObjectRepresentation(propValue, depth + 1);
        }

        return result;
    }

    /// <summary>
    /// Determines if a type is considered "simple" for serialization purposes.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if simple; otherwise, false.</returns>
    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type.Equals(typeof(string)) ||
               type.Equals(typeof(decimal)) ||
               type.Equals(typeof(DateTime)) ||
               type.Equals(typeof(DateTimeOffset)) ||
               type.Equals(typeof(TimeSpan)) ||
               type.Equals(typeof(Guid));
    }
}
