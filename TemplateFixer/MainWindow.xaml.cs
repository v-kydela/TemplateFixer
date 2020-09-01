using Newtonsoft.Json.Linq;
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
            Console.WriteLine($"Template Fixer encountered an error. {((Exception)e.ExceptionObject).Message}");
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            if (AzHelper.CheckLoginFailure()) return;

            Console.WriteLine("Cleaning resource groups and app registrations...");

            var rgTask = ListAndDelete("resource group", AzHelper.ListResourceGroups, AzHelper.DeleteResourceGroup);
            var appTask = ListAndDelete("app registration", AzHelper.ListAppRegistrations, AzHelper.DeleteAppRegistration);

            Task.WaitAll(rgTask, appTask);

            Console.WriteLine("Finished cleaning");

            static Task ListAndDelete(string itemType, Func<IEnumerable<string>> listFunc, Func<string, ProcessResult> deleteFunc)
            {
                return Task.Run(() =>
                {
                    Console.WriteLine($"Checking {itemType}s...");

                    var items = listFunc();

                    if (items.Count() == 0)
                    {
                        Console.WriteLine($"No {itemType}s need to be deleted");

                        return;
                    }

                    Console.WriteLine($"Deleting {items.Count()} {itemType}(s)...");

                    var loopResult = Parallel.ForEach(items, (item, state, index) =>
                    {
                        Console.WriteLine($"Deleting {itemType}: {item}");

                        var deleteResult = deleteFunc(item);

                        if (deleteResult.ExitCode == 0)
                        {
                            Console.WriteLine($"{item} deleted successfully");
                        }
                        else
                        {
                            Console.WriteLine($"Couldn't delete {item}");
                        }
                    });

                    if (loopResult.IsCompleted)
                    {
                        Console.WriteLine($"Finished deleting {items.Count()} {itemType}(s)");
                    }
                    else
                    {
                        Console.WriteLine($"Couldn't delete all {itemType}s");
                    }
                });
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            SearchResults.Items.Clear();

            var directory = BasePath.Text;
            var searchPattern = SearchPattern.Text;

            Console.WriteLine($"Searching {directory} for {searchPattern}");

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
                    };

                    SearchResults.Items.Add(fileTemplate);
                }
                
                fileTemplate.Paths.Add(file);
            }

            Console.WriteLine($"Found {SearchResults.Items.Count} distinct results in {files.Count()} total results");
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            var selection = SearchResults.SelectedItems.Cast<DistinctTemplate>();

            if (selection.Count() == 0)
            {
                Console.WriteLine("No templates selected");

                return;
            }

            if (AzHelper.CheckLoginFailure()) return;

            Console.WriteLine($"Testing {selection.Count()} template(s)...");

            var loopResult = Parallel.ForEach(selection, (template, state) =>
            {
                Console.WriteLine($"Creating app registration for {template.GetName()}");

                var appResult = AzHelper.CreateAppRegistration(template.Index);

                if (appResult.ExitCode == 0)
                {
                    var appId = JObject.Parse(appResult.Output)[Properties.AppId].ToString();

                    Console.WriteLine($"Deploying {template.GetName()} with app ID {appId}");

                    var deployResult = AzHelper.Deploy(template, appId, template.Index);

                    if (deployResult.ExitCode == 0)
                    {
                        Console.WriteLine($"{template.GetName()} deployment successful");
                    }
                    else
                    {
                        Console.WriteLine($"{template.GetName()} deployment failed");
                    }
                }
                else
                {
                    Console.WriteLine($"Couldn't create app registration for {template.GetName()}. Try cleaning first.");

                    state.Stop();
                }
            });

            if (loopResult.IsCompleted)
            {
                Console.WriteLine($"Finished testing {selection.Count()} template(s)");
            }
            else
            {
                Console.WriteLine("Testing did not complete successfully");
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
                    template.Paths.Select(path => path
                        .Replace(BasePath.Text, string.Empty)
                        .Replace(SearchPattern.Text, string.Empty)))));

            JsonCode.Text = selection.Count() == 1 ? selection.Single().JObject.ToString() : string.Empty;
        }
    }
}
