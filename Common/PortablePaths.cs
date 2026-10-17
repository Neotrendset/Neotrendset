namespace Common;
public static class PortablePaths
{
 public static string DataDirectory=>Environment.GetEnvironmentVariable("ROMFORGE_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"RomForge-Mac");
 public static string KeyDirectory=>Environment.GetEnvironmentVariable("ROMFORGE_KEYS_DIR")??Path.Combine(DataDirectory,"keys");
}
