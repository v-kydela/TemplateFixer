﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TemplateFixer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ConsoleHelper.WriteLine($"Template Fixer encountered an error. {((Exception)e.ExceptionObject).Message}");
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            if (AzHelper.CheckLoginFailure()) return;

            ConsoleHelper.WriteLine("Cleaning resource groups and app registrations...");

            var rgTask = ListAndDelete("resource group", AzHelper.ListResourceGroups, AzHelper.DeleteResourceGroup);
            var appTask = ListAndDelete("app registration", AzHelper.ListAppRegistrations, AzHelper.DeleteAppRegistration);

            Task.WaitAll(rgTask, appTask);

            ConsoleHelper.WriteLine("Finished cleaning");

            static Task ListAndDelete(string itemType, Func<IEnumerable<string>> listFunc, Func<string, ProcessResult> deleteFunc)
            {
                return Task.Run(() =>
                {
                    ConsoleHelper.WriteLine($"Checking {itemType}s...");

                    var items = listFunc();

                    if (items.Count() == 0)
                    {
                        Console.WriteLine($"No {itemType}s need to be deleted");

                        return;
                    }

                    ConsoleHelper.WriteLine($"Deleting {items.Count()} {itemType}(s)...");

                    var loopResult = Parallel.ForEach(items, (item, state, index) =>
                    {
                        ConsoleHelper.WriteLine($"Deleting {itemType}: {item}");

                        var deleteResult = deleteFunc(item);

                        if (deleteResult.ExitCode == 0)
                        {
                            ConsoleHelper.WriteLine($"{item} deleted successfully");
                        }
                        else
                        {
                            ConsoleHelper.WriteLine($"Couldn't delete {item}");
                        }
                    });

                    if (loopResult.IsCompleted)
                    {
                        ConsoleHelper.WriteLine($"Finished deleting {items.Count()} {itemType}(s)");
                    }
                    else
                    {
                        ConsoleHelper.WriteLine($"Couldn't delete all {itemType}s");
                    }
                });
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            SearchResults.Items.Clear();

            var directory = BasePath.Text;
            var searchPattern = SearchPattern.Text;

            ConsoleHelper.WriteLine($"Searching {directory} for {searchPattern}");

            var fileArray = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
            var files = fileArray.Where(file => new[] { @"\bin\", @"\obj\" }.All(segment => !file.Contains(segment)));

            foreach (var file in files)
            {
                var fileJson = File.ReadAllText(file);
                var fileJObject = JObject.Parse(fileJson);
                var fileTemplate = (DistinctTemplate)null;

                foreach (var template in SearchResults.Items.Cast<DistinctTemplate>())
                {
                    if (JToken.DeepEquals(template.JObject, fileJObject))
                    {
                        fileTemplate = template;

                        break;
                    }
                }

                if (fileTemplate is null)
                {
                    fileTemplate = new DistinctTemplate
                    {
                        Index = SearchResults.Items.Count,
                        JObject = fileJObject,
                        ExampleFile = file,
                    };

                    SearchResults.Items.Add(fileTemplate);
                }
                
                fileTemplate.Paths.Add(file.Replace(directory, string.Empty).Replace(searchPattern, string.Empty));
            }

            ConsoleHelper.WriteLine($"Found {SearchResults.Items.Count} distinct results in {files.Count()} total results");
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            var selection = SearchResults.SelectedItems.Cast<DistinctTemplate>();

            if (selection.Count() == 0)
            {
                ConsoleHelper.WriteLine("No templates selected");

                return;
            }

            if (AzHelper.CheckLoginFailure()) return;

            ConsoleHelper.WriteLine($"Testing {selection.Count()} template(s)...");

            var loopResult = Parallel.ForEach(selection, (template, state, index) =>
            {
                ConsoleHelper.WriteLine($"Creating app registration for {template.GetName()}");

                var appResult = AzHelper.CreateAppRegistration(index);

                if (appResult.ExitCode == 0)
                {
                    var appId = JObject.Parse(appResult.Output)[Properties.AppId].ToString();

                    ConsoleHelper.WriteLine($"Deploying {template.GetName()} with app ID {appId}");

                    var deployResult = AzHelper.Deploy(template, appId, index);

                    if (deployResult.ExitCode == 0)
                    {
                        ConsoleHelper.WriteLine($"{template.GetName()} deployment successful");
                    }
                    else
                    {
                        ConsoleHelper.WriteLine($"{template.GetName()} deployment failed");
                    }
                }
                else
                {
                    ConsoleHelper.WriteLine($"Couldn't create app registration for {template.GetName()}. Try cleaning first.");

                    state.Stop();
                }
            });

            if (loopResult.IsCompleted)
            {
                ConsoleHelper.WriteLine($"Finished testing {selection.Count()} template(s)");
            }
            else
            {
                ConsoleHelper.WriteLine("Testing did not complete successfully");
            }
        }

        private void Fix_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selection = SearchResults.SelectedItems.Cast<DistinctTemplate>();

            ResultPaths.Text = string.Join(
                Environment.NewLine,
                selection.Select(template => string.Join(
                    Environment.NewLine,
                    template.Paths)));

            if (selection.Count() == 1)
            {
                JsonCode.Text = selection.Single().JObject.ToString();
            }
            else
            {
                JsonCode.Text = string.Empty;
            }
        }
    }
}
