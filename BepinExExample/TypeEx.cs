using System.Reflection;

namespace CrowdControl;

public static class TypeEx
{
#if NETSTANDARD
    public static bool IsAssignableTo(this Type sourceType, Type targetType)
    {
        if (targetType == null)
            throw new ArgumentNullException(nameof(targetType));

        return targetType.IsAssignableFrom(sourceType);
    }
#endif

    public static bool IsSubclassOfRawGeneric(this Type toCheck, Type generic)
    {
        while (toCheck != null && toCheck != typeof(object))
        {
            var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
            if (generic == cur)
            {
                return true;
            }
            toCheck = toCheck.BaseType;
        }
        return false;
    }

    public static bool IsFloatingPoint(this Type type) => (Type.GetTypeCode(type) is TypeCode.Single or TypeCode.Double);
    
    public static bool IsAssignableToGenericType(this Type givenType, Type genericType)
    {
        var interfaceTypes = givenType.GetInterfaces();

        foreach (var it in interfaceTypes)
        {
            if (it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
                return true;
        }

        if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
            return true;

        Type baseType = givenType.BaseType;
        if (baseType == null) return false;

        return IsAssignableToGenericType(baseType, genericType);
    }

    public static string GetSimpleName(this Type type)
    {
        if (type == typeof(int))
            return "int";
        if (type == typeof(short))
            return "short";
        if (type == typeof(byte))
            return "byte";
        if (type == typeof(bool))
            return "bool";
        if (type == typeof(long))
            return "long";
        if (type == typeof(float))
            return "float";
        if (type == typeof(double))
            return "double";
        if (type == typeof(decimal))
            return "decimal";
        if (type == typeof(string))
            return "string";
        if (type.IsGenericType)
            return type.Name.Remove(type.Name.IndexOf('`'));
        return type.Name;
    }

    public static bool IsNumeric(this Type type) =>
        Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => true,
            TypeCode.SByte => true,
            TypeCode.UInt16 => true,
            TypeCode.UInt32 => true,
            TypeCode.UInt64 => true,
            TypeCode.Int16 => true,
            TypeCode.Int32 => true,
            TypeCode.Int64 => true,
            TypeCode.Decimal => true,
            TypeCode.Single => true,
            TypeCode.Double => true,
            _ => false
        };

    public static T? GetCustomAttribute<T>(this Type type) where T : Attribute
        => (T?)type.GetCustomAttribute(typeof(T));

    public static IEnumerable<MethodInfo> GetMethodsWithAttribute<T>(this Type type) where T : Attribute
        => type.GetMethods().Where(t => t.GetCustomAttribute<T>() != null);
}