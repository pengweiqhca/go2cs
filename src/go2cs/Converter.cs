﻿//******************************************************************************************************
//  Converter.cs - Gbtc
//
//  Copyright © 2018, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  05/01/2018 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using static go2cs.Common;

#pragma warning disable SCS0018 // Path traversal

namespace go2cs
{
    public partial class Converter : GolangBaseListener
    {
        public const string RootNamespace = "go";
        public const string ClassSuffix = "_package";
        private const string UsingsMarker = ">>MARKER:USINGS<<";

        private readonly Options m_options;
        private readonly BufferedTokenStream m_tokenStream;
        private readonly GolangParser m_parser;
        private readonly string m_sourceFileName;
        private readonly string m_sourceFilePath;
        private readonly string m_targetFileName;
        private readonly string m_targetFilePath;
        private readonly StringBuilder m_targetFile = new StringBuilder();
        private readonly List<string> m_warnings = new List<string>();

        public Options Options => m_options;

        public string SourceFileName => m_sourceFileName;

        public string SourceFilePath => m_sourceFilePath;

        public string TargetFileName => m_targetFileName;

        public string TargetFilePath => m_targetFilePath;

        public string[] Warnings => m_warnings.ToArray();

        public Converter(BufferedTokenStream tokenStream, GolangParser parser, Options options, string fileName)
        {
            m_options = options;

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            if (!File.Exists(fileName))
                throw new FileNotFoundException($"WARNING: Source file \"{fileName}\" cannot be found.", fileName);

            m_tokenStream = tokenStream;
            m_parser = parser;
            m_sourceFileName = Path.GetFullPath(fileName);
            m_sourceFilePath = Path.GetDirectoryName(m_sourceFileName) ?? "";
            m_targetFileName = $"{Path.GetFileNameWithoutExtension(m_sourceFileName)}.cs";

            if (string.IsNullOrWhiteSpace(options.TargetPath))
                m_targetFilePath = m_sourceFilePath;
            else
                m_targetFilePath = Path.GetDirectoryName(Path.GetFullPath(options.TargetPath)) ?? "";

            if (!Directory.Exists(m_targetFilePath))
                Directory.CreateDirectory(m_targetFilePath);

            m_targetFileName = Path.Combine(m_targetFilePath, m_targetFileName);
        }

        public void Convert()
        {
            IParseTree sourceTree = m_parser.sourceFile();

            if (m_options.ShowParseTree)
                Console.WriteLine(sourceTree.ToStringTree(m_parser));
            
            // Walk parsed source tree to start visiting nodes
            ParseTreeWalker walker = new ParseTreeWalker();
            walker.Walk(this, sourceTree);

            // Write any end of file comments
            if (!string.IsNullOrWhiteSpace(m_nextDeclComments))
            {
                m_targetFile.AppendLine();
                m_targetFile.Append(FixForwardSpacing(m_nextDeclComments, indentLevel: 2));
            }                

            // Close class and namespaces as begun during Converter_TopLevelDecl visit
            m_targetFile.AppendLine($"{Spacing(indentLevel: 1)}}}");
            m_targetFile.AppendLine(new string('}', m_packageNamespaces.Length));

            string targetFile = m_targetFile.ToString();

            // Find usings marker
            int index = targetFile.IndexOf(UsingsMarker, StringComparison.Ordinal);

            // Insert required usings
            if (index > -1 && m_requiredUsings.Count > 0)
                targetFile = targetFile.Insert(index, $"{Environment.NewLine}{string.Join(Environment.NewLine, m_requiredUsings.Select(usingStmt => $"using {usingStmt};"))}{Environment.NewLine}");

            // Remove code markers
            targetFile = targetFile.Replace(UsingsMarker, "");

            using (StreamWriter writer = File.CreateText(m_targetFileName))
                writer.Write(targetFile);
        }

        private void AddWarning(ParserRuleContext context, string message)
        {
            m_warnings.Add($"{Path.GetFileName(m_sourceFileName)}:{context.Start.Line}:{context.Start.Column}: {message}");
        }

        private static readonly bool s_isPosix;
        private static readonly string s_goRoot;
        private static readonly string s_goPath;
        private static readonly string[] s_newLineDelimeters;
        private static readonly HashSet<string> s_processedFiles;
        private static readonly HashSet<string> s_processedImports;
        private static readonly HashSet<string> s_importQueue;
        private static int s_totalSkippedFiles;
        private static int s_totalSkippedPackages;
        private static int s_totalWarnings;

