using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;

namespace CrowdControl.CustomEffects;

/// <summary>A single unit of custom effects loaded from disk.</summary>
/// <remarks>
/// Each immediate subfolder of the game's custom effect folder is one pack, compiled as a single
/// assembly so a multi-file effect can share types across its own files. A loose <c>.cs</c> file at
/// the top level is a pack of its own, which keeps one person's syntax error from taking down
/// everyone else's effects.
/// </remarks>
public class CustomEffectPack
{
    /// <summary>The pack ID, derived from the folder or file name.</summary>
    public string ID { get; }

    /// <summary>The pack name as it appears on disk, for logs and messages.</summary>
    public string DisplayName { get; }

    /// <summary>The C# files that make up the pack.</summary>
    public IReadOnlyList<string> SourceFiles { get; }

    /// <summary>Any prebuilt assemblies shipped with the pack.</summary>
    /// <remarks>
    /// Distributing a compiled DLL avoids the streamer needing the compiler at all, which is the
    /// sensible way to hand a finished effect to someone else.
    /// </remarks>
    public IReadOnlyList<string> AssemblyFiles { get; }

    /// <summary>A hash of everything in the pack, used to key the compile cache and to identify the loaded version in logs.</summary>
    public string Hash { get; }

    private CustomEffectPack(string id, string displayName, List<string> sourceFiles, List<string> assemblyFiles, string hash)
    {
        ID = id;
        DisplayName = displayName;
        SourceFiles = sourceFiles;
        AssemblyFiles = assemblyFiles;
        Hash = hash;
    }

    /// <summary>Finds every pack in the supplied folder.</summary>
    /// <param name="folder">The game's custom effect folder.</param>
    /// <param name="logger">The mod logger.</param>
    /// <returns>The discovered packs, in a stable order.</returns>
    public static List<CustomEffectPack> Discover(string folder, ManualLogSource logger)
    {
        List<CustomEffectPack> packs = new();
        if (!Directory.Exists(folder)) return packs;

        try
        {
            foreach (string dir in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileName(dir);
                if (name.StartsWith(".", StringComparison.Ordinal)) continue; //.cache and friends

                List<string> sources = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
                List<string> assemblies = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

                if ((sources.Count == 0) && (assemblies.Count == 0)) continue;

                packs.Add(Create(name, sources, assemblies));
            }

            foreach (string file in Directory.GetFiles(folder).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string extension = Path.GetExtension(file);
                bool isSource = string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase);
                bool isAssembly = string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase);
                if (!isSource && !isAssembly) continue;

                packs.Add(Create(
                    Path.GetFileNameWithoutExtension(file),
                    isSource ? [file] : [],
                    isAssembly ? [file] : []));
            }
        }
        catch (Exception e)
        {
            logger.LogError($"Could not read the custom effects folder: {e}");
        }

        return packs;
    }

    private static CustomEffectPack Create(string name, List<string> sources, List<string> assemblies)
        => new(CustomEffectPaths.SanitizeID(name), name, sources, assemblies, ComputeHash(sources, assemblies));

    /// <summary>The cache format version, bumped whenever a cached assembly stops being reusable.</summary>
    private const string CACHE_FORMAT = "1";

    /// <summary>
    /// Hashes the pack's contents together with the mod version, so that a mod update invalidates
    /// every cached assembly rather than running code compiled against an older effect API.
    /// </summary>
    private static string ComputeHash(List<string> sources, List<string> assemblies)
    {
        try
        {
            using SHA256 sha = SHA256.Create();
            using MemoryStream buffer = new();

            void Write(string s)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(s);
                buffer.Write(bytes, 0, bytes.Length);
            }

            Write(CACHE_FORMAT);
            Write(CrowdControlMod.MOD_VERSION);

            foreach (string file in sources.Concat(assemblies))
            {
                Write(Path.GetFileName(file).ToLowerInvariant());
                byte[] content = File.ReadAllBytes(file);
                buffer.Write(content, 0, content.Length);
            }

            buffer.Position = 0;
            byte[] hash = sha.ComputeHash(buffer);

            StringBuilder sb = new(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
        catch
        {
            //an unreadable pack gets a unique hash so it never collides with a cached build
            return "unreadable-" + Guid.NewGuid().ToString("n");
        }
    }
}
