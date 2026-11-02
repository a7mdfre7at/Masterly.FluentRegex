using System;
using System.Reflection;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>Looks up the methods this package translates.</summary>
internal static class ReflectionHelper
{
    private const BindingFlags MethodFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>Returns the method <paramref name="name"/> of <paramref name="type"/> with exactly <paramref name="parameters"/>.</summary>
    /// <exception cref="MissingMethodException">No such method exists.</exception>
    public static MethodInfo GetMethod(Type type, string name, params Type[] parameters) =>
        type.GetMethod(name, MethodFlags, null, parameters, null)
        ?? throw new MissingMethodException(type.FullName, name);
}
