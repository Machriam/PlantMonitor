using System.ComponentModel;

namespace Plantmonitor.Shared.Extensions;

public static class EnumExtensions
{
    public static T Attribute<T>(this Enum value) where T : Attribute
    {
        var type = value.GetType();
        var name = Enum.GetName(type, value) ?? throw new Exception("Enum Name not found on " + value.ToString());
        var field = type.GetField(name) ?? throw new Exception("Custom Field not found on " + value.ToString());
        if (System.Attribute.GetCustomAttribute(field, typeof(T)) is T attr) return attr;
        throw new Exception("Custom Attribute " + nameof(T) + " not found on " + nameof(value));
    }

    public static T? GetEnumValue<T>(this string value, out bool valueExists) where T : Enum
    {
        var enumDic = Enum.GetValues(typeof(T)).Cast<T>()
            .ToDictionary(e => Enum.GetName(typeof(T), e) ?? Guid.NewGuid().ToString());
        valueExists = enumDic.ContainsKey(value);
        if (!valueExists) return default;
        return enumDic[value];
    }

    public static string GetDescription(this Enum value)
    {
        var type = value.GetType();
        var name = Enum.GetName(type, value);
        if (name != null)
        {
            var field = type.GetField(name);
            if (field != null && System.Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attr)
            {
                return attr.Description;
            }
        }
        return "";
    }
}