        static Converter()
        {
            s_isPosix = Path.DirectorySeparatorChar == '/';

            s_goRoot = Environment.GetEnvironmentVariable("GOROOT");

            if (string.IsNullOrWhiteSpace(s_goRoot))
                s_goRoot = Path.GetFullPath($"{Path.DirectorySeparatorChar}Go");

            s_goRoot = AddPathSuffix($"{AddPathSuffix(s_goRoot)}src");

            if (!Directory.Exists(s_goRoot))
                throw new InvalidOperationException($"Unable to resolve GOROOT src directory: \"{s_goRoot}\". Validate that Go is properly installed.");

            s_goPath = Environment.GetEnvironmentVariable("GOPATH");

            if (string.IsNullOrWhiteSpace(s_goPath))
                s_goPath = Environment.ExpandEnvironmentVariables(s_isPosix ? "$HOME/go" : "%USERPROFILE%\\go");

            s_goPath = AddPathSuffix($"{AddPathSuffix(s_goPath)}src");

            if (!Directory.Exists(s_goPath))
                Directory.CreateDirectory(s_goPath);

            s_newLineDelimeters = new[] { "\r\n", "\r", "\n" };

            s_processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            s_processedImports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            s_importQueue = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public static int TotalProcessedFiles => s_processedFiles.Count;

        public static int TotalSkippedFiles => s_totalSkippedFiles;

        public static int TotalSkippedPackages => s_totalSkippedPackages;

        public static int TotalWarnings => s_totalWarnings;

        public static void Convert(Options options)
        {
            string sourcePath = GetAbsolutePath(options.SourcePath);

            if (File.Exists(sourcePath))
            {
                ConvertFile(options, sourcePath);
            }
            else if (Directory.Exists(sourcePath))
            {
                Regex excludeExpression = options.GetExcludeExpression();
                bool convertFileMatch(string fileName) => !excludeExpression.IsMatch(fileName);

                foreach (string fileName in Directory.EnumerateFiles(sourcePath, "*.go", SearchOption.TopDirectoryOnly))
                {
                    if (convertFileMatch(fileName))
                        ConvertFile(options, fileName);
                }
            }
            else
            {
                throw new DirectoryNotFoundException($"WARNING: Source path \"{sourcePath}\" cannot be found.");
            }
        }

        public static void ConvertFile(Options options, string fileName)
        {
            if (s_processedFiles.Contains(fileName))
                return;

            s_processedFiles.Add(fileName);

            AntlrInputStream inputStream;

            using (StreamReader reader = File.OpenText(fileName))
                inputStream = new AntlrInputStream(reader);

            GolangLexer lexer = new GolangLexer(inputStream);
            CommonTokenStream tokenStream = new CommonTokenStream(lexer);
            GolangParser parser = new GolangParser(tokenStream);
            Converter converter = new Converter(tokenStream, parser, options, fileName);

            parser.RemoveErrorListeners();
            parser.AddErrorListener(new ParserErrorListener(converter));

            if (options.OverwriteExistingFiles || !File.Exists(converter.TargetFileName))
            {
                Console.WriteLine($"Converting from{Environment.NewLine}    \"{converter.SourceFileName}\" to{Environment.NewLine}    \"{converter.TargetFileName}\"...");
                converter.Convert();
                Console.WriteLine("    Finished.");

                if (!converter.PackageImport.Equals("main"))
                    Console.WriteLine($"   import \"{converter.PackageImport}\" ==> using {converter.PackageUsing}");
            }
            else
            {
                Console.WriteLine($"Skipping convert for{Environment.NewLine}    \"{converter.SourceFileName}\", target{Environment.NewLine}    \"{converter.TargetFileName}\" already exists.");
                s_totalSkippedFiles++;
            }

            string[] warnings = converter.Warnings;

            if (warnings.Length > 0)
            {
                Console.WriteLine();
                Console.WriteLine("WARNINGS:");

                foreach (string warning in warnings)
                    Console.WriteLine($"    {warning}");

                s_totalWarnings += warnings.Length;
            }

            Console.WriteLine();

            if (!options.LocalConvertOnly)
                ConvertImports(options);
        }

        private static void ConvertImports(Options options)
        {
            string[] imports = s_importQueue.ToArray();

            foreach (string import in imports)
            {
                if (s_processedImports.Contains(import))
                    continue;

                s_processedImports.Add(import);

                string importPath = AddPathSuffix(import.Replace("/", "\\"));
                string goRootImport = Path.Combine(s_goRoot, importPath);
                string goPathImport = Path.Combine(s_goPath, importPath);
                string targetPath = null;

                if (Directory.Exists(goRootImport))
                {
                    targetPath = Path.Combine(options.TargetGoSrcPath, importPath);

                    if (options.ConvertStandardLibrary)
                    {
                        Convert(Options.Clone(options, options.OverwriteExistingPackages, goRootImport, targetPath));
                    }
                    else
                    {
                        // Only count package conversion as skipped when there are no existing converted files
                        if (PathHasFiles(targetPath, "*.cs"))
                            continue;

                        Console.WriteLine($"Skipping convert for Go standard library import package \"{import}\".");
                        Console.WriteLine();
                        s_totalSkippedPackages++;
                    }
                }
                else if (Directory.Exists(goPathImport))
                {
                    if (!string.IsNullOrEmpty(options.TargetPath))
                        targetPath = Path.Combine(options.TargetPath, importPath);

                    Convert(Options.Clone(options, options.OverwriteExistingPackages, goPathImport, targetPath));
                }
                else
                {
                    Console.WriteLine($"WARNING: Failed to locate package \"{import}\" at either:");
                    Console.WriteLine($"    {goRootImport} (from %GOROOT%)");
                    Console.WriteLine($"    {goPathImport} (from %GOPATH%)");
                    Console.WriteLine();
                }
            }
        }

        private static string GetAbsolutePath(string filePath)
        {
            if (!Path.IsPathRooted(filePath))
                filePath = Path.Combine(s_goPath, filePath);

            return filePath;
        }

        private static bool PathHasFiles(string filePath, string searchPattern)
        {
            return Directory.Exists(filePath) && Directory.EnumerateFiles(filePath, searchPattern, SearchOption.TopDirectoryOnly).Any();
        }
    }
}
