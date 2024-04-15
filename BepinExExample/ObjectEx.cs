using System.Reflection;

namespace CrowdControl;

internal static class ObjectEx
{
    public static bool TrySetField(this object obj, string propName, object value)
    {
        try
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (string.IsNullOrWhiteSpace(propName)) return false;

            FieldInfo? fInfo = obj.GetType().GetField(propName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fInfo == null) return false;

            fInfo.SetValue(obj, value);
            return true;
        }
        catch (Exception e)
        {
            TestMod.LogError(e);
            return false;
        }
    }

    public static bool TryGetField<T>(this object obj, out T result, string propName)
    {
        try
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (string.IsNullOrWhiteSpace(propName)) goto fail;

            FieldInfo? fInfo = obj.GetType().GetField(propName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fInfo == null) goto fail;

            object mResult = fInfo.GetValue(obj);
            if (!mResult.GetType().IsAssignableTo(typeof(T))) goto fail;
            result = (T)mResult;
            return true;
        }
        catch (Exception e) { TestMod.LogError(e); }

        fail:
        result = default!;
        return false;
    }

    public static bool TrySetProperty(this object obj, string propName, object value)
    {
        try
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (string.IsNullOrWhiteSpace(propName)) return false;

            PropertyInfo? pInfo = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (pInfo == null) return false;

            pInfo.SetValue(obj, value);
            return true;
        }
        catch (Exception e)
        {
            TestMod.LogError(e);
            return false;
        }
    }

    public static bool TryGetProperty<T>(this object obj, out T result, string propName)
    {
        try
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (string.IsNullOrWhiteSpace(propName)) goto fail;

            PropertyInfo? pInfo = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (pInfo == null) goto fail;

            object mResult = pInfo.GetValue(obj);
            if (!mResult.GetType().IsAssignableTo(typeof(T))) goto fail;
            result = (T)mResult;
            return true;
        }
        catch (Exception e) { TestMod.LogError(e); }

        fail:
        result = default!;
        return false;
    }

    public static bool TryCall(this object obj, string funcName, params object[] parameters)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        if (string.IsNullOrWhiteSpace(funcName)) return false;

        MethodInfo? mInfo = obj.GetType().GetMethod(funcName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (mInfo == null) return false;

        mInfo.Invoke(obj, parameters);
        return true;
    }

    public static bool TryCall<T>(this object obj, out T result, string funcName, params object[] parameters)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        if (string.IsNullOrWhiteSpace(funcName)) goto fail;

        MethodInfo? mInfo = obj.GetType().GetMethod(funcName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (mInfo == null) goto fail;

        object mResult = mInfo.Invoke(obj, parameters);
        if (!mResult.GetType().IsAssignableTo(typeof(T))) goto fail;
        result = (T)mResult;
        return true;

        fail:
        result = default!;
        return false;
    }
}