// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DocAsCode.Common;

#nullable enable

namespace Microsoft.DocAsCode.Build.Engine;

public class TemplateManager
{
    private readonly List<string> _templates;
    private readonly List<string>? _themes;
    private readonly string _baseDirectory;

    public TemplateManager(List<string> templates, List<string>? themes, string? baseDirectory)
    {
        _templates = templates;
        _themes = themes;
        _baseDirectory = baseDirectory ?? Directory.GetCurrentDirectory();
    }

    public bool TryExportTemplateFiles(string outputDirectory, string? regexFilter = null)
    {
        return TryExportResourceFiles(_templates, outputDirectory, true, regexFilter);
    }

    public TemplateProcessor GetTemplateProcessor(DocumentBuildContext context, int maxParallelism)
    {
        return new TemplateProcessor(CreateTemplateResource(_templates), context, maxParallelism);
    }

    public CompositeResourceReader CreateTemplateResource() => CreateTemplateResource(_templates);

    private CompositeResourceReader CreateTemplateResource(IEnumerable<string> resources)
    {
        return new(GetTemplateDirectories(resources).Select(path => new LocalFileResourceReader(path)));
    }

    public IEnumerable<string> GetTemplateDirectories()
    {
        return GetTemplateDirectories(_templates);
    }

    private IEnumerable<string> GetTemplateDirectories(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "templates", name));
            if (Directory.Exists(directory))
            {
                yield return directory;
            }

            directory = Path.GetFullPath(Path.Combine(_baseDirectory, name));
            if (Directory.Exists(directory))
            {
                yield return directory;
            }
        }
    }

    public void ProcessTheme(string outputDirectory, bool overwrite)
    {
        using (new LoggerPhaseScope("Apply Theme", LogLevel.Verbose))
        {
            if (_themes != null && _themes.Count > 0)
            {
                TryExportResourceFiles(_themes, outputDirectory, overwrite);
                Logger.LogInfo($"Theme(s) {_themes.ToDelimitedString()} applied.");
            }
        }
    }

    private bool TryExportResourceFiles(IEnumerable<string> resourceNames, string outputDirectory, bool overwrite, string? regexFilter = null)
    {
        if (string.IsNullOrEmpty(outputDirectory))
            throw new ArgumentNullException(nameof(outputDirectory));

        if (!resourceNames.Any())
            return false;

        bool isEmpty = true;

        using (new LoggerPhaseScope("ExportResourceFiles", LogLevel.Verbose))
        {
            using var templateResource = CreateTemplateResource(resourceNames);
            if (templateResource.IsEmpty)
            {
                Logger.Log(LogLevel.Warning, $"No resource found for [{StringExtension.ToDelimitedString(resourceNames)}].");
            }
            else
            {
                foreach (var pair in templateResource.GetResourceStreams(regexFilter))
                {
                    var outputPath = Path.Combine(outputDirectory, pair.Key);
                    CopyResource(pair.Value, outputPath, overwrite);
                    Logger.Log(LogLevel.Verbose, $"File {pair.Key} copied to {outputPath}.");
                    isEmpty = false;
                }
            }

            return !isEmpty;
        }
    }

    private static void CopyResource(Stream stream, string filePath, bool overwrite)
    {
        Copy(stream.CopyTo, filePath, overwrite);
    }

    private static void Copy(Action<Stream> streamHandler, string filePath, bool overwrite)
    {
        FileMode fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;
        try
        {
            var subfolder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(subfolder) && !Directory.Exists(subfolder))
            {
                Directory.CreateDirectory(subfolder);
            }

            using var fs = new FileStream(filePath, fileMode, FileAccess.ReadWrite, FileShare.ReadWrite);
            streamHandler(fs);
        }
        catch (IOException e)
        {
            // If the file already exists, skip
            Logger.Log(LogLevel.Info, $"File {filePath}: {e.Message}, skipped");
        }
    }
}
