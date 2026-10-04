using Mono.Cecil;
using System.Text.RegularExpressions;
var pattern = new Regex(args[1]);
IEnumerable<TypeDefinition> Walk(IEnumerable<TypeDefinition> types) {
    foreach (var t in types) { yield return t; foreach(var nested in Walk(t.NestedTypes)) yield return nested; }
}
foreach (var filename in Directory.Exists(args[0]) ? Directory.GetFiles(args[0], "*.dll") : new[] {args[0]}) {
using var assembly = AssemblyDefinition.ReadAssembly(filename);
foreach (var t in Walk(assembly.MainModule.Types).Where(t => pattern.IsMatch(t.FullName))) {
    Console.WriteLine($"TYPE [{assembly.Name.Name}] {t.FullName} : {t.BaseType}");
    foreach(var p in t.Properties) Console.WriteLine($"  PROPERTY {p.PropertyType} {p.Name}");
    foreach(var m in t.Methods.Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_") && !m.Name.StartsWith(".c")))
        Console.WriteLine($"  METHOD {m.ReturnType} {m.Name}({string.Join(", ", m.Parameters.Select(p => p.ParameterType + " " + p.Name))})");
}
}
