using System.Reflection;

namespace CrowdControl;

/// <summary>Provides extension methods for performing reflection operations.</summary>
/// <remarks>
/// These helpers are mostly for accessing nonpublic fields, properties, and methods.
/// They may be useful depending on the specific game and modding framework.
/// </remarks>
internal static class ReflectionEx
{
    private const BindingFlags BINDING_FLAGS =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// Sets the value of a field on the specified object.
    /// </summary>
    /// <param name="obj">The object on which to set the field.</param>
    /// <param name="prop">The name of the field.</param>
    /// <param name="val">The value to set.</param>
    public static void SetField(this object obj, string prop, object val)
    {
        FieldInfo? f = obj.GetType().GetField(prop, BINDING_FLAGS);
        f.SetValue(obj, val);
    }

    /// <summary>
    /// Gets the value of a field on the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the field.</typeparam>
    /// <param name="obj">The object from which to get the field value.</param>
    /// <param name="prop">The name of the field.</param>
    /// <returns>The value of the field.</returns>
    public static T GetField<T>(this object obj, string prop)
    {
        FieldInfo? f = obj.GetType().GetField(prop, BINDING_FLAGS);
        return (T)f.GetValue(obj);
    }

    /// <summary>
    /// Sets the value of a property on the specified object.
    /// </summary>
    /// <param name="obj">The object on which to set the property.</param>
    /// <param name="prop">The name of the property.</param>
    /// <param name="val">The value to set.</param>
    public static void SetProperty(this object obj, string prop, object val)
    {
        FieldInfo? f = obj.GetType().GetField(prop, BINDING_FLAGS);
        f.SetValue(obj, val);
    }

    /// <summary>
    /// Gets the value of a property on the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="obj">The object from which to get the property value.</param>
    /// <param name="prop">The name of the property.</param>
    /// <returns>The value of the property.</returns>
    public static T GetProperty<T>(this object obj, string prop)
    {
        FieldInfo? f = obj.GetType().GetField(prop, BINDING_FLAGS);
        return (T)f.GetValue(obj);
    }

    /// <summary>
    /// Invokes a method on the specified object.
    /// </summary>
    /// <param name="obj">The object on which to invoke the method.</param>
    /// <param name="methodName">The name of the method.</param>
    /// <param name="vals">The arguments to pass to the method.</param>
    public static void CallMethod(this object obj, string methodName, params object[] vals)
    {
        MethodInfo? p = obj.GetType().GetMethod(methodName, BINDING_FLAGS);
        p.Invoke(obj, vals);
    }

    /// <summary>
    /// Invokes a method on the specified object and returns the result.
    /// </summary>
    /// <typeparam name="T">The return type of the method.</typeparam>
    /// <param name="obj">The object on which to invoke the method.</param>
    /// <param name="methodName">The name of the method.</param>
    /// <param name="vals">The arguments to pass to the method.</param>
    /// <returns>The result of the method.</returns>
    public static T CallMethod<T>(this object obj, string methodName, params object[] vals)
    {
        MethodInfo? p = obj.GetType().GetMethod(methodName, BINDING_FLAGS);
        return (T)p.Invoke(obj, vals);
    }
}
