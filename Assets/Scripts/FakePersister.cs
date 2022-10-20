using AdventureCore;

/// <summary>
/// does nothing, only here because before 1.1.2 AAK PersistedMovementBase does not work without a persister
/// </summary>
public class FakePersister : PersisterBase
{
    public override string PersistenceKey { get => null; set { } }
    public override PersistenceArea PersistenceArea { get => null; set { } }

    public override bool Check(string subKey = null) => false;
    public override void Clear(string subKey = null) { }
    public override T Get<T>(string subKey = null, T defaultValue = default) => defaultValue;
    public override void Set<T>(T value, string subKey = null) { }
}