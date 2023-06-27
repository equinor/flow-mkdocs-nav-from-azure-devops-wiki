using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.CommandLine;
using System.CommandLine.Invocation;

class Program
{
    static async Task Main(string[] args)
    {
        var mkdockTemplate = new Option<FileInfo?> (
            name: "--template",
            description:  "MKDocs template file and path",
            getDefaultValue: () => new FileInfo("./mkdocs.yaml")
        );
        mkdockTemplate.AddAlias("-t");
        var wikiDirectory = new Option<DirectoryInfo?> (
            name: "--source",
            description:  "Source Wiki directory path",
            getDefaultValue: () => new DirectoryInfo("/Users/tivik/GitHub/Flow-Business-Applications.wiki")
        );
        wikiDirectory.AddAlias("-s");
        var destinationDirectory = new Option<DirectoryInfo?> (
            name: "--destination",
            description:  "Destination directory path",
            getDefaultValue: () => new DirectoryInfo("./output")
        );
        destinationDirectory.AddAlias("-d");
        var rootCommand = new RootCommand("Generate mkdocs navigation from azure wiki")
        {
            mkdockTemplate,
            wikiDirectory,
            destinationDirectory
        };

        rootCommand.SetHandler ((mkdockTemplate, wikiDirectory, destinationDirectory) => {
            if(destinationDirectory is not null && wikiDirectory is not null && mkdockTemplate is not null)
            {
                Directory.CreateDirectory(destinationDirectory.FullName);
                ModifyYaml(ProcessFile(wikiDirectory.FullName), wikiDirectory.FullName, destinationDirectory.FullName, mkdockTemplate);
            }
        },mkdockTemplate, wikiDirectory, destinationDirectory);
        await rootCommand.InvokeAsync(args);
    }

    static void ModifyYaml(List<(string Key, string Value, bool Folder)> listToPrint, string directoryStartPath, string outputPathName, FileInfo templateFile)
    {
        var yamlContentToModify = File.ReadLines(templateFile.FullName);
        var yamlContentWithoutNav = new List<string>();
        for (int i = 0; i < yamlContentToModify.Count(); i++)
        {
            if (!yamlContentToModify.ElementAt(i).StartsWith("nav:"))
            {
                yamlContentWithoutNav.Add(yamlContentToModify.ElementAt(i));
            }
            else
            {
                while (i < yamlContentToModify.Count() - 1)
                {
                    i++;
                    if (yamlContentToModify.ElementAt(i).StartsWith("  "))
                    {
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        using (var writer = new StreamWriter(outputPathName+'/'+templateFile.Name, false))
        {
            foreach (var item in yamlContentWithoutNav)
            {
                writer.WriteLine(item);
            }
            writer.WriteLine("nav:");
            //writer.WriteLine("  - 'User documents':");
            foreach (var item in listToPrint)
            {
                var preFix = "  - ";
                var split = "";
                if (item.Key.Replace(directoryStartPath, "").Any(c => c == '/'))
                {
                    split= "/";
                    preFix = new string(' ', item.Key.Replace(directoryStartPath, "").Count(c => c == '/')*2) + preFix;
                }
                if(!item.Value.EndsWith("index.md"))
                {
                    writer.WriteLine(preFix + "\""+ HttpUtility.UrlDecode(item.Value.Replace("-", " ")).Replace("\"","\\\"") +"\": "+ (item.Folder ? "" : "\"" + item.Key.Replace(directoryStartPath + "/", "").Replace(directoryStartPath, "") + split + item.Value + (item.Folder ? "\":" : ".md\"")));
                }
                else
                {
                    writer.WriteLine(item.Folder ? "" : preFix + "\"" + item.Key.Replace(directoryStartPath + "/", "").Replace(directoryStartPath, "") + split + item.Value + "\"");
                }
            }
        }
    }

    static List<(string Key, string Value, bool Folder)> ProcessFile(string pathToFile)
    {
        List<(string Key, string Value, bool Folder)> result = new();
        if (!File.Exists(pathToFile + "/.order"))
        {
            return result;
        }
        string contents = File.ReadAllText(pathToFile + "/.order");
        foreach (var line in contents.Split("\n"))
        {
            result.Add(new() { Key = pathToFile, Value = line.Trim(), Folder = File.Exists(pathToFile + "/" + line.Trim() + "/.order") });
            if (File.Exists(pathToFile + "/" + line.Trim() + "/.order"))
            {
                File.Copy(pathToFile + "/" + line.Trim() + ".md", pathToFile + "/" + line.Trim() +"/index.md" , true);
                result.Add(new() { Key = pathToFile + "/"  + line.Trim(), Value = "index.md", Folder = false});
                var subResult = (pathToFile + "/" + line.Trim());
                try
                {
                    result.AddRange(ProcessFile(subResult));
                }
                catch (Exception)
                {
                    Console.WriteLine("No .order file found in " + subResult);
                }
            }
        }
        return result;
    }
}
