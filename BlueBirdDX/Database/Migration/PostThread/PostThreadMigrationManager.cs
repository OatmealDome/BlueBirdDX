namespace BlueBirdDX.Database.Migration.PostThread;

public class PostThreadMigrationManager : MigrationManager
{
    protected override string CollectionName => "threads";

    protected override IEnumerable<IDocumentMigrator> Migrators => new List<IDocumentMigrator>()
    {
        new PostThreadMigratorOneToTwo(),
    };
}