﻿namespace Quartzmin.Helpers;

public static class PartialViewHelper
{
    public static void RegisterPartialView(IHandlebars handlebars)
    {
        ReadResource(handlebars);
    }

    private static void ReadResource(IHandlebars handlebars)
    {
        // Determine path
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePaths = assembly.GetManifestResourceNames();

        // Format: "{Namespace}.{Folder}.{filename}.{Extension}"
        var pathPrefix = $"{nameof(Quartzmin)}.Views.Partials.";
        foreach (var resourcePath in resourcePaths)
        {
            if (!resourcePath.StartsWith(pathPrefix))
            {
                continue;
            }

            var fileName = Path.GetFileNameWithoutExtension(resourcePath.Replace(pathPrefix, string.Empty));

            using Stream stream = assembly.GetManifestResourceStream(resourcePath);
            using StreamReader reader = new StreamReader(stream);
            var fileContent = reader.ReadToEnd();

            handlebars.RegisterTemplate(fileName, fileContent);
        }
    }
}