
using System.CommandLine;
using System.Threading.Tasks;
using System;
using System.Data;
namespace fib
{
    internal class Program
    {

        static string[] SortFiles(string[] files, string sortBy)
        {



            if (sortBy == "name")
            {
                return files
                    .OrderBy(file => Path.GetFileName(file))
                    .ToArray();
            }

            if (sortBy == "type")
            {
                return files
                    .OrderBy(file => Path.GetExtension(file))
                    .ThenBy(file => Path.GetFileName(file))
                    .ToArray();
            }

            throw new ArgumentException("Sort must be 'name' or 'type'.");
        }

        static IEnumerable<string> GetFilesExceptBuildFolders(string directory)
        {
            string[] excludedFolders =
            {
        "bin",
        "obj",
        "debug",
        "release",
        ".git",
        "node_modules"
    };

            foreach (string file in Directory.EnumerateFiles(directory))
            {
                yield return file;
            }

            foreach (string subdirectory in Directory.EnumerateDirectories(directory))
            {
                string folderName = Path.GetFileName(subdirectory);

                if (excludedFolders.Contains(
                    folderName,
                    StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (string file in GetFilesExceptBuildFolders(subdirectory))
                {
                    yield return file;
                }
            }
        }
        static string[] GetCodeFiles(string directory, string[] languages)
        {
            string[] codeExtensions = { ".cs", ".js", ".ts", ".jsx", ".tsx", ".py", ".java", ".cpp", ".c", ".h", ".html", ".css" };

            string[] selectedExtensions;
            if (languages.Any(l => l.Equals("all", StringComparison.OrdinalIgnoreCase)))
                selectedExtensions = codeExtensions;
            else
            {
                selectedExtensions = languages.Select(language => "." + language.TrimStart('.').ToLower())
                .ToArray();
            }
            var invalidLanguages = selectedExtensions
                    .Except(codeExtensions, StringComparer.OrdinalIgnoreCase).ToArray();
            if (invalidLanguages.Length > 0)
            {
                throw new ArgumentException(
                    $"Unsupported language(s): {string.Join(", ", invalidLanguages)}");
            }
            return GetFilesExceptBuildFolders(directory)
                .Where(file => codeExtensions.Contains(
                    Path.GetExtension(file).ToLower()))
                .Where(file => selectedExtensions.Contains(
                    Path.GetExtension(file).ToLower()))
                .ToArray();
        }

        static string combineFiles(string[] files, bool note, bool removeEmptyLines, string author)
        {

            string res = "";
            if (!string.IsNullOrEmpty(author))
            {
                res += $"// Author: {author}{Environment.NewLine}";
            }

            foreach (string file in files)
            {
                if (note)
                    res += $"// File: {Path.GetRelativePath(Directory.GetCurrentDirectory(), file)}{Environment.NewLine}";
                string content = File.ReadAllText(file);

                if (removeEmptyLines)
                {
                    content = string.Join(
                        Environment.NewLine,
                        content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                               .Where(line => !string.IsNullOrWhiteSpace(line))
                    );
                }
                res += content + Environment.NewLine;
            }

            return res;
        }
        static async Task<int> Main(string[] args)
        {

            var rootCommand = new RootCommand("root command for file bundle cli");
            var bundleCommand = new Command("bundle", "bundle some files to a single file");
            var createRspCommand = new Command("create-rsp", "create-rsp response file to the command");

            var languageOption = new Option<string[]>("--language", "-l")
            {
                Description = "selected languages for the files",
                Required = true,
                Arity = ArgumentArity.OneOrMore
            };
            var outputOption = new Option<string>("--output", "-o")
            {
                Description = "the destination of the combined files",
                Required = true,
            };
            var noteOption = new Option<bool>("--note", "-n")
            {
                Description = "Add source file information as a comment",
            };
            var sortOption = new Option<string>("--sort", "-s")
            {
                Description = "Sort files by name or by type",
                DefaultValueFactory = _ => "name"
            };
            var removeEmptyLinesOption = new Option<bool>("--remove-empty-lines", "-r")
            {
                Description = "Remove empty lines from source files",

            };
            var authorOption = new Option<string>("--author", "-a")
            {
                Description = "Author name",

            };

            bundleCommand.SetAction((parseResult) =>
            {
                string[] languages = parseResult.GetValue(languageOption) ?? Array.Empty<string>();
                string output = parseResult.GetValue(outputOption) ?? string.Empty;
                string author = parseResult.GetValue(authorOption) ?? string.Empty;
                string sortBy = parseResult.GetValue(sortOption) ?? "name";
                bool removeEmptyLines = parseResult.GetValue(removeEmptyLinesOption);
                bool note = parseResult.GetValue(noteOption);
                string currentDirectory = Directory.GetCurrentDirectory();
                try
                {
                    string[] files = GetCodeFiles(currentDirectory, languages);
                    string outputPath = Path.GetFullPath(output);
                    if (files.Any(file =>
                        string.Equals(
                            Path.GetFullPath(file),
                            outputPath,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new ArgumentException("Output file cannot be one of the source files.");
                    }
                    files = SortFiles(files, sortBy);
                    string combinedContent = combineFiles(files, note, removeEmptyLines, author);
                    File.WriteAllText(output!, combinedContent);


                }
                catch (IOException e)
                {
                    Console.WriteLine($"File error:{e.Message}");
                }
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine($"Permission error:{e.Message}");
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Invalid input: {ex.Message}");
                }

            });

            createRspCommand.SetAction((parseResult) =>
            {
                Console.Write("Enter languages (for example: cs js, or all): ");
                string languages = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(languages))
                {
                    Console.WriteLine("Invalid input. Languages cannot be empty.");
                    return;
                }


                Console.Write("Enter output file name: ");
                string output = Console.ReadLine() ?? "";

                if (string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine("Invalid input. Output file name cannot be empty.");
                    return;
                }


                Console.Write("Include file notes? (y/n): ");
                string note = Console.ReadLine() ?? "n";
                note = note.Trim();

                if (!note.Equals("y", StringComparison.OrdinalIgnoreCase) &&
                    !note.Equals("n", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Invalid input. Please enter 'y' or 'n'.");
                    return;
                }

                Console.Write("Sort by name or type: ");
                string sort = Console.ReadLine() ?? "name";
                sort = sort.Trim();

                if (!sort.Equals("name", StringComparison.OrdinalIgnoreCase) &&
                    !sort.Equals("type", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Invalid sort. Please enter 'name' or 'type'.");
                    return;
                }

                Console.Write("Remove empty lines? (y/n): ");
                string removeEmptyLines = Console.ReadLine() ?? "n";

                removeEmptyLines = removeEmptyLines.Trim();

                if (!removeEmptyLines.Equals("y", StringComparison.OrdinalIgnoreCase) &&
                    !removeEmptyLines.Equals("n", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Invalid input. Please enter 'y' or 'n'.");
                    return;
                }

                Console.Write("Enter author name (optional): ");
                string author = Console.ReadLine() ?? "";

                Console.Write("Enter response file name: ");
                string responseFile = Console.ReadLine() ?? "bundle.rsp";

                string[] languageList = languages.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

                string command = $"bundle {string.Join(" ", languageList.Select(l => $"--language {l}"))} --output \"{output}\"";

                if (note.Equals("y", StringComparison.OrdinalIgnoreCase))
                    command += " --note";

                command += $" --sort {sort}";


                if (removeEmptyLines.Equals("y", StringComparison.OrdinalIgnoreCase))
                    command += " --remove-empty-lines";

                if (!string.IsNullOrWhiteSpace(author))
                    command += $" --author \"{author}\"";

                try
                {
                    File.WriteAllText(responseFile, command);
                }
                catch (IOException e)
                {
                    Console.WriteLine($"File error: {e.Message}");
                }
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine($"Permission error: {e.Message}");
                }


            });

            rootCommand.Subcommands.Add(bundleCommand);
            rootCommand.Subcommands.Add(createRspCommand);
            bundleCommand.Options.Add(languageOption);
            bundleCommand.Options.Add(outputOption);
            bundleCommand.Options.Add(noteOption);
            bundleCommand.Options.Add(sortOption);
            bundleCommand.Options.Add(removeEmptyLinesOption);
            bundleCommand.Options.Add(authorOption);

            var parseResult = rootCommand.Parse(args);

            if (parseResult.Errors.Count > 0)
            {
                Console.WriteLine("Invalid command input.");

                foreach (var error in parseResult.Errors)
                {
                    Console.WriteLine($"Error: {error.Message}");
                }

                return 1;
            }

            return await parseResult.InvokeAsync();





        }
    }
}
