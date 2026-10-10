using System.Reflection;

// Metadata only: never instantiate interop types or invoke native methods.
string directory = args[0], filter = args[1];
AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    string name = new AssemblyName(e.Name).Name + ".dll";
    foreach (string root in new[] { directory, Path.Combine(Path.GetDirectoryName(directory)!, "core") })
        if (File.Exists(Path.Combine(root, name))) return Assembly.LoadFrom(Path.Combine(root, name));
    return null;
};
foreach (string path in Directory.GetFiles(directory, "Sprocket*.dll"))
{
    Type[] types;
    try { types = Assembly.LoadFrom(path).GetTypes(); }
    catch (ReflectionTypeLoadException ex) { types = ex.Types.OfType<Type>().ToArray(); }
    foreach (var type in types.Where(t => t.FullName!.Contains(filter, StringComparison.OrdinalIgnoreCase) && !t.FullName.StartsWith("Il2Cpp")))
    {
        Console.WriteLine($"=== {type.FullName} ({Path.GetFileName(path)}) ===");
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            try { Console.WriteLine($"property {p.PropertyType.Name} {p.Name}"); } catch (TypeLoadException) { }
        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
            try { Console.WriteLine($"method {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})"); } catch (TypeLoadException) { }
        foreach (var c in type.GetConstructors())
            try { Console.WriteLine($"ctor ({string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})"); } catch (TypeLoadException) { }
    }
}
