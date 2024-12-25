namespace BlueBirdDX.Database.Migration.UploadedMedia;

public class UploadedMediaMigrationManager : MigrationManager
{
    protected override string CollectionName => "media";

    protected override IEnumerable<IDocumentMigrator> Migrators => new List<IDocumentMigrator>()
    {
        new UploadedMediaMigratorOneToTwo()
    };
}