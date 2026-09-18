using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace CrowdControl.CustomEffects;

/// <summary>Compiles a custom effect pack's source files into an assembly.</summary>
/// <remarks>
/// This is the only place that touches Roslyn. Nothing outside it holds a Roslyn type in a field or
/// signature, so the compiler assemblies are never loaded on a launch where every pack came back
/// from the cache - which is the normal case for a streamer who is not editing effects.
/// </remarks>
internal static class CustomEffectCompiler
{
    /// <summary>The result of a compilation attempt.</summary>
    /// <param name="assembly">The compiled assembly image, or null on failure.</param>
    /// <param name="symbols">The portable PDB, so exceptions thrown by custom effects have usable line numbers.</param>
    /// <param name="diagnostics">Compiler errors and warnings worth showing the author.</param>
    internal readonly struct Result(byte[]? assembly, byte[]? symbols, List<string> diagnostics)
    {
        public byte[]? Assembly { get; } = assembly;
        public byte[]? Symbols { get; } = symbols;
        public List<string> Diagnostics { get; } = diagnostics;
        public bool Succeeded => Assembly != null;
    }

    /// <summary>
    /// The usings every custom effect gets for free, matching the ones the mod itself compiles with.
    /// A single-file effect should not have to open with a wall of using directives.
    /// </summary>
    private const string GLOBAL_USINGS = """
        global using System;
        global using System.Collections;
        global using System.Collections.Generic;
        global using System.Linq;
        global using UnityEngine;
        global using ConnectorLib.JSON;
        global using CrowdControl;
        global using CrowdControl.Delegates.Effects;
        """;

    /// <summary>Compiles a pack.</summary>
    /// <param name="pack">The pack to compile.</param>
    /// <returns>The compiled assembly, or the diagnostics explaining why it did not compile.</returns>
    internal static Result Compile(CustomEffectPack pack)
    {
        List<string> diagnostics = new();

        CSharpParseOptions parseOptions = new(LanguageVersion.Latest, DocumentationMode.None, SourceCodeKind.Regular,
            ["CROWD_CONTROL", "CROWD_CONTROL_CUSTOM_EFFECT"]);

        List<SyntaxTree> trees =
        [
            CSharpSyntaxTree.ParseText(SourceText.From(GLOBAL_USINGS, Encoding.UTF8), parseOptions, "__globalusings.cs")
        ];

        foreach (string file in pack.SourceFiles)
        {
            try
            {
                trees.Add(CSharpSyntaxTree.ParseText(
                    SourceText.From(File.ReadAllText(file), Encoding.UTF8), parseOptions, file));
            }
            catch (Exception e)
            {
                diagnostics.Add($"{file}: could not be read ({e.Message})");
                return new(null, null, diagnostics);
            }
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            $"CrowdControl.CustomEffects.{pack.ID}",
            trees,
            GatherReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));

        using MemoryStream peStream = new();
        using MemoryStream pdbStream = new();

        EmitResult result = compilation.Emit(peStream, pdbStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

        foreach (Diagnostic diagnostic in result.Diagnostics)
        {
            if (diagnostic.Severity is not (DiagnosticSeverity.Error or DiagnosticSeverity.Warning)) continue;
            if (diagnostic.IsSuppressed) continue;
            diagnostics.Add(Describe(diagnostic));
        }

        return result.Success
            ? new(peStream.ToArray(), pdbStream.ToArray(), diagnostics)
            : new(null, null, diagnostics);
    }

    /// <summary>Formats a diagnostic the way a compiler would, so the author can act on it.</summary>
    private static string Describe(Diagnostic diagnostic)
    {
        string severity = diagnostic.Severity == DiagnosticSeverity.Error ? "error" : "warning";
        FileLinePositionSpan span = diagnostic.Location.GetLineSpan();

        if (!span.IsValid || string.IsNullOrEmpty(span.Path))
            return $"{severity} {diagnostic.Id}: {diagnostic.GetMessage()}";

        //Roslyn line and column numbers are zero-based, editors are not
        return $"{Path.GetFileName(span.Path)}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): " +
               $"{severity} {diagnostic.Id}: {diagnostic.GetMessage()}";
    }

    /// <summary>
    /// Builds the reference set from what the game already has loaded, which is exactly what a custom
    /// effect needs: the mod's own effect API, ConnectorLib, the Unity modules, and Assembly-CSharp.
    /// </summary>
    private static List<MetadataReference> GatherReferences()
    {
        List<MetadataReference> references = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;

            string location;
            try { location = assembly.Location; }
            catch { continue; } //some hosts throw rather than returning an empty location

            if (string.IsNullOrEmpty(location) || !File.Exists(location)) continue;

            string name = assembly.GetName().Name ?? location;
            if (!seen.Add(name)) continue;

            try { references.Add(MetadataReference.CreateFromFile(location)); }
            catch { seen.Remove(name); }
        }

        //the mod targets netstandard, so the facade has to be referenced explicitly - it is not
        //loaded at runtime and therefore never shows up in the loop above
        if (!seen.Contains("netstandard"))
        {
            string? runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (runtimeDir != null)
            {
                string facade = Path.Combine(runtimeDir, "netstandard.dll");
                if (File.Exists(facade))
                {
                    try { references.Add(MetadataReference.CreateFromFile(facade)); }
                    catch {/* the compile will fail with a clearer message than we could produce here */}
                }
            }
        }

        return references;
    }
}
