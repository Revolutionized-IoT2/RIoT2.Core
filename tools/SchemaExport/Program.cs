using RIoT2.Core.Schema;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: RIoT2.Core.SchemaExport <directory>");
    return 2;
}

Directory.CreateDirectory(args[0]);
var path = Path.Combine(args[0], ContractSchemas.FileName);
var json = ContractSchemas.ExportJson();
if (!File.Exists(path) || File.ReadAllText(path) != json)
{
    File.WriteAllText(path, json);
    Console.WriteLine($"Wrote {Path.GetFullPath(path)}");
}
return 0;
