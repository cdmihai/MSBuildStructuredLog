﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace StructuredLogger.Tests
{
    public class MSBuild
    {
        public static bool BuildProject(string projectText, params ILogger[] loggers)
        {
            var projectFile = TestUtilities.GetFullPath("build.proj");

            try
            {
                File.WriteAllText(projectFile, CleanupFileContents(projectText));

                var result = BuildManager.DefaultBuildManager.Build(
                    new BuildParameters
                    {
                        ShutdownInProcNodeOnBuildFinish = true,
                        EnableNodeReuse = false,
                        Loggers = loggers
                    },
                    new BuildRequestData(
                        projectFile,
                        new Dictionary<string, string>(),
                        null,
                        new string[0],
                        null));

                return result.OverallResult == BuildResultCode.Success;
            }
            finally
            {
                File.Delete(projectFile);
            }
        }

        public static Project CreateProjectInMemory(string projectText, ProjectCollection projectCollection = null, params ILogger[] loggers)
        {
            XmlReaderSettings readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            projectCollection = projectCollection ?? new ProjectCollection();

            Project project = new Project(
                XmlReader.Create(new StringReader(CleanupFileContents(projectText)), readerSettings),
                null,
                toolsVersion: null,
                projectCollection: projectCollection);

            Guid guid = Guid.NewGuid();
            project.FullPath = Path.GetFullPath("Temporary" + guid.ToString("N") + ".csproj");
            project.ReevaluateIfNecessary();

            return project;
        }

        const string msbuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

        /// <summary>
        /// Does certain replacements in a string representing the project file contents.
        /// This makes it easier to write unit tests because the author doesn't have
        /// to worry about escaping double-quotes, etc.
        /// </summary>
        /// <param name="projectFileContents"></param>
        /// <returns></returns>
        internal static string CleanupFileContents(string projectFileContents)
        {
            // Replace reverse-single-quotes with double-quotes.
            projectFileContents = projectFileContents.Replace("`", "\"");

            // Place the correct MSBuild namespace into the <Project> tag.
            projectFileContents = projectFileContents.Replace("msbuildnamespace", msbuildNamespace);
            projectFileContents = projectFileContents.Replace("msbuilddefaulttoolsversion", "15.0");
            projectFileContents = projectFileContents.Replace("msbuildassemblyversion", "15.1.0.0");

            return projectFileContents;
        }
    }
}
