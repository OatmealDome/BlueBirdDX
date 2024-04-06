using BlueBirdDX.Config;

BbConfig.Load();

if (!BbConfig.Exists())
{
    BbConfig.Instance.Save();

    Console.WriteLine("Wrote initial configuration, will now exit");

    return;
}
