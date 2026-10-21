using System;
using System.IO;

/// <summary>The only Sim class allowed to touch System.IO.</summary>
public sealed class FileSaveStore : ISaveStore
{
    private readonly string root;

    public FileSaveStore(string root) { this.root = root; }

    private string PathOf(string name) => Path.Combine(root, name);

    public bool Exists(string name) => File.Exists(PathOf(name));

    public bool TryRead(string name, out string text)
    {
        text = null;
        try
        {
            string p = PathOf(name);
            if (!File.Exists(p)) return false;
            text = File.ReadAllText(p);
            return true;
        }
        catch (Exception) { return false; }
    }

    public void WriteAtomic(string name, string text)
    {
        Directory.CreateDirectory(root);
        string path = PathOf(name);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
        else File.Move(tmp, path);
    }

    public void Delete(string name)
    {
        string path = PathOf(name);
        foreach (string p in new[] { path, path + ".bak", path + ".tmp" })
            if (File.Exists(p)) File.Delete(p);
    }
}
