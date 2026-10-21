/// <summary>Where save text lives. Real runs use a folder; tests can use a temp folder or an in-memory fake.</summary>
public interface ISaveStore
{
    bool Exists(string name);
    bool TryRead(string name, out string text);
    /// <summary>Writes so that a crash never leaves a half-written main file.</summary>
    void WriteAtomic(string name, string text);
    void Delete(string name);
}
