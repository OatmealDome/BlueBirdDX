namespace BlueBirdDX.Database.Migration.AccountGroup;

public class AccountGroupMigrationManager : MigrationManager
{
    protected override string CollectionName => "accounts";

    protected override IEnumerable<IDocumentMigrator> Migrators => new List<IDocumentMigrator>()
    {
        //
    };
}