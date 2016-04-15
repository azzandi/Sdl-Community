﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Sdl.Community.StarTransit.Shared.Models;

namespace Sdl.Community.StarTransit.Shared.Services
{
    public class PackageService
    {
        private readonly Dictionary<string, string> _dictionaryPropetries = new Dictionary<string, string>(); 
        private  Dictionary<string,Dictionary<string,string>> _pluginDictionary = new Dictionary<string, Dictionary<string, string>>();
        private PackageModel _package = new PackageModel();
        private List<string> _fileNameList = new List<string>();
        private const char LanguageTargetSeparator = '|';

        public PackageModel OpenPackage(string packagePath)
        {
            
            var entryName = string.Empty;
            var pathToTempFolder= Path.GetTempPath();
            using (var archive = ZipFile.OpenRead(packagePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".PRJ", StringComparison.OrdinalIgnoreCase))
                    {

                        try
                        {
                            entry.ExtractToFile(Path.Combine(pathToTempFolder, entry.FullName));
                        }
                        catch (Exception e)
                        {
                        }
                        entryName = entry.FullName;
                    }

                 }
              

            }

            return ReadPackage(pathToTempFolder, entryName, packagePath);
        }

        private PackageModel ReadPackage(string path, string fileName,string packagePath)
        {
            var filePath = Path.Combine(path, fileName);
            var keyProperty = string.Empty;


            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {

                    try
                    {
                        if (line.Contains("["))
                        {
                            var valuesDictionaries = new Dictionary<string, string>();
                            if (keyProperty != string.Empty && _dictionaryPropetries.Count != 0)
                            {
                                foreach (var property in _dictionaryPropetries)
                                {
                                    valuesDictionaries.Add(property.Key, property.Value);
                                }
                                _pluginDictionary.Add(keyProperty, valuesDictionaries);
                                _dictionaryPropetries.Clear();
                            }

                            var firstPosition = line.IndexOf("[", StringComparison.Ordinal) + 1;
                            var lastPosition = line.IndexOf("]", StringComparison.Ordinal) - 1;
                            keyProperty = line.Substring(firstPosition, lastPosition);

                        }
                        else
                        {
                            var properties = line.Split('=');
                            _dictionaryPropetries.Add(properties[0], properties[1]);


                        }

                    }
                    catch (Exception ex)
                    {
                    }
                }
            }

            var packageModel = CreateModel(packagePath);


            //ar trebui sa sterg fisierul in mom in care s-a creat proiectul
            try
            {
                File.Delete(filePath);
            }
            catch (Exception exception)
            {
            }

            _package = packageModel;
            return packageModel;
        }

        private PackageModel CreateModel(string packagePath)
        {
            var model = new PackageModel();
            if (_pluginDictionary.ContainsKey("Admin"))
            {
                var propertiesDictionary = _pluginDictionary["Admin"];
                foreach (var key in propertiesDictionary.Keys)
                {
                    if (key == "ProjectName")
                    {
                        model.Name = propertiesDictionary["ProjectName"];
                    }
                }
            }

            if (_pluginDictionary.ContainsKey("Languages"))
            {
                var propertiesDictionary = _pluginDictionary["Languages"];
                foreach (var key in propertiesDictionary.Keys)
                {
                    if (key == "SourceLanguage")
                    {
                        var languageCode = int.Parse(propertiesDictionary["SourceLanguage"]);
                        model.SourceLanguage = Language(languageCode);
                    }
                    if (key == "TargetLanguages")
                    {
                        //we assume languages code are separated by "|"
                        var languages = propertiesDictionary["TargetLanguages"].Split(LanguageTargetSeparator);
                        var targetLanguagesList = new List<CultureInfo>();
                        foreach (var language in languages)
                        {
                            var languageCode = int.Parse(language);
                            var cultureInfo = Language(languageCode);
                            targetLanguagesList.Add(cultureInfo);
                        }
                        model.TargetLanguage = targetLanguagesList;
                    }
                }
            }
            var filesName = GetFilesName();

            var names=ExtractFilesFromArchive(filesName, packagePath);

            var targetFiles = AddTargetFiles(model, names);
            model.TargetFiles = targetFiles;

            var sourceFiles = AddSourceFiles(model, names);
            model.SourceFiles = sourceFiles;

            return model;

        }

        private string[] AddSourceFiles(PackageModel model, List<string> names)
        {
            var tempFiles = Directory.GetFiles(Path.GetTempPath());
            var extension = model.SourceLanguage.ThreeLetterWindowsLanguageName;
            var sourcePathList = new List<string>();
            //selects from temp folder files which ends with source language code
            var filesFromTemp = (from file in tempFiles where file.Contains(extension) select file).ToList();

            //selects from files name only the names which contains the source language code
            var sourceName = (from name in names where name.Contains(extension) select name).ToList();

            foreach (var name in sourceName)
            {
                var path = (from file in filesFromTemp where file.Contains(name) select file).ToList();
                sourcePathList.AddRange(path);
            }

            var files = new string[sourcePathList.Count];
            for (var i = 0; i < sourcePathList.Count; i++)
            {
                files[i] = sourcePathList[i];
            }

            return files;
        }


        private string[] AddTargetFiles(PackageModel model, List<string> filesName)
        {

            var pathList = new List<string>();
            var tempFiles = Directory.GetFiles(Path.GetTempPath());
            var pathTotargetFiles = new List<string>();
            var targetFilesName = new List<string>();



            foreach (var language in model.TargetLanguage)
            {
                var extension = language.ThreeLetterWindowsLanguageName;
                //selects from temp folder files which ends with target language code language
                var targetFiles = (from file in tempFiles
                                   where file.Contains(extension)
                                   select file).ToList();

                //selects from files name only the names which contains the target language code
                var names = (from name in filesName where name.Contains(extension) select name).ToList();
                pathList.AddRange(targetFiles);
                targetFilesName.AddRange(names);

            }

            foreach (var fileName in targetFilesName)
            {

                var targetPath = (from path in pathList where path.Contains(fileName) select path).ToList();
                pathTotargetFiles.AddRange(targetPath);
            }


            var files = new string[pathTotargetFiles.Count];
            for (var i = 0; i < pathTotargetFiles.Count; i++)
            {
                files[i] = pathTotargetFiles[i];
            }

            return files;
        }



        private List<string> ExtractFilesFromArchive(List<string> filesName,string packagePath)
        {
            var filesNameList = new List<string>();
            using (var archive = ZipFile.OpenRead(packagePath))
            {
                foreach (var entry in archive.Entries)
                {
                    foreach (var name in filesName)
                    {
                        if (entry.Name.Contains(name))
                        {
                            try
                            {
                                entry.ExtractToFile(Path.Combine(Path.GetTempPath(), entry.Name));
                            }
                            catch(Exception e) { }
                           
                            filesNameList.Add(entry.Name);
                        }
                    }
                }
            }
            return filesNameList;
        }

        private CultureInfo Language(int languageCode)
        {
            return new CultureInfo(languageCode);
        }

        /// <summary>
        /// Return a list of file names
        /// </summary>
        /// <returns></returns>
        private List<string> GetFilesName()
        {
            //takes values from dictionary
            var filesDictionary = _pluginDictionary["Files"];
            var fileNameList = new List<string>();

            //loop through the keys in order to take the name  
            foreach (var key in filesDictionary.Keys)
            {
                var file = filesDictionary[key];
                var fileName=FileName(file);
                fileNameList.Add(fileName);
            }
            return fileNameList;
        }

        /// <summary>
        /// Splits the text after "|" and take the file name 
        /// </summary>
        /// <param name="file"></param>
        /// <returns>file name</returns>
        private string FileName(string file)
        {
            var words = file.Split('|');
            var fileName = words[6];
            return fileName;
        }
    }
}